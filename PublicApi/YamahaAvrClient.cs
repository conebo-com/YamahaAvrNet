using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Discovery;
using YamahaAvrNet.Exceptions;
using YamahaAvrNet.Features.SpeakerSetup;
using YamahaAvrNet.Features.Audio;
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
        private readonly YamahaLegacyDescriptorProbe _descriptorProbe;
        private readonly YamahaYncaTcpSession _yncaSession;
        private readonly YamahaZoneController _zoneController;
        private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
        private readonly object _stateSync = new object();
        private YamahaDeviceCapabilities _capabilities;
        private readonly Dictionary<string, DateTimeOffset> _speakerVerifiedReads =
            new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<YamahaZoneInfo> _zones = EmptyZones;
        private string _yncaModel;
        private int? _legacyActivePattern;
        private YamahaLegacyDescriptor _legacyDescriptor;
        private YamahaLegacyDescriptorDiagnostic _legacyDescriptorDiagnostic;
        private readonly Dictionary<string, DateTimeOffset> _extendedVerifiedReads =
            new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _verifiedWrites =
            new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _acceptedActions =
            new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<YamahaFunctionRoute> _functionRoutes =
            Array.AsReadOnly(new YamahaFunctionRoute[0]);
        private readonly Dictionary<string, YamahaProtocolProbe> _protocols =
            new Dictionary<string, YamahaProtocolProbe>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _yncaVerifiedReads =
            new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
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
            _descriptorProbe = _options.EnableLegacyXml
                ? new YamahaLegacyDescriptorProbe(_options) : null;
            _yncaSession = _options.EnableYnca
                ? new YamahaYncaTcpSession(_options.Host, _options.YncaPort, _options.RequestTimeout) : null;
            if (_yncaSession != null)
                _yncaSession.ReadSucceeded += RecordYncaRead;
            Ynca = _yncaSession == null ? null : new YamahaYncaController(_yncaSession);
            _zoneController = _yncaSession == null ? null : new YamahaZoneController(
                _yncaSession, () => DeviceFeatures, () => Zones,
                () => _disposed, _options.EnableYncaZoneWrites, RecordVerifiedWrite);
            ZoneControl = _zoneController;
            // The native YNCA DSP reader also works on receivers without Extended Control.
            Sound = _extendedHttp == null && _yncaSession == null ? null : new YamahaSoundController(
                _extendedHttp, _yncaSession, () => DeviceFeatures,
                () => _disposed, _options.EnableSoundSceneWrites,
                RecordExtendedRead, RecordVerifiedWrite, RecordAcceptedAction);
            SetInitialProtocolStates();
            Speakers = new YamahaSpeakerSetupController(EnsureNotDisposed, _legacyXml,
                GetReportedSpeakerPatternCount,
                _extendedHttp == null ? null : (Func<CancellationToken, Task<int?>>)GetExtendedActivePatternAsync,
                RecordSpeakerRead, RecordVerifiedWrite, () => Model,
                () => DeviceInfo == null ? null : DeviceInfo.SystemVersion,
                _options.EnableVerifiedSpeakerWrites);
        }

        public string Host { get { return _options.Host; } }
        public string Manufacturer { get { return "Yamaha"; } }

        public string Model
        {
            get { lock (_stateSync) return _deviceInfo == null ? _yncaModel : _deviceInfo.ModelName; }
        }

        public YamahaConnectionState ConnectionState
        {
            get { lock (_stateSync) return _disposed ? YamahaConnectionState.Disposed : _connectionState; }
        }

        /// <summary>Per-protocol discovery results; unavailable never means unsupported.</summary>
        public IReadOnlyDictionary<string, YamahaProtocolProbe> Protocols
        {
            get
            {
                EnsureNotDisposed();
                lock (_stateSync)
                    return new System.Collections.ObjectModel.ReadOnlyDictionary<string, YamahaProtocolProbe>(
                        new Dictionary<string, YamahaProtocolProbe>(_protocols, StringComparer.OrdinalIgnoreCase));
            }
        }

        public YamahaProtocolProbe GetProtocol(string protocol)
        {
            if (string.IsNullOrWhiteSpace(protocol))
                throw new ArgumentException("Protocol name must not be empty.", nameof(protocol));
            EnsureNotDisposed();
            lock (_stateSync)
            {
                YamahaProtocolProbe result;
                return _protocols.TryGetValue(protocol, out result) ? result :
                    new YamahaProtocolProbe(protocol, YamahaProtocolAvailability.NotChecked, null, null);
            }
        }

        public IReadOnlyList<YamahaZoneInfo> Zones
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _zones; }
        }

        /// <summary>
        /// Optional, incomplete legacy XML device description; null when unavailable.
        /// Listed entries are never treated as proof of writable commands.
        /// </summary>
        public YamahaLegacyDescriptor LegacyDescriptor
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _legacyDescriptor; }
        }

        /// <summary>
        /// Receiver-advertised native function identifiers and YNCA read observations,
        /// grouped by protocol. Routes are descriptive, not executable commands.
        /// </summary>
        public YamahaLegacyDescriptorDiagnostic LegacyDescriptorDiagnostic
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _legacyDescriptorDiagnostic; }
        }

        public IReadOnlyList<YamahaFunctionRoute> FunctionRoutes
        {
            get { EnsureNotDisposed(); lock (_stateSync) return _functionRoutes; }
        }

        /// <summary>
        /// Chooses a deterministic already-verified route for informational routing.
        /// It NEVER sends a command or grants permission to bypass a controller's
        /// device-specific checks or explicit write opt-in.
        /// </summary>
        public YamahaFunctionRoute GetPreferredVerifiedRoute(string functionId,
            YamahaFunctionOperation operation)
        {
            if (string.IsNullOrWhiteSpace(functionId))
                throw new ArgumentException("A function ID is required.", nameof(functionId));
            EnsureNotDisposed();
            lock (_stateSync)
            {
                YamahaFunctionRoute best = null;
                foreach (var candidate in _functionRoutes)
                {
                    if (!string.Equals(candidate.FunctionId, functionId,
                        StringComparison.OrdinalIgnoreCase) || !candidate.IsVerifiedFor(operation))
                        continue;
                    if (best == null || RoutePriority(candidate, operation) < RoutePriority(best, operation))
                        best = candidate;
                }
                return best;
            }
        }

        private static int RoutePriority(YamahaFunctionRoute route, YamahaFunctionOperation operation)
        {
            // For reads, stateless HTTP is preferred when equally verified. For
            // writes/execute, availability of verified evidence is the only filter.
            if (string.Equals(route.Protocol, "ExtendedControl", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(route.Protocol, "LegacyXml", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
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
        /// <summary>Sound, DSP and scene operations backed by advertised Extended Control features.</summary>
        public IYamahaSound Sound { get; private set; }

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
                    _stale, _deviceInfo == null ? _yncaModel : _deviceInfo.ModelName,
                    _zoneController == null ? null : _zoneController.GetStates());
        }

        /// <summary>
        /// Runs read-only discovery against each enabled protocol. At least one
        /// successful protocol suffices; a failed endpoint does not hide working ones.
        /// YNCA is disconnected after probing so it does not monopolize port 50000.
        /// </summary>
        public Task ConnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return InitializeAsync(cancellationToken);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureNotDisposed();
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

        /// <summary>Refresh Extended Control function status when available; otherwise rediscover.</summary>
        public async Task RefreshAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureNotDisposed();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureNotDisposed();
                if (_extendedHttp != null && _features != null && _deviceInfo != null)
                {
                    try
                    {
                        var status = await _extendedHttp.GetFuncStatusAsync(cancellationToken)
                            .ConfigureAwait(false);
                        lock (_stateSync)
                        {
                            _functionStatus = status;
                            _lastUpdatedUtc = status.ObservedUtc;
                            _stale = false;
                            _connectionState = YamahaConnectionState.Connected;
                            _capabilities = BuildCapabilities(_deviceInfo, _features, status);
                        }
                    }
                    catch
                    {
                        MarkFailure();
                        throw;
                    }
                }
                else
                {
                    SetConnectionState(YamahaConnectionState.Connecting);
                    try { await ReadInitializationAsync(cancellationToken).ConfigureAwait(false); }
                    catch { MarkFailure(); throw; }
                }
            }
            finally { _lifecycleGate.Release(); }
        }

        private void SetInitialProtocolStates()
        {
            lock (_stateSync)
            {
                SetProbe("ExtendedControl", _extendedHttp == null ?
                    YamahaProtocolAvailability.Disabled : YamahaProtocolAvailability.NotChecked, null);
                SetProbe("LegacyXml", _legacyXml == null ?
                    YamahaProtocolAvailability.Disabled : YamahaProtocolAvailability.NotChecked, null);
                SetProbe("YNCA", _yncaSession == null ?
                    YamahaProtocolAvailability.Disabled : YamahaProtocolAvailability.NotChecked, null);
            }
        }

        // Caller must hold _stateSync.
        private void SetProbe(string protocol, YamahaProtocolAvailability availability, string detail)
        {
            _protocols[protocol] = new YamahaProtocolProbe(protocol, availability, detail,
                availability == YamahaProtocolAvailability.Disabled ||
                availability == YamahaProtocolAvailability.NotChecked ?
                (DateTimeOffset?)null : DateTimeOffset.UtcNow);
        }

        private async Task ReadInitializationAsync(CancellationToken ct)
        {
            // Clear instance-bound discovery before accepting evidence from this run.
            lock (_stateSync)
            {
                _deviceInfo = null;
                _features = null;
                _functionStatus = null;
                _yncaModel = null;
                _legacyActivePattern = null;
                _legacyDescriptor = null;
                _legacyDescriptorDiagnostic = null;
                _extendedVerifiedReads.Clear();
                _verifiedWrites.Clear();
                _acceptedActions.Clear();
                _functionRoutes = Array.AsReadOnly(new YamahaFunctionRoute[0]);
                _lastUpdatedUtc = null;
                _zones = EmptyZones;
                _speakerVerifiedReads.Clear();
                _yncaVerifiedReads.Clear();
                _capabilities = new YamahaDeviceCapabilities(new YamahaCapability[0]);
                SetInitialProtocolStates();
            }
            int success = 0;
            int failures = 0;
            var diagnostics = new List<string>();

            if (_extendedHttp != null)
            {
                try
                {
                    var info = await _extendedHttp.GetDeviceInfoAsync(ct).ConfigureAwait(false);
                    lock (_stateSync) UpdateIdentity(info);
                    success++;
                    var partial = new List<string>();
                    try
                    {
                        var features = await _extendedHttp.GetFeaturesAsync(ct).ConfigureAwait(false);
                        lock (_stateSync)
                        {
                            _features = features;
                            var zones = new List<YamahaZoneInfo>();
                            foreach (var zone in features.Zones)
                                zones.Add(new YamahaZoneInfo(zone.NativeId, zone.NativeId));
                            _zones = zones.AsReadOnly();
                            _lastUpdatedUtc = features.ObservedUtc;
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex) { partial.Add("getFeatures: " + ex.Message); }
                    try
                    {
                        var status = await _extendedHttp.GetFuncStatusAsync(ct).ConfigureAwait(false);
                        lock (_stateSync)
                        {
                            _functionStatus = status;
                            _lastUpdatedUtc = status.ObservedUtc;
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex) { partial.Add("getFuncStatus: " + ex.Message); }
                    lock (_stateSync) SetProbe("ExtendedControl", YamahaProtocolAvailability.Available,
                        partial.Count == 0 ? "Device info, features and function status read." :
                        "Device info available; " + string.Join("; ", partial));
                    if (partial.Count != 0) { failures++; diagnostics.AddRange(partial); }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    failures++;
                    diagnostics.Add("ExtendedControl: " + ex.Message);
                    lock (_stateSync) SetProbe("ExtendedControl", YamahaProtocolAvailability.Unavailable, ex.Message);
                }
            }

            // Legacy XML can be present on a receiver without Extended Control.
            // Probe only the already implemented read-only Pattern_Sel GET.
            if (_legacyXml != null)
            {
                try
                {
                    string xml = await _legacyXml.SendGetAsync(
                        YamahaLegacyXmlCommandBuilder.ActivePattern(), ct).ConfigureAwait(false);
                    int? pattern = YamahaLegacyXmlResponseParser.ParseActivePattern(xml);
                    lock (_stateSync)
                    {
                        _legacyActivePattern = pattern;
                        SetProbe("LegacyXml", YamahaProtocolAvailability.Available,
                            pattern.HasValue ? "Active speaker pattern read." : "XML GET succeeded; pattern unknown.");
                    }
                    success++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    failures++;
                    diagnostics.Add("LegacyXml: " + ex.Message);
                    lock (_stateSync) SetProbe("LegacyXml", YamahaProtocolAvailability.Unavailable, ex.Message);
                }
            }

            // Optional description is useful for diagnostics and older models,
            // but it is incomplete and independent from the working ctrl endpoint.
            if (_descriptorProbe != null)
            {
                try
                {
                    var result = await _descriptorProbe.ProbeAsync(ct).ConfigureAwait(false);
                    lock (_stateSync)
                    {
                        _legacyDescriptor = result.Descriptor;
                        _legacyDescriptorDiagnostic = result.Diagnostic;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // Not a protocol failure: desc.xml is optional and often incomplete.
                }
            }

            if (_yncaSession != null)
            {
                bool originallyConnected = _yncaSession.IsConnected;
                try
                {
                    // One native model query verifies YNCA read capability. Lack of this
                    // particular function does NOT prove the protocol unsupported.
                    await _yncaSession.ConnectAsync(ct).ConfigureAwait(false);
                    bool verifiedYnca = false;
                    string note = "TCP connection established; YNCA commands not yet verified.";
                    try
                    {
                        var model = await _yncaSession.GetAsync("SYS", "MODELNAME", ct)
                            .ConfigureAwait(false);
                        lock (_stateSync)
                        {
                            _yncaModel = model.Value;
                            _yncaVerifiedReads["ynca.sys.modelname"] = model.ReceivedUtc;
                        }
                        verifiedYnca = true;
                        note = "SYS:MODELNAME read.";
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (YamahaFeatureNotSupportedException)
                    {
                        verifiedYnca = true; // Native @UNDEFINED is itself a YNCA response.
                        note = "YNCA answered @UNDEFINED to SYS:MODELNAME.";
                    }
                    catch (YamahaFeatureTemporarilyUnavailableException)
                    {
                        verifiedYnca = true; // Native @RESTRICTED is itself a YNCA response.
                        note = "YNCA answered @RESTRICTED to SYS:MODELNAME.";
                    }
                    catch (Exception ex)
                    {
                        note += " SYS:MODELNAME unavailable: " + ex.Message;
                    }
                    // Observe native zone power on read-only YNCA. When Extended
                    // Control listed zones, only probe those with known native
                    // YNCA subunit names. Without it, use the bounded candidates.
                    // A failed PWR request never removes a reported zone.
                    var candidates = new List<string>();
                    if (_features != null)
                    {
                        foreach (var zone in _features.Zones)
                        {
                            string id = zone.NativeId.ToUpperInvariant();
                            if (id == "MAIN" || id == "ZONE2" ||
                                id == "ZONE3" || id == "ZONE4")
                                candidates.Add(id);
                        }
                    }
                    else candidates.AddRange(new[] { "MAIN", "ZONE2", "ZONE3", "ZONE4" });
                    var found = new List<YamahaZoneInfo>();
                    foreach (string zone in candidates)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var power = await _yncaSession.GetAsync(zone, "PWR", ct)
                                .ConfigureAwait(false);
                            verifiedYnca = true;
                            if (_features == null)
                                found.Add(new YamahaZoneInfo(zone.ToLowerInvariant(), zone.ToLowerInvariant()));
                            lock (_stateSync)
                                _yncaVerifiedReads["ynca." + zone.ToLowerInvariant() + ".pwr"] = power.ReceivedUtc;
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (YamahaFeatureNotSupportedException) { verifiedYnca = true; }
                        catch (YamahaFeatureTemporarilyUnavailableException) { verifiedYnca = true; }
                        catch (Exception)
                        {
                            // Not receiving PWR does not prove the zone absent.
                        }
                    }
                    if (_features == null)
                        lock (_stateSync) _zones = found.AsReadOnly();
                    lock (_stateSync) SetProbe("YNCA", verifiedYnca ?
                        YamahaProtocolAvailability.Available : YamahaProtocolAvailability.Unavailable, note);
                    if (verifiedYnca) success++;
                    else
                    {
                        failures++;
                        diagnostics.Add("YNCA: TCP reachable but no native protocol reply observed.");
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    failures++;
                    diagnostics.Add("YNCA: " + ex.Message);
                    lock (_stateSync) SetProbe("YNCA", YamahaProtocolAvailability.Unavailable, ex.Message);
                }
                finally
                {
                    if (!originallyConnected)
                        _yncaSession.Disconnect();
                }
            }

            lock (_stateSync)
            {
                _capabilities = BuildCapabilities(_deviceInfo, _features, _functionStatus);
                _lastUpdatedUtc = DateTimeOffset.UtcNow;
                _stale = success == 0;
                _connectionState = success == 0 ? YamahaConnectionState.Faulted :
                    failures > 0 ? YamahaConnectionState.PartiallyConnected : YamahaConnectionState.Connected;
            }
            if (success == 0)
                throw new YamahaConnectionException("No enabled Yamaha protocol could be reached. " +
                    string.Join(" | ", diagnostics));
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
                _extendedVerifiedReads.Clear();
                _verifiedWrites.Clear();
                _acceptedActions.Clear();
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
            foreach (var probe in _protocols.Values)
            {
                if (probe.Availability != YamahaProtocolAvailability.Available) continue;
                known.Add(new YamahaCapability("protocol." + probe.Protocol.ToLowerInvariant(),
                    YamahaCapabilityStatus.ReadVerified, YamahaEvidenceSource.ReadProbe,
                    canRead: true, protocol: probe.Protocol,
                    model: device == null ? _yncaModel : device.ModelName,
                    firmware: device == null ? null : device.SystemVersion,
                    lastVerifiedUtc: probe.ObservedUtc));
            }
            if (_legacyActivePattern.HasValue &&
                (status == null || !status.ActiveSpeakerPattern.HasValue))
                known.Add(new YamahaCapability("legacy.speaker.active_pattern",
                    YamahaCapabilityStatus.ReadVerified, YamahaEvidenceSource.ReadProbe,
                    canRead: true, protocol: "LegacyXml",
                    model: device == null ? _yncaModel : device.ModelName,
                    lastVerifiedUtc: _protocols["LegacyXml"].ObservedUtc));
            foreach (var pair in _yncaVerifiedReads)
                known.Add(new YamahaCapability(pair.Key, YamahaCapabilityStatus.ReadVerified,
                    YamahaEvidenceSource.ReadProbe, canRead: true, protocol: "YNCA",
                    model: device == null ? _yncaModel : device.ModelName,
                    firmware: device == null ? null : device.SystemVersion,
                    lastVerifiedUtc: pair.Value));
            foreach (var pair in _speakerVerifiedReads)
                known.Add(new YamahaCapability(pair.Key,
                    YamahaCapabilityStatus.ReadVerified, YamahaEvidenceSource.ReadProbe,
                    canRead: true, protocol: "LegacyXml",
                    model: device == null ? _yncaModel : device.ModelName,
                    firmware: device == null ? null : device.SystemVersion,
                    lastVerifiedUtc: pair.Value));
            if (_legacyDescriptor != null)
                known.Add(new YamahaCapability("legacy.descriptor",
                    YamahaCapabilityStatus.ReadVerified, YamahaEvidenceSource.ReadProbe,
                    canRead: true, protocol: "LegacyXml",
                    model: device == null ? _yncaModel : device.ModelName,
                    firmware: device == null ? null : device.SystemVersion,
                    lastVerifiedUtc: _legacyDescriptor.ObservedUtc));
            _functionRoutes = YamahaFunctionRouteMapper.Build(device, features, status,
                _zones, _protocols["YNCA"].Availability == YamahaProtocolAvailability.Available,
                _protocols["LegacyXml"].Availability == YamahaProtocolAvailability.Available,
                _legacyActivePattern, _yncaVerifiedReads, _speakerVerifiedReads,
                _extendedVerifiedReads, _verifiedWrites, _acceptedActions);

            // Present a coherent logical capability as well as the per-protocol
            // FunctionRoutes. A write result cannot be generalized to other models.
            var merged = new Dictionary<string, YamahaCapability>(StringComparer.OrdinalIgnoreCase);
            foreach (var capability in known)
                merged[capability.FeatureId] = capability;
            foreach (var route in _functionRoutes)
            {
                if (route.CanRead != true && route.CanWrite != true && route.CanExecute != true)
                    continue;
                YamahaCapability prior;
                merged.TryGetValue(route.FunctionId, out prior);
                bool canRead = route.CanRead == true || (prior != null && prior.CanRead == true);
                bool canWrite = route.CanWrite == true || (prior != null && prior.CanWrite == true);
                bool canExecute = route.CanExecute == true || (prior != null && prior.CanExecute == true);
                var verification = canWrite ? YamahaCapabilityStatus.WriteVerified :
                    canRead ? YamahaCapabilityStatus.ReadVerified : YamahaCapabilityStatus.ActionAccepted;
                merged[route.FunctionId] = new YamahaCapability(route.FunctionId, verification,
                    canWrite ? YamahaEvidenceSource.VerifiedHardwareWrite : YamahaEvidenceSource.ReadProbe,
                    canRead: canRead ? (bool?)true : null,
                    canWrite: canWrite ? (bool?)true : null,
                    canExecute: canExecute ? (bool?)true : null,
                    protocol: route.Protocol,
                    model: device == null ? _yncaModel : device.ModelName,
                    firmware: device == null ? null : device.SystemVersion,
                    lastVerifiedUtc: route.LastObservedUtc ?? (prior == null ? null : prior.LastVerifiedUtc));
            }
            return new YamahaDeviceCapabilities(merged.Values);
        }

        private void RecordYncaRead(YamahaYncaMessage message)
        {
            if (_disposed || message == null || message.Kind != YamahaYncaMessageKind.Value)
                return;
            string key = "ynca." + message.Subunit.ToLowerInvariant() + "." +
                message.Function.ToLowerInvariant();
            lock (_stateSync)
            {
                _yncaVerifiedReads[key] = message.ReceivedUtc;
                _capabilities = BuildCapabilities(_deviceInfo, _features, _functionStatus);
            }
        }

        private void RecordExtendedRead(string protocolRouteKey, DateTimeOffset observedUtc)
        {
            lock (_stateSync)
            {
                if (_disposed) return;
                _extendedVerifiedReads[protocolRouteKey] = observedUtc;
                _capabilities = BuildCapabilities(_deviceInfo, _features, _functionStatus);
            }
        }

        private void RecordVerifiedWrite(string protocolRouteKey, DateTimeOffset observedUtc)
        {
            lock (_stateSync)
            {
                if (_disposed) return;
                _verifiedWrites[protocolRouteKey] = observedUtc;
                _capabilities = BuildCapabilities(_deviceInfo, _features, _functionStatus);
            }
        }

        private void RecordAcceptedAction(string protocolRouteKey, DateTimeOffset observedUtc)
        {
            lock (_stateSync)
            {
                if (_disposed) return;
                _acceptedActions[protocolRouteKey] = observedUtc;
                _capabilities = BuildCapabilities(_deviceInfo, _features, _functionStatus);
            }
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
                    "Extended Control is disabled; use Legacy XML or YNCA when available.");
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
            if (_descriptorProbe != null)
                _descriptorProbe.Dispose();
            if (_zoneController != null)
                _zoneController.Dispose();
            if (_yncaSession != null)
                _yncaSession.Dispose();
            lock (_stateSync) _connectionState = YamahaConnectionState.Disposed;
        }
    }
}
