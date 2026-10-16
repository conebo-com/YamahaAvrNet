using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Exceptions;
using YamahaAvrNet.Models;
using YamahaAvrNet.Protocols.LegacyXml;

namespace YamahaAvrNet.Features.SpeakerSetup
{
    /// <summary>
    /// Reads native speaker settings and performs explicitly opted-in, RX-A3060-verified writes.
    /// Other models, firmwares, patterns and channels are intentionally read-only.
    /// </summary>
    internal sealed class YamahaSpeakerSetupController : IYamahaSpeakerSetup
    {
        private readonly Action _ensureNotDisposed;
        private readonly YamahaLegacyXmlHttpClient _legacy;
        private readonly Func<int?> _getReportedPatternCount;
        private readonly Func<CancellationToken, Task<int?>> _getExtendedActivePattern;
        private readonly Action<string, DateTimeOffset> _recordVerifiedRead;
        private readonly Func<string> _getModel;
        private readonly Func<string> _getFirmware;
        private readonly bool _enableVerifiedSpeakerWrites;
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);

        internal YamahaSpeakerSetupController(Action ensureNotDisposed,
            YamahaLegacyXmlHttpClient legacy,
            Func<int?> getReportedPatternCount,
            Func<CancellationToken, Task<int?>> getExtendedActivePattern,
            Action<string, DateTimeOffset> recordVerifiedRead,
            Func<string> getModel,
            Func<string> getFirmware,
            bool enableVerifiedSpeakerWrites)
        {
            _ensureNotDisposed = ensureNotDisposed ??
                throw new ArgumentNullException(nameof(ensureNotDisposed));
            _legacy = legacy;
            _getReportedPatternCount = getReportedPatternCount ??
                throw new ArgumentNullException(nameof(getReportedPatternCount));
            _getExtendedActivePattern = getExtendedActivePattern;
            _recordVerifiedRead = recordVerifiedRead ??
                throw new ArgumentNullException(nameof(recordVerifiedRead));
            _getModel = getModel ?? throw new ArgumentNullException(nameof(getModel));
            _getFirmware = getFirmware ?? throw new ArgumentNullException(nameof(getFirmware));
            _enableVerifiedSpeakerWrites = enableVerifiedSpeakerWrites;
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
            return WriteVerifiedAsync(pattern, nativeId, decibels, isDistance: false,
                cancellationToken: cancellationToken);
        }

        public Task SetDistanceAsync(int pattern, string nativeId, decimal meters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return WriteVerifiedAsync(pattern, nativeId, meters, isDistance: true,
                cancellationToken: cancellationToken);
        }

        private async Task WriteVerifiedAsync(int pattern, string nativeId, decimal requested,
            bool isDistance, CancellationToken cancellationToken)
        {
            CheckRead();
            cancellationToken.ThrowIfCancellationRequested();
            CheckVerifiedWriteAllowed(pattern, nativeId, requested, isDistance);

            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Never infer write permission from the channel's presence in a GET response.
                CheckVerifiedWriteAllowed(pattern, nativeId, requested, isDistance);
                int? active = await GetActivePatternAsync(cancellationToken).ConfigureAwait(false);
                if (active != 1)
                    throw new YamahaFeatureTemporarilyUnavailableException(
                        "Pattern 1 must be active for this historically verified write path.");

                var before = await ReadForWriteAsync(isDistance, cancellationToken).ConfigureAwait(false);
                YamahaSpeakerValue original = GetFrontLeft(before);
                int exponent = isDistance ? 2 : 1;
                string unit = isDistance ? "m" : "dB";
                if (original.Exponent != exponent ||
                    !string.Equals(original.Unit, unit, StringComparison.OrdinalIgnoreCase))
                    throw new YamahaValidationException(
                        "Unexpected native exponent or unit. No write was sent.");
                if (!IsVerifiedValue(original.Value, isDistance))
                    throw new YamahaValidationException(
                        "Current Front_L setting is outside the verified reference values. No write was sent.");
                if (original.Value == requested)
                    return; // The current GET already confirms the requested value.

                decimal raw = requested * (isDistance ? 100m : 10m);
                var put = isDistance
                    ? YamahaLegacyXmlCommandBuilder.VerifiedFrontLeftDistance(pattern, raw, exponent)
                    : YamahaLegacyXmlCommandBuilder.VerifiedFrontLeftLevel(pattern, raw, exponent);

                // Never retry a PUT automatically. A timeout may mean the device did apply it.
                string response = await _legacy.SendPutAsync(put, cancellationToken)
                    .ConfigureAwait(false);
                YamahaLegacyXmlResponseParser.ValidatePutResponse(response);

                var after = await ReadForWriteAsync(isDistance, cancellationToken)
                    .ConfigureAwait(false);
                var confirmed = GetFrontLeft(after);
                if (confirmed.Value != requested)
                    throw new YamahaStateVerificationException(
                        "PUT returned success, but readback did not confirm the requested Front_L value. " +
                        "Do not retry without checking the receiver state.");
                int? activeAfter = await GetActivePatternAsync(cancellationToken).ConfigureAwait(false);
                if (activeAfter != 1)
                    throw new YamahaStateVerificationException(
                        "Speaker pattern changed during write verification. Check the receiver manually.");
            }
            finally { _writeGate.Release(); }
        }

        private Task<IReadOnlyDictionary<string, YamahaSpeakerValue>> ReadForWriteAsync(
            bool isDistance, CancellationToken ct)
        {
            return isDistance ? GetDistancesAsync(1, ct) : GetLevelsAsync(1, ct);
        }

        private static YamahaSpeakerValue GetFrontLeft(
            IReadOnlyDictionary<string, YamahaSpeakerValue> values)
        {
            YamahaSpeakerValue value;
            if (!values.TryGetValue("Front_L", out value))
                throw new YamahaLegacyXmlException(
                    "Front_L is missing from the aggregate response. Current receiver state is unknown.");
            return value;
        }

        private void CheckVerifiedWriteAllowed(int pattern, string nativeId, decimal requested,
            bool isDistance)
        {
            if (!_enableVerifiedSpeakerWrites)
                throw new YamahaFeatureNotSupportedException(
                    "Verified speaker writes are disabled. Set EnableVerifiedSpeakerWrites explicitly.");
            if (_getModel() != "RX-A3060" || _getFirmware() != "2.86" ||
                _getReportedPatternCount() != 2)
                throw new YamahaFeatureNotSupportedException(
                    "Verified writes are restricted to RX-A3060 firmware 2.86 with two patterns.");
            if (pattern != 1 || nativeId != "Front_L")
                throw new YamahaFeatureNotSupportedException(
                    "Only Pattern 1 / Front_L has a verified write path. No write was sent.");
            if (!IsVerifiedValue(requested, isDistance))
                throw new YamahaValidationException(isDistance
                    ? "Only 2.95 m and 3.00 m are allowed by the verified distance scope."
                    : "Only 0.0 dB and +0.5 dB are allowed by the verified level scope.");
        }

        private static bool IsVerifiedValue(decimal value, bool isDistance)
        {
            return isDistance ? value == 2.95m || value == 3.00m
                : value == 0m || value == 0.5m;
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

        private static NotSupportedException LegacyUnavailable()
        {
            return new NotSupportedException(
                "Legacy Yamaha HTTP/XML is disabled or unavailable for this client.");
        }
    }
}
