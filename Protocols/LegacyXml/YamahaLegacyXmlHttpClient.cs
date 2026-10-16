using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using YamahaAvrNet.Exceptions;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Protocols.LegacyXml
{
    /// <summary>Bounded HTTP/XML transport. Writes are initiated only through guarded feature controllers.</summary>
    internal sealed class YamahaLegacyXmlHttpClient : IDisposable
    {
        private const int MaximumResponseBytes = 2 * 1024 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly HttpClient _http;
        private readonly Uri _endpoint;
        private readonly TimeSpan _timeout;
        private readonly SemaphoreSlim _requestGate = new SemaphoreSlim(1, 1);
        private bool _disposed;

        internal YamahaLegacyXmlHttpClient(YamahaAvrOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            _endpoint = new Uri(new UriBuilder(Uri.UriSchemeHttp, options.Host, options.HttpPort).Uri,
                "/YamahaRemoteControl/ctrl");
            _timeout = options.RequestTimeout;
            _http = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false
            });
            _http.Timeout = Timeout.InfiniteTimeSpan;
        }

        internal Task<string> SendGetAsync(XDocument requestDocument, CancellationToken ct)
        {
            return SendAsync(requestDocument, "GET", ct);
        }

        internal Task<string> SendPutAsync(XDocument requestDocument, CancellationToken ct)
        {
            return SendAsync(requestDocument, "PUT", ct);
        }

        private async Task<string> SendAsync(XDocument requestDocument, string commandType,
            CancellationToken ct)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(YamahaLegacyXmlHttpClient));
            if (requestDocument == null)
                throw new ArgumentNullException(nameof(requestDocument));
            ct.ThrowIfCancellationRequested();
            if (requestDocument.Root == null ||
                !string.Equals((string)requestDocument.Root.Attribute("cmd"), commandType,
                    StringComparison.OrdinalIgnoreCase))
                throw new YamahaValidationException("Legacy XML command type mismatch.");
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(_timeout);
                bool entered = false;
                try
                {
                    await _requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
                    entered = true;
                    if (_disposed)
                        throw new ObjectDisposedException(nameof(YamahaLegacyXmlHttpClient));
                    using (var content = new StringContent(requestDocument.ToString(
                        SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml"))
                    using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
                    {
                        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml")
                        {
                            CharSet = "utf-8"
                        };
                        request.Content = content;
                        using (var response = await _http.SendAsync(request,
                            HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                        {
                            if (!response.IsSuccessStatusCode)
                                throw new YamahaLegacyXmlException(
                                    "Legacy XML " + commandType + " returned HTTP " + (int)response.StatusCode + ".");
                            long? length = response.Content.Headers.ContentLength;
                            if (length.HasValue && length.Value > MaximumResponseBytes)
                                throw new YamahaLegacyXmlException("Legacy XML response is too large.");
                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var memory = new MemoryStream())
                            {
                                byte[] buffer = new byte[8192];
                                int n;
                                while ((n = await stream.ReadAsync(buffer, 0, buffer.Length,
                                    timeout.Token).ConfigureAwait(false)) > 0)
                                {
                                    if (memory.Length + n > MaximumResponseBytes)
                                        throw new YamahaLegacyXmlException("Legacy XML response is too large.");
                                    memory.Write(buffer, 0, n);
                                }
                                return StrictUtf8.GetString(memory.ToArray());
                            }
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (ct.IsCancellationRequested)
                        throw;
                    throw new YamahaTimeoutException("Legacy XML " + commandType +
                        " timed out. The receiver state is unknown; read it again before another write.", ex);
                }
                catch (HttpRequestException ex)
                {
                    throw new YamahaConnectionException("Legacy XML endpoint is unreachable.", ex);
                }
                catch (WebException ex)
                {
                    throw new YamahaConnectionException("Legacy XML endpoint is unreachable.", ex);
                }
                catch (DecoderFallbackException ex)
                {
                    throw new YamahaLegacyXmlException("Legacy XML response is not valid UTF-8.", ex);
                }
                finally
                {
                    if (entered)
                        _requestGate.Release();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _http.Dispose();
        }
    }
}
