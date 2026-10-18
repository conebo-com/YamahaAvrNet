using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Exceptions;
using YamahaAvrNet.Models;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Protocols.ExtendedControl
{
    /// <summary>
    /// Shared Extended Control HTTP transport for discovery and explicitly gated zone actions.
    /// Each request is serialized; the action methods are never called during discovery.
    /// </summary>
    internal sealed class YamahaExtendedHttpClient : IYamahaExtendedControl, IDisposable
    {
        private const int MaximumResponseBytes = 2 * 1024 * 1024;
        private const string ApiPrefix = "/YamahaExtendedControl/v1/";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly HttpClient _http;
        private readonly Uri _baseUri;
        private readonly TimeSpan _timeout;
        private readonly SemaphoreSlim _requestGate = new SemaphoreSlim(1, 1);
        private bool _disposed;

        internal YamahaExtendedHttpClient(YamahaAvrOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var builder = new UriBuilder(Uri.UriSchemeHttp, options.Host, options.HttpPort);
            _baseUri = builder.Uri;
            _timeout = options.RequestTimeout;
            _http = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false
            });
            _http.Timeout = Timeout.InfiniteTimeSpan;
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<YamahaExtendedDeviceInfo> GetDeviceInfoAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string json = await GetJsonAsync("system/getDeviceInfo", cancellationToken).ConfigureAwait(false);
            return YamahaExtendedResponseParser.ParseDeviceInfo(json);
        }

        public async Task<YamahaExtendedFeatures> GetFeaturesAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string json = await GetJsonAsync("system/getFeatures", cancellationToken).ConfigureAwait(false);
            return YamahaExtendedResponseParser.ParseFeatures(json);
        }

        public async Task<YamahaExtendedFunctionStatus> GetFuncStatusAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string json = await GetJsonAsync("system/getFuncStatus", cancellationToken).ConfigureAwait(false);
            return YamahaExtendedResponseParser.ParseFuncStatus(json);
        }

        internal async Task<YamahaSoundStatus> GetZoneSoundStatusAsync(string zone,
            CancellationToken cancellationToken)
        {
            string json = await GetJsonAsync(zone + "/getStatus", cancellationToken).ConfigureAwait(false);
            return YamahaExtendedResponseParser.ParseZoneSoundStatus(json, zone);
        }

        internal async Task<YamahaAudioSignalInfo> GetZoneSignalInfoAsync(string zone,
            CancellationToken cancellationToken)
        {
            string json = await GetJsonAsync(zone + "/getSignalInfo", cancellationToken)
                .ConfigureAwait(false);
            return YamahaExtendedResponseParser.ParseZoneSignalInfo(json, zone);
        }

        internal async Task SetSurroundDecoderTypeAsync(string zone, string nativeType,
            CancellationToken cancellationToken)
        {
            // Yamaha's 2018 document has a copy/paste typo in the URI table.
            // Its example and other implementations use setSurroundDecoderType.
            string operation = zone + "/setSurroundDecoderType?type=" + Uri.EscapeDataString(nativeType);
            string json = await GetJsonAsync(operation, cancellationToken).ConfigureAwait(false);
            YamahaExtendedResponseParser.ValidateCommandResponse(json, "setSurroundDecoderType");
        }

        internal async Task SetSoundProgramAsync(string zone, string nativeProgram,
            CancellationToken cancellationToken)
        {
            string operation = zone + "/setSoundProgram?program=" + Uri.EscapeDataString(nativeProgram);
            string json = await GetJsonAsync(operation, cancellationToken).ConfigureAwait(false);
            YamahaExtendedResponseParser.ValidateCommandResponse(json, "setSoundProgram");
        }

        internal async Task RecallSceneAsync(string zone, int number,
            CancellationToken cancellationToken)
        {
            string operation = zone + "/recallScene?num=" +
                number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string json = await GetJsonAsync(operation, cancellationToken).ConfigureAwait(false);
            YamahaExtendedResponseParser.ValidateCommandResponse(json, "recallScene");
        }

        private async Task<string> GetJsonAsync(string operation, CancellationToken ct)
        {
            EnsureNotDisposed();
            ct.ThrowIfCancellationRequested();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(_timeout);
                bool gateEntered = false;
                try
                {
                    await _requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
                    gateEntered = true;
                    EnsureNotDisposed();

                    var uri = new Uri(_baseUri, ApiPrefix + operation);
                    using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                    using (var response = await _http.SendAsync(request,
                        HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new YamahaExtendedControlException(
                                operation + " returned HTTP " + (int)response.StatusCode + ".");
                        long? length = response.Content.Headers.ContentLength;
                        if (length.HasValue && length.Value > MaximumResponseBytes)
                            throw new YamahaExtendedControlException(operation + " exceeded the response size limit.");

                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var memory = new MemoryStream())
                        {
                            var buffer = new byte[8192];
                            int read;
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length,
                                timeout.Token).ConfigureAwait(false)) > 0)
                            {
                                if (memory.Length + read > MaximumResponseBytes)
                                    throw new YamahaExtendedControlException(
                                        operation + " exceeded the response size limit.");
                                memory.Write(buffer, 0, read);
                            }
                            return StrictUtf8.GetString(memory.ToArray());
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (ct.IsCancellationRequested)
                        throw;
                    throw new YamahaTimeoutException(operation + " timed out.", ex);
                }
                catch (HttpRequestException ex)
                {
                    throw new YamahaConnectionException(operation + " failed to contact the receiver.", ex);
                }
                catch (WebException ex)
                {
                    throw new YamahaConnectionException(operation + " failed to contact the receiver.", ex);
                }
                catch (DecoderFallbackException ex)
                {
                    throw new YamahaExtendedControlException(operation + " returned invalid UTF-8.", ex);
                }
                finally
                {
                    if (gateEntered)
                        _requestGate.Release();
                }
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(YamahaExtendedHttpClient));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _http.Dispose();
            // Do not dispose the semaphore while an outstanding async call may release it.
        }
    }
}
