using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Discovery;
using YamahaAvrNet.Features.SpeakerSetup;
using YamahaAvrNet.Features.Zones;
using YamahaAvrNet.Models;
using YamahaAvrNet.Protocols.ExtendedControl;
using YamahaAvrNet.Protocols.LegacyXml;
using YamahaAvrNet.Protocols.Ynca;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Independent Yamaha receiver facade. Supports Extended Control discovery
    /// and guarded, explicitly opted-in legacy XML writes for verified RX-A3060 parameters.
    /// </summary>
    public sealed class YamahaAvrClient : IDisposable
    {
        private static readonly IReadOnlyList<YamahaZoneInfo> EmptyZones =
            Array.AsReadOnly(new YamahaZoneInfo[0]);

        private readonly YamahaAvrOptions _options;
        private readonly YamahaExtendedHttpClient _extendedHttp;
        private readonly YamahaLegacyXmlHttpClient _legacyXml;
        private readonly YamahaYncaTcpSession _yncaSession;
        private readonly YamahaZoneController _zoneController;
        private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
        private readonly object _stateSync = new object();
        private YamahaDeviceCapabilities _capabilities;
        private readonly Dictionary<string, DateTimeOffset> _speakerVerifiedReads =
            new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<YamahaZoneInfo> _zones = EmptyZones;
        private YamahaConnectionState _connectionState = YamahaConnectionState.Disconnected;
        private YamahaExtendedDeviceInfo _deviceInfo;
        private YamahaExtendedFeatures _features;
        private YamahaExtendedFunctionStatus _functionStatus;
        private DateTimeOffset? _lastUpdatedUtc;
        private bool _stale = true;
        private volatile bool _disposed;

        public YamahaAvrClient(YamahaAvrOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            _options = options.CopyValidated();
            _capabilities = new YamahaDeviceCapabilities(new YamahaCapability[0]);
            _extendedHttp = _options.EnableExtendedControl
                ? new YamahaExtendedHttpClient(_options) : null;
            _legacyXml = _options.EnableLegacyXml
                ? new YamahaLegacyXmlHttpClient(_options) : null;
            _yncaSession = _options.EnableYnca
                ? new YamahaYncaTcpSession(_options.Host, _options.YncaPort, _options.RequestTimeout) : null;
            Ynca = _yncaSession == null ? null : new YamahaYncaController(_yncaSession);
            _zoneController = _yncaSession == null ? null : new YamahaZoneController(
                _yncaSession, () => DeviceFeatures, () => _disposed, _options.EnableYncaZoneWrites);
            ZoneControl = _zoneController;
            Speakers = new YamahaSpeakerSetupController(EnsureNotDisposed, _legacyXml,
                GetReportedSpeakerPatternCount, GetExtendedActivePatternAsync,
                RecordSpeakerRead, () => Model,
                () => DeviceInfo == null ? null : DeviceInfo.SystemVersion,
                _options.EnableVerifiedSpeakerWrites);
        }

        public string Host { get { return _options.Host; } }
        public string Manufacturer { get { return "Yamaha"; } }

        public string Model
        {
            get { lock (_stateSync) return _deviceInfo == null ? null : _deviceInfo.ModelName; }
        }

        public YamahaConnectionState ConnectionState
        {
            get { lock (_stateSync) return _disposed ? YamahaConnectionState.Disposed : _connectionState; }
        }

        public IReadOnlyList<YamahaZoneInfo> Zones
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _zones; }
        }

        public IReadOnlyList<YamahaCapability> Capabilities
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _capabilities.All; }
        }

        public YamahaDeviceCapabilities YamahaCapabilities
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _capabilities; }
        }

        /// <summary>Null when Extended Control is disabled in the options.</summary>
        public IYamahaExtendedControl ExtendedControl { get { EnsureNotDisposed(); return _extendedHttp; } }
        public IYamahaSpeakerSetup Speakers { get; private set; }

        /// <summary>Optional shared YNCA TCP session. Connecting it never writes AVR settings.</summary>
        public IYamahaYnca Ynca { get; private set; }
        /// <summary>Typed zone controls using the same shared YNCA session.</summary>
        public IYamahaZones ZoneControl { get; private set; }

        public YamahaExtendedDeviceInfo DeviceInfo
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _deviceInfo; }
        }

        public YamahaExtendedFeatures DeviceFeatures
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _features; }
        }

        public YamahaExtendedFunctionStatus FunctionStatus
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _functionStatus; }
        }

        public YamahaCapability GetCapability(string featureId)
        {
            EnsureNotDisposed();
            lock (_stateSync) return _capabilities.Get(featureId);
        }

        public YamahaReceiverState GetState()
        {
            EnsureNotDisposed();
            lock (_stateSync)
                return new YamahaReceiverState(_connectionState, _lastUpdatedUtc,
                    _stale, _deviceInfo == null ? null : _deviceInfo.ModelName,
                    _zoneController == null ? null : _zoneController.GetStates());
        }

        /// <summary>
        /// Checks Extended Control reachability via getDeviceInfo. HTTP has no
        /// persistent session, so Connected means the last device-info GET worked.
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureExtendedAvailable();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureNotDisposed();
                SetConnectionState(YamahaConnectionState.Connecting);
                try
                {
                    var info = await _extendedHttp.GetDeviceInfoAsync(cancellationToken)
                        .ConfigureAwait(false);
                    lock (_stateSync)
                    {
                        UpdateIdentity(info);
                        _connectionState = YamahaConnectionState.Connected;
                        if (_features != null)
                            _capabilities = BuildCapabilities(info, _features, _functionStatus);
                    }
                }
                catch
                {
                    MarkFailure();
                    throw;
                }
            }
            finally { _lifecycleGate.Release(); }
        }

        /// <summary>
        /// Reads device identity, reported features and system function status.
        /// Discovery makes GET requests only and never switches a speaker pattern.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureExtendedAvailable();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureNotDisposed();
                SetConnectionState(YamahaConnectionState.Connecting);
                try
                {
                    await ReadInitializationAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    MarkFailure();
                    throw;
                }
            }
            finally { _lifecycleGate.Release(); }
        }

        /// <summary>Refreshes getFuncStatus; initializes when no features are cached.</summary>
        public async Task RefreshAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureExtendedAvailable();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureNotDisposed();
                try
                {
                    if (_features == null || _deviceInfo == null)
                    {
                        SetConnectionState(YamahaConnectionState.Connecting);
                        await ReadInitializationAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var status = await _extendedHttp.GetFuncStatusAsync(cancellationToken)
                            .ConfigureAwait(false);
                        lock (_stateSync)
                        {
                            _functionStatus = status;
                            _lastUpdatedUtc = status.ObservedUtc;
                            _stale = false;
                            _connectionState = YamahaConnectionState.Connected;
                            _capabilities = BuildCapabilities(
                                _deviceInfo, _features, status);
                        }
                    }
                }
                catch
                {
                    MarkFailure();
                    throw;
                }
            }
            finally { _lifecycleGate.Release(); }
        }

        private async Task ReadInitializationAsync(CancellationToken ct)
        {
            var info = await _extendedHttp.GetDeviceInfoAsync(ct).ConfigureAwait(false);
            lock (_stateSync) UpdateIdentity(info);
            var features = await _extendedHttp.GetFeaturesAsync(ct).ConfigureAwait(false);
            var zones = new List<YamahaZoneInfo>();
            foreach (var zone in features.Zones)
                zones.Add(new YamahaZoneInfo(zone.NativeId, zone.NativeId));

            lock (_stateSync)
            {
                _features = features;
                _zones = zones.AsReadOnly();
                _lastUpdatedUtc = features.ObservedUtc;
                _capabilities = BuildCapabilities(info, features, null);
            }
            var status = await _extendedHttp.GetFuncStatusAsync(ct).ConfigureAwait(false);
            lock (_stateSync)
            {
                _functionStatus = status;
                _lastUpdatedUtc = status.ObservedUtc;
                _capabilities = BuildCapabilities(info, features, status);
                _stale = false;
                _connectionState = YamahaConnectionState.Connected;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureNotDisposed();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureNotDisposed();
                // HTTP is stateless; close the separate persistent YNCA session.
                if (_yncaSession != null)
                    _yncaSession.Disconnect();
                lock (_stateSync)
                {
                    _connectionState = YamahaConnectionState.Disconnected;
                    _stale = true;
                }
            }
            finally { _lifecycleGate.Release(); }
        }

        /// <summary>Invalidates firmware-bound discovery data when receiver identity changes.</summary>
        private void UpdateIdentity(YamahaExtendedDeviceInfo info)
        {
            if (_deviceInfo != null &&
                (!string.Equals(_deviceInfo.ModelName, info.ModelName, StringComparison.Ordinal) ||
                 !string.Equals(_deviceInfo.SystemVersion, info.SystemVersion, StringComparison.Ordinal) ||
                 !string.Equals(_deviceInfo.ApiVersion, info.ApiVersion, StringComparison.Ordinal)))
            {
                _features = null;
                _speakerVerifiedReads.Clear();
                _functionStatus = null;
                _zones = EmptyZones;
                _capabilities = new YamahaDeviceCapabilities(new YamahaCapability[0]);
            }
            _deviceInfo = info;
            _lastUpdatedUtc = info.ObservedUtc;
            _stale = true;
        }

        private void MarkFailure()
        {
            lock (_stateSync)
            {
                _stale = true;
                _connectionState = _deviceInfo != null
                    ? YamahaConnectionState.PartiallyConnected : YamahaConnectionState.Faulted;
            }
        }

        private void SetConnectionState(YamahaConnectionState value)
        {
            lock (_stateSync) _connectionState = value;
        }

        /// <summary>Retains successful instance-specific XML reads across Extended refreshes.</summary>
        private YamahaDeviceCapabilities BuildCapabilities(YamahaExtendedDeviceInfo device,
            YamahaExtendedFeatures features, YamahaExtendedFunctionStatus status)
        {
            var known = new List<YamahaCapability>(
                YamahaExtendedCapabilityMapper.Build(device, features, status).All);
            foreach (var pair in _speakerVerifiedReads)
                known.Add(new YamahaCapability(pair.Key,
                    YamahaCapabilityStatus.ReadVerified, YamahaEvidenceSource.ReadProbe,
                    canRead: true, protocol: "LegacyXml",
                    model: device == null ? null : device.ModelName,
                    firmware: device == null ? null : device.SystemVersion,
                    lastVerifiedUtc: pair.Value));
            return new YamahaDeviceCapabilities(known);
        }

        private void RecordSpeakerRead(string featureId, DateTimeOffset observedUtc)
        {
            lock (_stateSync)
            {
                _speakerVerifiedReads[featureId] = observedUtc;
                _capabilities = BuildCapabilities(_deviceInfo, _features, _functionStatus);
            }
        }

        private int? GetReportedSpeakerPatternCount()
        {
            lock (_stateSync)
                return _features == null ? (int?)null : _features.SpeakerPatternCount;
        }

        private async Task<int?> GetExtendedActivePatternAsync(CancellationToken ct)
        {
            if (_extendedHttp == null)
                throw new NotSupportedException("Extended Control is disabled.");
            var status = await _extendedHttp.GetFuncStatusAsync(ct).ConfigureAwait(false);
            return status.ActiveSpeakerPattern;
        }

        private void EnsureExtendedAvailable()
        {
            EnsureNotDisposed();
            if (_extendedHttp == null)
                throw new NotSupportedException(
                    "Extended Control is disabled. No other network protocol is implemented yet.");
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(YamahaAvrClient));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_extendedHttp != null)
                _extendedHttp.Dispose();
            if (_legacyXml != null)
                _legacyXml.Dispose();
            if (_zoneController != null)
                _zoneController.Dispose();
            if (_yncaSession != null)
                _yncaSession.Dispose();
            lock (_stateSync) _connectionState = YamahaConnectionState.Disposed;
        }
    }
}
