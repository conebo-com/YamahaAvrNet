using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using YamahaAvrNet.Models;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Discovery
{
    /// <summary>
    /// Optional GET-only Yamaha description lookup. Never follows redirects,
    /// uses external entities, or interprets descriptor entries as writable.
    /// </summary>
    internal sealed class YamahaLegacyDescriptorProbe : IDisposable
    {
        private const int MaxBytes = 1024 * 1024;
        private const int MaxCommands = 2048;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly Uri _baseUri;
        private readonly TimeSpan _timeout;
        private readonly HttpClient _http;
        private bool _disposed;

        internal YamahaLegacyDescriptorProbe(YamahaAvrOptions options)
        {
            _baseUri = new UriBuilder(Uri.UriSchemeHttp, options.Host, options.HttpPort).Uri;
            _timeout = options.RequestTimeout;
            _http = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false
            });
            _http.Timeout = Timeout.InfiniteTimeSpan;
        }

        internal async Task<YamahaDescriptorProbeResult> ProbeAsync(CancellationToken cancellationToken)
        {
            var attempts = new List<YamahaDescriptorAttempt>();
            foreach (string path in new[] {
                "/YamahaRemoteControl/desc.xml", "/YamahaRemoteControl/UnitDesc.xml" })
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    string xml = await ReadDocumentAsync(path, cancellationToken).ConfigureAwait(false);
                    YamahaLegacyDescriptor parsed = Parse(xml, path);
                    attempts.Add(new YamahaDescriptorAttempt(path,
                        YamahaDescriptorAttemptStatus.Found, 200, "Recognized Yamaha legacy description XML."));
                    return new YamahaDescriptorProbeResult(parsed,
                        new YamahaLegacyDescriptorDiagnostic(attempts, DateTimeOffset.UtcNow));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (DescriptorHttpStatusException ex)
                {
                    var status = ex.StatusCode == 404 || ex.StatusCode == 410 ?
                        YamahaDescriptorAttemptStatus.NotFound : YamahaDescriptorAttemptStatus.HttpError;
                    attempts.Add(new YamahaDescriptorAttempt(path, status, ex.StatusCode,
                        "HTTP " + ex.StatusCode));
                }
                catch (XmlException ex)
                {
                    attempts.Add(new YamahaDescriptorAttempt(path,
                        YamahaDescriptorAttemptStatus.InvalidXml, null, ex.Message));
                }
                catch (InvalidDataException ex)
                {
                    var status = ex.Message.StartsWith("Response is not", StringComparison.Ordinal) ?
                        YamahaDescriptorAttemptStatus.UnrecognizedDescription :
                        YamahaDescriptorAttemptStatus.InvalidContent;
                    attempts.Add(new YamahaDescriptorAttempt(path, status, null, ex.Message));
                }
                catch (DecoderFallbackException ex)
                {
                    attempts.Add(new YamahaDescriptorAttempt(path,
                        YamahaDescriptorAttemptStatus.InvalidContent, null, ex.Message));
                }
                catch (OperationCanceledException)
                {
                    attempts.Add(new YamahaDescriptorAttempt(path,
                        YamahaDescriptorAttemptStatus.Timeout, null, "Request timed out."));
                }
                catch (HttpRequestException ex)
                {
                    attempts.Add(new YamahaDescriptorAttempt(path,
                        YamahaDescriptorAttemptStatus.NetworkError, null, ex.Message));
                }
                catch (IOException ex)
                {
                    attempts.Add(new YamahaDescriptorAttempt(path,
                        YamahaDescriptorAttemptStatus.NetworkError, null, ex.Message));
                }
            }
            return new YamahaDescriptorProbeResult(null,
                new YamahaLegacyDescriptorDiagnostic(attempts, DateTimeOffset.UtcNow));
        }

        internal sealed class YamahaDescriptorProbeResult
        {
            internal YamahaDescriptorProbeResult(YamahaLegacyDescriptor descriptor,
                YamahaLegacyDescriptorDiagnostic diagnostic)
            {
                Descriptor = descriptor;
                Diagnostic = diagnostic;
            }
            internal YamahaLegacyDescriptor Descriptor { get; private set; }
            internal YamahaLegacyDescriptorDiagnostic Diagnostic { get; private set; }
        }

        private sealed class DescriptorHttpStatusException : Exception
        {
            internal DescriptorHttpStatusException(int statusCode) { StatusCode = statusCode; }
            internal int StatusCode { get; private set; }
        }

        private async Task<string> ReadDocumentAsync(string path, CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(YamahaLegacyDescriptorProbe));
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(_timeout);
                using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, path)))
                using (var response = await _http.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new DescriptorHttpStatusException((int)response.StatusCode);
                    if (response.Content.Headers.ContentLength > MaxBytes)
                        throw new InvalidDataException("Descriptor exceeded maximum length.");
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192];
                        int count;
                        while ((count = await stream.ReadAsync(buffer, 0, buffer.Length,
                            timeout.Token).ConfigureAwait(false)) != 0)
                        {
                            if (output.Length + count > MaxBytes)
                                throw new InvalidDataException("Descriptor exceeded maximum length.");
                            output.Write(buffer, 0, count);
                        }
                        return StrictUtf8.GetString(output.ToArray());
                    }
                }
            }
        }

        internal static YamahaLegacyDescriptor Parse(string xml, string path)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new InvalidDataException("Empty Yamaha description.");
            XDocument document;
            using (var input = new StringReader(xml))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxBytes,
                MaxCharactersFromEntities = 0
            }))
                document = XDocument.Load(reader, LoadOptions.None);
            if (document.Root == null)
                throw new InvalidDataException("Yamaha description has no root element.");
            string rootName = document.Root.Name.LocalName;
            if (!string.Equals(rootName, "Unit_Desc", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rootName, "YAMAHA_AV", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Response is not a recognized Yamaha unit description.");

            // Descriptors vary substantially by model. Only clearly labelled
            // command identifiers are extracted, never synthesized GET/PUT paths.
            var commands = new List<string>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int observed = 0;
            foreach (var node in document.Descendants())
            {
                if (!string.Equals(node.Name.LocalName, "Cmd", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(node.Name.LocalName, "Command", StringComparison.OrdinalIgnoreCase))
                    continue;
                observed++;
                if (observed > MaxCommands)
                    throw new InvalidDataException("Description contains too many command entries.");
                string id = (string)node.Attribute("ID") ?? (string)node.Attribute("id");
                if (id == null && !node.HasElements) id = node.Value;
                if (string.IsNullOrWhiteSpace(id)) continue;
                id = id.Trim();
                if (id.Length <= 192 && unique.Add(id)) commands.Add(id);
            }
            return new YamahaLegacyDescriptor(path, rootName,
                new ReadOnlyCollection<string>(commands), observed, DateTimeOffset.UtcNow);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _http.Dispose();
        }
    }
}
