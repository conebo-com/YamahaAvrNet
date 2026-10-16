using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Models;
using YamahaAvrNet.Protocols.LegacyXml;

namespace YamahaAvrNet.Features.SpeakerSetup
{
    /// <summary>
    /// Reads Yamaha-native speaker settings via known legacy XML aggregate paths.
    /// No writes or pattern switches are performed by this implementation.
    /// </summary>
    internal sealed class YamahaSpeakerSetupController : IYamahaSpeakerSetup
    {
        private readonly Action _ensureNotDisposed;
        private readonly YamahaLegacyXmlHttpClient _legacy;
        private readonly Func<int?> _getReportedPatternCount;
        private readonly Func<CancellationToken, Task<int?>> _getExtendedActivePattern;
        private readonly Action<string, DateTimeOffset> _recordVerifiedRead;

        internal YamahaSpeakerSetupController(Action ensureNotDisposed,
            YamahaLegacyXmlHttpClient legacy,
            Func<int?> getReportedPatternCount,
            Func<CancellationToken, Task<int?>> getExtendedActivePattern,
            Action<string, DateTimeOffset> recordVerifiedRead)
        {
            _ensureNotDisposed = ensureNotDisposed ??
                throw new ArgumentNullException(nameof(ensureNotDisposed));
            _legacy = legacy;
            _getReportedPatternCount = getReportedPatternCount ??
                throw new ArgumentNullException(nameof(getReportedPatternCount));
            _getExtendedActivePattern = getExtendedActivePattern;
            _recordVerifiedRead = recordVerifiedRead ??
                throw new ArgumentNullException(nameof(recordVerifiedRead));
        }

        public async Task<int?> GetActivePatternAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (_legacy != null)
            {
                string xml = await _legacy.SendGetAsync(
                    YamahaLegacyXmlCommandBuilder.ActivePattern(), cancellationToken)
                    .ConfigureAwait(false);
                return YamahaLegacyXmlResponseParser.ParseActivePattern(xml);
            }
            if (_getExtendedActivePattern != null)
                return await _getExtendedActivePattern(cancellationToken).ConfigureAwait(false);
            throw LegacyUnavailable();
        }

        public async Task<IReadOnlyList<YamahaSpeakerPatternInfo>> GetPatternsAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRead();
            cancellationToken.ThrowIfCancellationRequested();
            int? count = _getReportedPatternCount();
            if (!count.HasValue || count.Value < 1 || count.Value > 32)
                throw new NotSupportedException(
                    "The device has not provided a usable speaker pattern count. " +
                    "Initialize Extended Control first, or read one explicit pattern.");

            int? active = await GetActivePatternAsync(cancellationToken).ConfigureAwait(false);
            var results = new List<YamahaSpeakerPatternInfo>();
            for (int pattern = 1; pattern <= count.Value; pattern++)
            {
                var levels = await GetLevelsAsync(pattern, cancellationToken).ConfigureAwait(false);
                var distances = await GetDistancesAsync(pattern, cancellationToken).ConfigureAwait(false);
                var nativeIds = new HashSet<string>(levels.Keys, StringComparer.Ordinal);
                nativeIds.UnionWith(distances.Keys);
                var channels = new List<YamahaSpeakerChannelInfo>();
                foreach (string id in nativeIds.OrderBy(x => x, StringComparer.Ordinal))
                    channels.Add(new YamahaSpeakerChannelInfo(
                        id, isAddressable: true,
                        canReadLevel: levels.ContainsKey(id) ? (bool?)true : null,
                        canReadDistance: distances.ContainsKey(id) ? (bool?)true : null));
                results.Add(new YamahaSpeakerPatternInfo(pattern, active == pattern, channels));
            }
            return new ReadOnlyCollection<YamahaSpeakerPatternInfo>(results);
        }

        public async Task<IReadOnlyDictionary<string, YamahaSpeakerValue>> GetLevelsAsync(
            int pattern, CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRead();
            cancellationToken.ThrowIfCancellationRequested();
            CheckPattern(pattern);
            string xml = await _legacy.SendGetAsync(YamahaLegacyXmlCommandBuilder.Levels(pattern),
                cancellationToken).ConfigureAwait(false);
            var values = YamahaLegacyXmlResponseParser.ParseLevels(xml, pattern);
            _recordVerifiedRead("speaker.pattern." + pattern + ".levels", DateTimeOffset.UtcNow);
            return values;
        }

        public async Task<IReadOnlyDictionary<string, YamahaSpeakerValue>> GetDistancesAsync(
            int pattern, CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRead();
            cancellationToken.ThrowIfCancellationRequested();
            CheckPattern(pattern);
            string xml = await _legacy.SendGetAsync(YamahaLegacyXmlCommandBuilder.Distances(pattern),
                cancellationToken).ConfigureAwait(false);
            var values = YamahaLegacyXmlResponseParser.ParseDistances(xml, pattern);
            _recordVerifiedRead("speaker.pattern." + pattern + ".distances", DateTimeOffset.UtcNow);
            return values;
        }

        public Task SetLevelAsync(int pattern, string nativeId, decimal decibels,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            CheckPattern(pattern);
            CheckChannel(nativeId);
            throw WritesUnavailable();
        }

        public Task SetDistanceAsync(int pattern, string nativeId, decimal meters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            CheckPattern(pattern);
            CheckChannel(nativeId);
            throw WritesUnavailable();
        }

        private void CheckRead()
        {
            _ensureNotDisposed();
            if (_legacy == null)
                throw LegacyUnavailable();
        }

        private static void CheckPattern(int pattern)
        {
            if (pattern < 1 || pattern > 99)
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }

        private static void CheckChannel(string nativeId)
        {
            if (string.IsNullOrWhiteSpace(nativeId))
                throw new ArgumentException("A native speaker channel ID is required.", nameof(nativeId));
        }

        private static NotSupportedException LegacyUnavailable()
        {
            return new NotSupportedException(
                "Legacy Yamaha HTTP/XML is disabled or unavailable for this client.");
        }

        private static NotSupportedException WritesUnavailable()
        {
            return new NotSupportedException(
                "Speaker PUT commands are not implemented. No receiver setting was changed.");
        }
    }
}
