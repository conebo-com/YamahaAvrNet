using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvrNet.Abstractions.Interfaces;
using AvrNet.Abstractions.Models;
using YamahaAvrNet.Discovery;
using YamahaAvrNet.Features.SpeakerSetup;
using YamahaAvrNet.Models;
using YamahaAvrNet.Protocols.ExtendedControl;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Independent Yamaha receiver facade. Currently supports read-only Extended
    /// Control discovery; YNCA, legacy XML and speaker writes are not yet available.
    /// </summary>
    public sealed class YamahaAvrClient : IAvrReceiver
    {
        private static readonly IReadOnlyList<AvrZoneInfo> EmptyZones =
            Array.AsReadOnly(new AvrZoneInfo[0]);

        private readonly YamahaAvrOptions _options;
        private readonly YamahaExtendedHttpClient _extendedHttp;
        private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
        private readonly object _stateSync = new object();
        private YamahaDeviceCapabilities _capabilities;
        private IReadOnlyList<AvrZoneInfo> _zones = EmptyZones;
        private AvrConnectionState _connectionState = AvrConnectionState.Disconnected;
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
            _capabilities = new YamahaDeviceCapabilities(new AvrCapability[0]);
            _extendedHttp = _options.EnableExtendedControl
                ? new YamahaExtendedHttpClient(_options) : null;
            Speakers = new YamahaSpeakerSetupController(EnsureNotDisposed);
        }

        public string Host { get { return _options.Host; } }
        public string Manufacturer { get { return "Yamaha"; } }

        public string Model
        {
            get { lock (_stateSync) return _deviceInfo == null ? null : _deviceInfo.ModelName; }
        }

        public AvrConnectionState ConnectionState
        {
            get { lock (_stateSync) return _disposed ? AvrConnectionState.Disposed : _connectionState; }
        }

        public IReadOnlyList<AvrZoneInfo> Zones
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _zones; }
        }

        public IReadOnlyList<AvrCapability> Capabilities
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

        public AvrCapability GetCapability(string featureId)
        {
            EnsureNotDisposed();
            lock (_stateSync) return _capabilities.Get(featureId);
        }

        public YamahaReceiverState GetState()
        {
            EnsureNotDisposed();
            lock (_stateSync)
                return new YamahaReceiverState(_connectionState, _lastUpdatedUtc,
                    _stale, _deviceInfo == null ? null : _deviceInfo.ModelName);
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
                SetConnectionState(AvrConnectionState.Connecting);
                try
                {
                    var info = await _extendedHttp.GetDeviceInfoAsync(cancellationToken)
                        .ConfigureAwait(false);
                    lock (_stateSync)
                    {
                        UpdateIdentity(info);
                        _connectionState = AvrConnectionState.Connected;
                        if (_features != null)
                            _capabilities = YamahaExtendedCapabilityMapper.Build(info, _features, _functionStatus);
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
                SetConnectionState(AvrConnectionState.Connecting);
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
                        SetConnectionState(AvrConnectionState.Connecting);
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
                            _connectionState = AvrConnectionState.Connected;
                            _capabilities = YamahaExtendedCapabilityMapper.Build(
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
            var zones = new List<AvrZoneInfo>();
            foreach (var zone in features.Zones)
                zones.Add(new AvrZoneInfo(zone.NativeId, zone.NativeId));

            lock (_stateSync)
            {
                _features = features;
                _zones = zones.AsReadOnly();
                _lastUpdatedUtc = features.ObservedUtc;
                _capabilities = YamahaExtendedCapabilityMapper.Build(info, features, null);
            }
            var status = await _extendedHttp.GetFuncStatusAsync(ct).ConfigureAwait(false);
            lock (_stateSync)
            {
                _functionStatus = status;
                _lastUpdatedUtc = status.ObservedUtc;
                _capabilities = YamahaExtendedCapabilityMapper.Build(info, features, status);
                _stale = false;
                _connectionState = AvrConnectionState.Connected;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureNotDisposed();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureNotDisposed();
                // HTTP is stateless. Mark snapshots stale; no persistent YNCA session exists yet.
                lock (_stateSync)
                {
                    _connectionState = AvrConnectionState.Disconnected;
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
                _functionStatus = null;
                _zones = EmptyZones;
                _capabilities = new YamahaDeviceCapabilities(new AvrCapability[0]);
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
                    ? AvrConnectionState.PartiallyConnected : AvrConnectionState.Faulted;
            }
        }

        private void SetConnectionState(AvrConnectionState value)
        {
            lock (_stateSync) _connectionState = value;
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
            lock (_stateSync) _connectionState = AvrConnectionState.Disposed;
        }
    }
}
