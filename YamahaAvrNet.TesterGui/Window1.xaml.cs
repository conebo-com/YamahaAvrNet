using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YamahaAvrNet.Models;
using YamahaAvrNet.PublicApi;
using YamahaAvrNet.Protocols.Ynca;

namespace YamahaAvrNet.TesterGui
{
    public partial class SupportReportWindow : Window
    {
        private const string RemovedText = "[removed]";
        private const string MainZoneId = "main";
        private const int ReportPattern = 1;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };

        private readonly CancellationTokenSource _lifetime = new();
        private readonly ObservableCollection<ReportEntry> _entries = new();
        private readonly YamahaAvrClient _receiver;
        private readonly string _host;
        private readonly string? _detectedProfileId;
        private readonly string? _usedProfileId;
        private readonly string? _enteredReceiverType;
        private readonly bool _isCompatibilityMode;

        public SupportReportWindow(
            Window owner,
            YamahaAvrClient receiver,
            string host,
            string? detectedProfileId,
            string? selectedProfileId,
            string? enteredReceiverType)
        {
            if (receiver is null)
            {
                throw new ArgumentNullException(nameof(receiver)); // <-----------
            }

            InitializeComponent();
            ApplyLanguage();
            Owner = owner;

            _receiver = receiver;
            _host = host ?? string.Empty;
            _detectedProfileId = detectedProfileId;
            _usedProfileId = selectedProfileId ?? detectedProfileId;
            _enteredReceiverType = enteredReceiverType;
            _isCompatibilityMode = _usedProfileId != null &&
                !string.Equals(detectedProfileId, _usedProfileId, StringComparison.OrdinalIgnoreCase);

            LstOperations.ItemsSource = _entries;
            BtnPrepare.IsEnabled = false;

            if (_isCompatibilityMode)
            {
                TxtCompatibilityMode.Text = BuildCompatibilityText();
                BrdCompatibilityMode.Visibility = Visibility.Visible;
            }
        }

        private void ApplyLanguage()
        {
            Title = Loc.Get("Check receiver compatibility");
            TxtIntro.Text = Loc.Get("Check whether your receiver works with the functions of this tester and help the project with the result.\n1. Choose the report type below. 'Anonymized' leaves out your MAC address and host.\n2. Click 'Run read-only operations'. Values are only read; nothing is changed on your receiver.\n3. Check the results in the list: green = ok, yellow = warning, red = error.\n4. Click 'Prepare for sending', copy the report and paste it into an e-mail yourself. Nothing is sent automatically.");
            LblReportType.Text = Loc.Get("Report type:");
            RdoRegular.Content = Loc.Get("Regular");
            RdoAnonymized.Content = Loc.Get("Anonymized (no MAC address, serial number or host)");
            BtnRun.Content = Loc.Get("Run read-only operations");
            LblResultHeading.Text = Loc.Get("Result of the selected operation");
            LblIndividualInformation.Text = Loc.Get("Individual information (what ever you want to tell us ;-)");
            ChkMayContact.Content = Loc.Get("The project may contact me by e-mail if there are questions about this report");
            ChkProjectNews.Content = Loc.Get("Keep me informed by e-mail about changes to the project");
            BtnPrepare.Content = Loc.Get("Prepare for sending");
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            _lifetime.Cancel();
            base.OnClosed(eventArgs);
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs eventArgs)
        {
            BtnRun.IsEnabled = false;
            BtnPrepare.IsEnabled = false;
            _entries.Clear();
            TxtResult.Clear();

            try
            {
                foreach (var operation in BuildOperations())
                {
                    TxtProgress.Text = Loc.Format("Running '{0}' ...", operation.Name);
                    _entries.Add(await RunOperationAsync(operation));
                }

                var okCount = _entries.Count(entry => entry.Status == ReportStatus.Ok);
                var warningCount = _entries.Count(entry => entry.Status == ReportStatus.Warning);
                var errorCount = _entries.Count(entry => entry.Status == ReportStatus.Error);
                TxtProgress.Text = Loc.Format(
                    "Done: {0} operations ({1} ok, {2} warnings, {3} errors).",
                    _entries.Count,
                    okCount,
                    warningCount,
                    errorCount);
                LstOperations.SelectedIndex = 0;
            }
            catch (OperationCanceledException)
            {
                return; // <-----------
            }
            finally
            {
                BtnRun.IsEnabled = true;
                BtnPrepare.IsEnabled = _entries.Count > 0;
            }
        }

        private void BtnPrepare_Click(object sender, RoutedEventArgs eventArgs)
        {
            var previewWindow = new JsonPreviewWindow(this, BuildReportJson());
            previewWindow.ShowDialog();
        }

        private void LstOperations_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            TxtResult.Text = LstOperations.SelectedItem is ReportEntry entry ? entry.Output : string.Empty;
        }

        private async Task<ReportEntry> RunOperationAsync(ReportOperation operation)
        {
            try
            {
                var result = await operation.ExecuteAsync();
                return new ReportEntry(operation.Group, operation.Name, result.Status, result.Text); // <-----------
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                throw; // <-----------
            }
            catch (Exception exception)
            {
                return new ReportEntry(
                    operation.Group,
                    operation.Name,
                    ReportStatus.Error,
                    $"{exception.GetType().Name}: {exception.Message}"); // <-----------
            }
        }

        // ---------------------------------------------------------------
        // Read-only operations
        // ---------------------------------------------------------------

        private List<ReportOperation> BuildOperations()
        {
            var operations = new List<ReportOperation>
            {
                new("Receiver", "Receiver info", () => Task.FromResult(DescribeReceiver())),
                new("Receiver", "Protocol discovery", () => Task.FromResult(DescribeProtocols())),
                new("Receiver", "Capabilities", () => Task.FromResult(DescribeCapabilities())),
                new("Receiver", "Function routes", () => Task.FromResult(DescribeFunctionRoutes())),
                new("YNCA", "Receiver model (SYS/MODELNAME)", () => ReadYncaAsync("SYS", "MODELNAME")),
                new("YNCA", "Main zone (PWR/VOL/MUTE/INP)", () => ReadYncaAsync("MAIN", "PWR", "VOL", "MUTE", "INP"))
            };

            var zoneFound = false;
            foreach (var zone in _receiver.Zones)
            {
                var zoneId = zone.NativeId;
                zoneFound = true;
                operations.Add(new("Zones", "Zone status (" + zoneId + ")", () => ReadZoneStatusAsync(zoneId)));
            }

            if (!zoneFound)
            {
                operations.Add(new("Zones", "Zone status", () => Task.FromResult(Warning("No zones discovered."))));
            }

            operations.Add(new("Speakers", "Active speaker pattern", ReadActivePatternAsync));
            operations.Add(new("Speakers", "Advertised patterns", ReadPatternsAsync));
            operations.Add(new("Speakers", "Speaker levels (Pattern " + ReportPattern + ")", ReadLevelsAsync));
            operations.Add(new("Speakers", "Speaker distances (Pattern " + ReportPattern + ")", ReadDistancesAsync));
            operations.Add(new("Sound", "Zone sound status (" + MainZoneId + ")", ReadSoundStatusAsync));
            operations.Add(new("Sound", "Audio signal information (" + MainZoneId + ")", ReadSignalInfoAsync));
            operations.Add(new("Sound", "YNCA MAIN DSP", ReadMainDspAsync));
            operations.Add(new("Sound", "Advertised decoders, programs and scenes (" + MainZoneId + ")", () => Task.FromResult(DescribeAdvertisedSound())));

            return operations; // <-----------
        }

        private OperationResult DescribeReceiver()
        {
            var info = _receiver.DeviceInfo;
            var features = _receiver.DeviceFeatures;
            var status = _receiver.FunctionStatus;
            var builder = new StringBuilder();
            builder.AppendLine("Model:                " + Show(_receiver.Model));
            builder.AppendLine("Firmware:             " + Show(info?.SystemVersion));
            builder.AppendLine("Extended Control API: " + Show(info?.ApiVersion));
            builder.AppendLine("Reported zones:       " + Show(features?.ReportedZoneCount));
            builder.AppendLine("Speaker patterns:     " + Show(features?.SpeakerPatternCount));
            builder.AppendLine("Active pattern (Ext): " + Show(status?.ActiveSpeakerPattern));
            builder.AppendLine("Connection state:     " + _receiver.ConnectionState);
            builder.AppendLine("YNCA TCP connected:   " + (_receiver.Ynca != null && _receiver.Ynca.IsConnected));
            builder.AppendLine("Detected profile:     " + (_detectedProfileId ?? "-"));
            builder.AppendLine("Used profile:         " + (_usedProfileId ?? "-"));
            builder.AppendLine("Compatibility mode:   " + (_isCompatibilityMode ? "YES" : "no"));

            var text = builder.ToString().TrimEnd();
            return info == null || string.IsNullOrWhiteSpace(_receiver.Model)
                ? Warning(text)
                : Ok(text); // <-----------
        }

        private OperationResult DescribeProtocols()
        {
            var builder = new StringBuilder();
            var count = 0;
            foreach (var probe in _receiver.Protocols.Values.OrderBy(item => item.Protocol))
            {
                count++;
                builder.AppendLine(probe.Protocol + " | " + probe.Availability +
                    " | " + (probe.Detail ?? "not probed"));
            }

            builder.AppendLine("Unavailable is not proof of permanent lack of support.");
            return count == 0
                ? Warning(builder.ToString().TrimEnd())
                : Ok(builder.ToString().TrimEnd()); // <-----------
        }

        private OperationResult DescribeCapabilities()
        {
            var builder = new StringBuilder();
            var count = 0;
            foreach (var capability in _receiver.Capabilities.OrderBy(item => item.FeatureId))
            {
                count++;
                builder.AppendLine(capability.FeatureId + " | " + capability.Status + " | read " +
                    Permission(capability.CanRead) + " | write " + Permission(capability.CanWrite) +
                    " | execute " + Permission(capability.CanExecute) + " | " + capability.Evidence);
            }

            return count == 0
                ? Warning("No capabilities were reported.")
                : Ok(builder.ToString().TrimEnd()); // <-----------
        }

        private OperationResult DescribeFunctionRoutes()
        {
            var builder = new StringBuilder();
            var count = 0;
            foreach (var route in _receiver.FunctionRoutes.OrderBy(item => item.FunctionId)
                .ThenBy(item => item.Protocol))
            {
                count++;
                builder.AppendLine(route.FunctionId + " | " + route.Protocol + " | " +
                    route.NativeIdentifier + " | " + route.Status +
                    " | impl R/W/E=" + (route.ImplementedRead ? "Y" : "-") + "/" +
                    (route.ImplementedWrite ? "Y" : "-") + "/" +
                    (route.ImplementedExecute ? "Y" : "-") +
                    " | verified R/W/E=" + Permission(route.CanRead) + "/" +
                    Permission(route.CanWrite) + "/" + Permission(route.CanExecute));
            }

            if (count == 0)
            {
                return Warning("No function routes were reported."); // <-----------
            }

            builder.AppendLine("A route does not grant write permission or guarantee a typed operation.");
            return Ok(builder.ToString().TrimEnd()); // <-----------
        }

        private async Task<OperationResult> ReadYncaAsync(string subunit, params string[] functions)
        {
            var ynca = _receiver.Ynca;
            if (ynca == null)
            {
                return Warning("YNCA is disabled in receiver options."); // <-----------
            }

            if (!ynca.IsConnected)
            {
                return Warning("YNCA TCP is not connected. Connect YNCA in the main window first."); // <-----------
            }

            var builder = new StringBuilder();
            foreach (var function in functions)
            {
                var observed = await ynca.GetAsync(subunit, function);
                builder.AppendLine("YNCA observed " + observed.Raw);
            }

            return Ok(builder.ToString().TrimEnd()); // <-----------
        }

        private async Task<OperationResult> ReadZoneStatusAsync(string zoneId)
        {
            var zoneState = await _receiver.ZoneControl.RefreshAsync(zoneId);
            if (zoneState == null)
            {
                return Warning(FormatZone(zoneState)); // <-----------
            }

            return zoneState.IsStale
                ? Warning(FormatZone(zoneState))
                : Ok(FormatZone(zoneState)); // <-----------
        }

        private OperationResult? CheckReportPattern()
        {
            int? reported = _receiver.DeviceFeatures?.SpeakerPatternCount;
            if (reported.HasValue && ReportPattern > reported.Value)
            {
                return Warning("Pattern " + ReportPattern + " exceeds the device-reported count."); // <-----------
            }

            return null; // <-----------
        }

        private async Task<OperationResult> ReadActivePatternAsync()
        {
            int? active = await _receiver.Speakers.GetActivePatternAsync();
            var text = "Active speaker pattern (Legacy XML): " + Show(active);
            return active.HasValue ? Ok(text) : Warning(text); // <-----------
        }

        private async Task<OperationResult> ReadPatternsAsync()
        {
            var patterns = await _receiver.Speakers.GetPatternsAsync();
            var builder = new StringBuilder();
            var count = 0;
            foreach (var item in patterns)
            {
                count++;
                builder.AppendLine("Pattern " + item.Number +
                    " (active: " + (item.IsActive.HasValue
                    ? item.IsActive.Value.ToString() : "unknown") + ")");
                foreach (var channel in item.Channels)
                {
                    builder.AppendLine("  " + channel.NativeId +
                        " | level: " + Permission(channel.CanReadLevel) +
                        " | distance: " + Permission(channel.CanReadDistance) +
                        " | configured: unknown");
                }
            }

            return count == 0
                ? Warning("The receiver returned no patterns.")
                : Ok(builder.ToString().TrimEnd()); // <-----------
        }

        private async Task<OperationResult> ReadLevelsAsync()
        {
            var unavailable = CheckReportPattern();
            if (unavailable != null)
            {
                return unavailable; // <-----------
            }

            var levels = await _receiver.Speakers.GetLevelsAsync(ReportPattern);
            var text = FormatChannels("Levels", ReportPattern, levels).TrimEnd();
            return levels.Count == 0 ? Warning(text) : Ok(text); // <-----------
        }

        private async Task<OperationResult> ReadDistancesAsync()
        {
            var unavailable = CheckReportPattern();
            if (unavailable != null)
            {
                return unavailable; // <-----------
            }

            var distances = await _receiver.Speakers.GetDistancesAsync(ReportPattern);
            var text = FormatChannels("Distances", ReportPattern, distances).TrimEnd();
            return distances.Count == 0 ? Warning(text) : Ok(text); // <-----------
        }

        private async Task<OperationResult> ReadSoundStatusAsync()
        {
            if (_receiver.Sound == null)
            {
                return Warning("Extended Control is unavailable."); // <-----------
            }

            var status = await _receiver.Sound.GetStatusAsync(MainZoneId);
            return Ok(FormatSound(status)); // <-----------
        }

        private async Task<OperationResult> ReadSignalInfoAsync()
        {
            if (_receiver.Sound == null)
            {
                return Warning("Extended Control is unavailable."); // <-----------
            }

            var signal = await _receiver.Sound.GetSignalInfoAsync(MainZoneId);
            var builder = new StringBuilder();
            builder.AppendLine("Zone " + MainZoneId + " audio signal (read-only):");
            builder.AppendLine("  Format: " + Show(signal.AudioFormat));
            builder.AppendLine("  Sample rate: " + Show(signal.SampleRate));
            builder.AppendLine("  Audio error: " + Show(signal.AudioErrorCode));
            foreach (var field in signal.AudioFields)
            {
                builder.AppendLine("  audio." + field.Key + " = " + field.Value);
            }

            foreach (var field in signal.OtherFields)
            {
                builder.AppendLine("  " + field.Key + " = " + field.Value);
            }

            builder.AppendLine("Missing/--- does not imply no decoder or no current input support.");
            return Ok(builder.ToString().TrimEnd()); // <-----------
        }

        private async Task<OperationResult> ReadMainDspAsync()
        {
            if (_receiver.Sound == null)
            {
                return Warning("Extended Control is unavailable."); // <-----------
            }

            if (_receiver.Ynca == null)
            {
                return Warning("YNCA is disabled in receiver options."); // <-----------
            }

            if (!_receiver.Ynca.IsConnected)
            {
                return Warning("YNCA TCP is not connected. Connect YNCA in the main window first."); // <-----------
            }

            var dsp = await _receiver.Sound.GetMainYncaDspStatusAsync();
            var builder = new StringBuilder("YNCA MAIN DSP values (native, read-only):" + Environment.NewLine);
            foreach (var field in dsp.Values)
            {
                builder.AppendLine("  " + field.Key + " = " + field.Value);
            }

            var unavailableCount = 0;
            foreach (var field in dsp.Unavailable)
            {
                unavailableCount++;
                builder.AppendLine("  " + field.Key + " = " + field.Value);
            }

            builder.AppendLine("SOUNDPRG is stored DSP; Straight can change effective playback.");
            return unavailableCount > 0
                ? Warning(builder.ToString().TrimEnd())
                : Ok(builder.ToString().TrimEnd()); // <-----------
        }

        private OperationResult DescribeAdvertisedSound()
        {
            if (_receiver.Sound == null)
            {
                return Warning("Extended Control is unavailable."); // <-----------
            }

            var decoders = _receiver.Sound.GetAdvertisedSurroundDecoderTypes(MainZoneId);
            var programs = _receiver.Sound.GetAdvertisedSoundPrograms(MainZoneId);
            var builder = new StringBuilder();
            builder.AppendLine("Advertised surround decoder IDs: " + string.Join(", ", decoders));
            builder.AppendLine("Advertised sound programs: " + string.Join(", ", programs));
            builder.AppendLine("Advertised scene count: " + Show(_receiver.Sound.GetAdvertisedSceneCount(MainZoneId)));

            return decoders.Any() || programs.Any()
                ? Ok(builder.ToString().TrimEnd())
                : Warning(builder.ToString().TrimEnd()); // <-----------
        }

        // ---------------------------------------------------------------
        // Report
        // ---------------------------------------------------------------

        private string BuildCompatibilityText() =>
            $"Compatibility mode: the AVR reports '{Show(_receiver.Model)}' " +
            $"(detected profile '{_detectedProfileId ?? "none"}'), " +
            $"but profile '{_usedProfileId ?? "none"}' was selected manually and is used.";

        private string BuildReportJson()
        {
            var anonymized = RdoAnonymized.IsChecked == true;
            var snapshot = _receiver.GetOverview();
            var info = _receiver.DeviceInfo;

            var report = new
            {
                ReportVersion = 1,
                CreatedUtc = DateTimeOffset.UtcNow,
                Anonymized = anonymized,
                CompatibilityMode = new
                {
                    Active = _isCompatibilityMode,
                    DetectedProfile = _detectedProfileId,
                    UsedProfile = _usedProfileId,
                    ReportedModelName = _receiver.Model,
                    Notice = _isCompatibilityMode
                        ? BuildCompatibilityText()
                        : "Not used. The profile detected for the AVR was used."
                },
                Receiver = new
                {
                    ModelName = _receiver.Model,
                    ModelNameReported = !string.IsNullOrWhiteSpace(_receiver.Model),
                    UserEnteredReceiverType = _enteredReceiverType,
                    Firmware = info?.SystemVersion,
                    ExtendedControlApiVersion = info?.ApiVersion,
                    ConnectionState = Text(snapshot.State.ConnectionState),
                    IsStale = snapshot.State.IsStale,
                    Host = anonymized ? null : _host
                },
                DiscoveredZones = snapshot.DiscoveredZones.Select(zone => zone.NativeId).ToArray(),
                Protocols = snapshot.Protocols.Values
                    .OrderBy(probe => probe.Protocol)
                    .Select(probe => new
                    {
                        Protocol = Text(probe.Protocol),
                        Availability = Text(probe.Availability)
                    })
                    .ToArray(),
                Capabilities = _receiver.Capabilities
                    .OrderBy(capability => capability.FeatureId)
                    .Select(capability => new
                    {
                        FeatureId = Text(capability.FeatureId),
                        Status = Text(capability.Status),
                        CanRead = capability.CanRead,
                        CanWrite = capability.CanWrite,
                        CanExecute = capability.CanExecute,
                        Evidence = Anonymize(Text(capability.Evidence), anonymized)
                    })
                    .ToArray(),
                FunctionRoutes = _receiver.FunctionRoutes
                    .OrderBy(route => route.FunctionId)
                    .ThenBy(route => route.Protocol)
                    .Select(route => new
                    {
                        FunctionId = Text(route.FunctionId),
                        Protocol = Text(route.Protocol),
                        NativeIdentifier = Text(route.NativeIdentifier),
                        Status = Text(route.Status),
                        ImplementedRead = route.ImplementedRead,
                        ImplementedWrite = route.ImplementedWrite,
                        ImplementedExecute = route.ImplementedExecute,
                        CanRead = route.CanRead,
                        CanWrite = route.CanWrite,
                        CanExecute = route.CanExecute
                    })
                    .ToArray(),
                Operations = _entries
                    .Select(entry => new
                    {
                        entry.Group,
                        entry.Name,
                        entry.Status,
                        OutputLines = ToOutputLines(entry.Output, anonymized)
                    })
                    .ToArray(),
                Notes = new[]
                {
                    "Only read-only operations were run. No writes were performed.",
                    "Not an exhaustive hardware compatibility test.",
                    "Function routes are metadata, NOT write authorization.",
                    "MAC addresses and serial numbers are never included in this report.",
                    "No automatic transmission is performed."
                },
                User = new
                {
                    Message = string.Empty,
                    IndividualInformation = TxtIndividualInformation.Text.Trim(),
                    MayContactByMail = ChkMayContact.IsChecked == true,
                    WantsProjectNewsByMail = ChkProjectNews.IsChecked == true
                }
            };

            return JsonSerializer.Serialize(report, JsonOptions); // <-----------
        }

        private string[] ToOutputLines(string output, bool anonymized)
        {
            var text = Anonymize(output, anonymized) ?? string.Empty;
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            return text.Split('\n'); // <-----------
        }

        private string? Anonymize(string? text, bool anonymized)
        {
            if (!anonymized || text is null || text.Length == 0 || _host.Length == 0)
            {
                return text; // <-----------
            }

            return NetFrameworkCompat.Replace(text, _host, RemovedText, StringComparison.OrdinalIgnoreCase); // <-----------
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static OperationResult Ok(string text) => new(ReportStatus.Ok, text);

        private static OperationResult Warning(string text) => new(ReportStatus.Warning, text);

        private static string? Text(object? value) => value?.ToString();

        private static string FormatZone(YamahaZoneState? zone)
        {
            if (zone is null)
            {
                return "No zone value observed."; // <-----------
            }

            return "Zone " + zone.NativeId + " | power=" +
                (zone.IsPoweredOn.HasValue ? (zone.IsPoweredOn.Value ? "On" : "Standby") : "unknown") +
                " | volume=" + (zone.VolumeDb.HasValue
                    ? zone.VolumeDb.Value.ToString(CultureInfo.InvariantCulture) + " dB" : "unknown") +
                " | mute=" + (zone.IsMuted.HasValue ? zone.IsMuted.Value.ToString() : "unknown") +
                " | input=" + Show(zone.InputNativeId) + (zone.IsStale ? " [STALE]" : ""); // <-----------
        }

        private static string FormatSound(YamahaSoundStatus state)
        {
            return "Zone " + state.ZoneNativeId + " | program=" + Show(state.SoundProgramNativeId) +
                " | pure direct=" + ShowBool(state.PureDirectEnabled) +
                " | enhancer=" + ShowBool(state.EnhancerEnabled) +
                " | direct=" + ShowBool(state.DirectEnabled) +
                " | surround decoder=" + Show(state.SurroundDecoderNativeId) +
                " | surround 3D=" + ShowBool(state.Surround3dEnabled) +
                " | observed UTC=" + state.ObservedUtc.ToString("u", CultureInfo.InvariantCulture); // <-----------
        }

        private static string FormatChannels(string kind, int pattern,
            IReadOnlyDictionary<string, YamahaSpeakerValue> values)
        {
            var builder = new StringBuilder();
            builder.AppendLine(kind + " for Pattern " + pattern + " (" + values.Count + " channels)");
            foreach (var channel in values.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                builder.AppendLine(channel.Key.PadRight(24) + " " +
                    channel.Value.Value.ToString(CultureInfo.InvariantCulture) + " " + channel.Value.Unit +
                    "  [Val=" + channel.Value.RawValue.ToString(CultureInfo.InvariantCulture) +
                    ", Exp=" + channel.Value.Exponent + "]");
            }

            return builder.ToString(); // <-----------
        }

        private static string ShowBool(bool? value)
        {
            return value.HasValue ? (value.Value ? "On" : "Off") : "unknown"; // <-----------
        }

        private static string Permission(bool? permission)
        {
            return !permission.HasValue ? "Unknown" : permission.Value ? "Yes" : "No"; // <-----------
        }

        private static string Show(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value!; // <-----------
        }

        private static string Show(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown"; // <-----------
        }
    }

    internal enum ReportStatus
    {
        Ok,
        Warning,
        Error
    }

    internal sealed record OperationResult(ReportStatus Status, string Text);

    internal sealed record ReportOperation(
        string Group,
        string Name,
        Func<Task<OperationResult>> ExecuteAsync);

    internal sealed record ReportEntry(string Group, string Name, ReportStatus Status, string Output)
    {
        public string DisplayName => $"{Group}: {Name}";

        public Brush StatusBrush => Status switch
        {
            ReportStatus.Ok => Brushes.Green,
            ReportStatus.Warning => Brushes.Gold,
            _ => Brushes.Red
        };
    }
}
