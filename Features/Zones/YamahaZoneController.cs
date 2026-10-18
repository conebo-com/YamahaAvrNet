using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Exceptions;
using YamahaAvrNet.Models;
using YamahaAvrNet.Protocols.Ynca;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Features.Zones
{
    /// <summary>
    /// YNCA zone facade over the receiver's one existing TCP session. Native zone
    /// and input IDs are validated against the actual Extended Control feature list.
    /// </summary>
    internal sealed class YamahaZoneController : IYamahaZones, IDisposable
    {
        private static readonly string[] ReadFields = { "PWR", "VOL", "MUTE", "INP" };
        private readonly YamahaYncaTcpSession _session;
        private readonly Func<YamahaExtendedFeatures> _features;
        private readonly Func<IReadOnlyList<YamahaZoneInfo>> _discoveredZones;
        private readonly Func<bool> _isDisposed;
        private readonly bool _allowWrites;
        private readonly Action<string, DateTimeOffset> _recordVerifiedWrite;
        private readonly object _sync = new object();
        private readonly Dictionary<string, YamahaZoneState> _states =
            new Dictionary<string, YamahaZoneState>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        internal YamahaZoneController(YamahaYncaTcpSession session,
            Func<YamahaExtendedFeatures> features,
            Func<IReadOnlyList<YamahaZoneInfo>> discoveredZones,
            Func<bool> isDisposed, bool allowWrites,
            Action<string, DateTimeOffset> recordVerifiedWrite)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _features = features ?? throw new ArgumentNullException(nameof(features));
            _discoveredZones = discoveredZones ?? throw new ArgumentNullException(nameof(discoveredZones));
            _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
            _allowWrites = allowWrites;
            _recordVerifiedWrite = recordVerifiedWrite ??
                throw new ArgumentNullException(nameof(recordVerifiedWrite));
            _session.MessageReceived += SessionOnMessageReceived;
            _session.ConnectionChanged += SessionOnConnectionChanged;
        }

        public event EventHandler<YamahaZoneStateChangedEventArgs> ZoneStateChanged;

        public IReadOnlyDictionary<string, YamahaZoneState> GetStates()
        {
            EnsureAvailable();
            lock (_sync)
                return new ReadOnlyDictionary<string, YamahaZoneState>(
                    new Dictionary<string, YamahaZoneState>(_states, StringComparer.OrdinalIgnoreCase));
        }

        public async Task<YamahaZoneState> RefreshAsync(string nativeZoneId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string subunit = RequireZone(nativeZoneId).ToUpperInvariant();
            foreach (string field in ReadFields)
            {
                try
                {
                    await _session.GetAsync(subunit, field, cancellationToken).ConfigureAwait(false);
                }
                catch (YamahaFeatureNotSupportedException)
                {
                    // Do not infer that other functions or the whole zone are unsupported.
                }
                catch (YamahaFeatureTemporarilyUnavailableException)
                {
                    // A sleeping or unconfigured zone can reject an individual GET.
                }
            }
            lock (_sync)
            {
                YamahaZoneState result;
                return _states.TryGetValue(subunit, out result)
                    ? result : new YamahaZoneState(subunit, null, null, null, null, null, !_session.IsConnected);
            }
        }

        public Task SetPowerAsync(string nativeZoneId, bool on,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return WriteAsync(nativeZoneId, "PWR", on ? "On" : "Standby", cancellationToken);
        }

        public Task SetMuteAsync(string nativeZoneId, bool mute,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return WriteAsync(nativeZoneId, "MUTE", mute ? "On" : "Off", cancellationToken);
        }

        public Task SetVolumeDbAsync(string nativeZoneId, decimal decibels,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var zone = RequireZoneFeature(nativeZoneId);
            if (!_allowWrites)
                throw new YamahaValidationException("YNCA writes are disabled. Set EnableYncaZoneWrites explicitly.");
            YamahaExtendedNumericRange range;
            if (!zone.Ranges.TryGetValue("actual_volume_db", out range) ||
                !range.Minimum.HasValue || !range.Maximum.HasValue || !range.Step.HasValue ||
                range.Step.Value <= 0m)
                throw new YamahaValidationException("Device-reported dB range/step is unavailable for this zone.");
            if (decibels < range.Minimum.Value || decibels > range.Maximum.Value ||
                (decibels - range.Minimum.Value) % range.Step.Value != 0m)
                throw new YamahaValidationException("Requested dB volume is outside the reported range or step.");
            return WriteAsync(nativeZoneId, "VOL", decibels.ToString("0.0################", CultureInfo.InvariantCulture),
                cancellationToken);
        }

        public Task SetInputAsync(string nativeZoneId, string inputNativeId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var zone = RequireZoneFeature(nativeZoneId);
            if (!_allowWrites)
                throw new YamahaValidationException("YNCA writes are disabled. Set EnableYncaZoneWrites explicitly.");
            if (string.IsNullOrWhiteSpace(inputNativeId))
                throw new YamahaValidationException("An input ID is required.");
            string native = zone.Inputs.FirstOrDefault(x =>
                string.Equals(x, inputNativeId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (native == null)
                throw new YamahaValidationException("Input is not reported for this zone.");
            // The Extended and YNCA input naming schemes are not always identical.
            // Only simple identical IDs (e.g. AUDIO1, HDMI1) are safe without a mapping profile.
            foreach (char c in native)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9')))
                    throw new YamahaValidationException("This input requires a verified Extended-to-YNCA mapping.");
            return WriteAsync(nativeZoneId, "INP", native.ToUpperInvariant(), cancellationToken);
        }

        private async Task WriteAsync(string zoneId, string field, string requested, CancellationToken ct)
        {
            // Write validation needs reported Extended Control metadata; a YNCA-only
            // zone read does not authorize writing on an unprofiled receiver.
            RequireZoneFeature(zoneId);
            string subunit = RequireZone(zoneId).ToUpperInvariant();
            if (!_allowWrites)
                throw new YamahaValidationException("YNCA writes are disabled. Set EnableYncaZoneWrites explicitly.");
            YamahaYncaMessage observed = await _session.PutAndReadBackAsync(subunit, field, requested, ct)
                .ConfigureAwait(false);
            if (!string.Equals(observed.Value, requested, StringComparison.OrdinalIgnoreCase))
                throw new YamahaStateVerificationException("YNCA readback differs from the requested " +
                    field + " value. Requested: " + requested + ", observed: " + observed.Value +
                    ". Do not retry blindly.");
            // A matching GET observation cannot exclude an identical unsolicited update.
            // Evidence applies only to this receiver instance, never to every Yamaha.
            string id = YamahaAvrNet.Discovery.YamahaFunctionRouteMapper.YncaFunctionId(subunit, field);
            if (id != null) _recordVerifiedWrite("YNCA:" + id, observed.ReceivedUtc);
        }

        private string RequireZone(string id)
        {
            EnsureAvailable();
            if (string.IsNullOrWhiteSpace(id))
                throw new YamahaValidationException("Zone ID is required.");
            string native = id.Trim();
            if (!string.Equals(native, "main", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(native, "zone2", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(native, "zone3", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(native, "zone4", StringComparison.OrdinalIgnoreCase))
                throw new YamahaFeatureNotSupportedException("No known YNCA mapping for zone " + native + ".");
            foreach (var zone in _discoveredZones())
                if (string.Equals(zone.NativeId, native, StringComparison.OrdinalIgnoreCase))
                    return zone.NativeId;
            throw new YamahaFeatureNotSupportedException(
                "No successful discovery observation for zone " + native + ".");
        }

        private YamahaExtendedZoneFeatures RequireZoneFeature(string zoneId)
        {
            EnsureAvailable();
            if (string.IsNullOrWhiteSpace(zoneId))
                throw new YamahaValidationException("Zone ID is required.");
            // No arbitrary subunit writes: use the four known YNCA zone identifiers,
            // restricted to those actually returned by this receiver.
            string native = zoneId.Trim();
            if (!string.Equals(native, "main", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(native, "zone2", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(native, "zone3", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(native, "zone4", StringComparison.OrdinalIgnoreCase))
                throw new YamahaFeatureNotSupportedException("No confirmed YNCA mapping for zone " + native + ".");
            var discovered = _features();
            if (discovered == null)
                throw new YamahaValidationException("Discover receiver features before using the typed zone API.");
            var zone = discovered.Zones.FirstOrDefault(x =>
                string.Equals(x.NativeId, native, StringComparison.OrdinalIgnoreCase));
            if (zone == null)
                throw new YamahaFeatureNotSupportedException("Zone is not listed by this receiver: " + native);
            return zone;
        }

        private void SessionOnMessageReceived(object sender, YamahaYncaMessageEventArgs e)
        {
            var msg = e.Message;
            if (_disposed || msg.Kind != YamahaYncaMessageKind.Value ||
                string.IsNullOrEmpty(msg.Subunit) || string.IsNullOrEmpty(msg.Function)) return;
            string zoneId = msg.Subunit.ToUpperInvariant();
            if (zoneId != "MAIN" && zoneId != "ZONE2" && zoneId != "ZONE3" && zoneId != "ZONE4") return;
            YamahaZoneState result;
            lock (_sync)
            {
                YamahaZoneState old;
                _states.TryGetValue(zoneId, out old);
                bool? power = old == null ? null : old.IsPoweredOn;
                decimal? volume = old == null ? null : old.VolumeDb;
                bool? mute = old == null ? null : old.IsMuted;
                string input = old == null ? null : old.InputNativeId;
                string value = msg.Value;
                switch (msg.Function.ToUpperInvariant())
                {
                    case "PWR":
                        if (string.Equals(value, "On", StringComparison.OrdinalIgnoreCase)) power = true;
                        else if (string.Equals(value, "Standby", StringComparison.OrdinalIgnoreCase)) power = false;
                        else return;
                        break;
                    case "VOL":
                        decimal parsed;
                        if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign |
                            NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out parsed)) return;
                        volume = parsed;
                        break;
                    case "MUTE":
                        if (string.Equals(value, "On", StringComparison.OrdinalIgnoreCase)) mute = true;
                        else if (string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase)) mute = false;
                        else return; // e.g. Att -20 dB is not a boolean mute state.
                        break;
                    case "INP": input = value; break;
                    default: return;
                }
                bool changed = old == null || old.IsStale || old.IsPoweredOn != power ||
                    old.VolumeDb != volume || old.IsMuted != mute ||
                    !string.Equals(old.InputNativeId, input, StringComparison.Ordinal);
                result = new YamahaZoneState(zoneId, power, volume, mute, input, msg.ReceivedUtc, false);
                _states[zoneId] = result;
                if (!changed) return;
            }
            RaiseStateChanged(result);
        }

        private void SessionOnConnectionChanged(object sender, YamahaYncaConnectionEventArgs e)
        {
            if (_disposed || e.IsConnected) return;
            List<YamahaZoneState> stale = new List<YamahaZoneState>();
            lock (_sync)
            {
                foreach (var pair in _states.ToList())
                {
                    if (pair.Value.IsStale) continue;
                    var current = pair.Value;
                    var next = new YamahaZoneState(current.NativeId, current.IsPoweredOn,
                        current.VolumeDb, current.IsMuted, current.InputNativeId, current.ObservedUtc, true);
                    _states[pair.Key] = next;
                    stale.Add(next);
                }
            }
            foreach (var zone in stale) RaiseStateChanged(zone);
        }

        private void RaiseStateChanged(YamahaZoneState state)
        {
            var handler = ZoneStateChanged;
            if (handler == null) return;
            foreach (EventHandler<YamahaZoneStateChangedEventArgs> subscriber in handler.GetInvocationList())
                try { subscriber(this, new YamahaZoneStateChangedEventArgs(state)); }
                catch { /* User event handlers must not block the TCP reader. */ }
        }

        private void EnsureAvailable()
        {
            if (_disposed || _isDisposed())
                throw new ObjectDisposedException(nameof(YamahaZoneController));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _session.MessageReceived -= SessionOnMessageReceived;
            _session.ConnectionChanged -= SessionOnConnectionChanged;
        }
    }
}
