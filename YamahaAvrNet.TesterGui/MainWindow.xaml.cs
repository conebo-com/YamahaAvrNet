using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using YamahaAvrNet.Models;
using YamahaAvrNet.PublicApi;
using YamahaAvrNet.Protocols.Ynca;

namespace YamahaAvrNet.TesterGui
{
    /// <summary>
    /// Manual command browser following the DenonMarantz tester layout.
    /// All receiver communication is performed by the YamahaAvrNet library.
    /// </summary>
    public partial class MainWindow : Window
    {
        private YamahaAvrClient _receiver;
        private bool _busy;

        public MainWindow()
        {
            InitializeComponent();
            var commands = new List<TesterCommand>
            {
                new TesterCommand("Overview", "Receiver summary", "summary"),
                new TesterCommand("Overview", "Protocol discovery results", "protocols"),
                new TesterCommand("Overview", "Refresh function status", "refresh"),
                new TesterCommand("Overview", "Available zones and sources", "zones"),
                new TesterCommand("Overview", "Capabilities and evidence", "capabilities"),
                new TesterCommand("Overview", "Function routes and legacy description", "function-routes"),
                new TesterCommand("Speaker Setup", "Read active speaker pattern", "active"),
                new TesterCommand("Speaker Setup", "Read all advertised patterns", "patterns"),
                new TesterCommand("Speaker Setup", "Read speaker levels", "levels", true),
                new TesterCommand("Speaker Setup", "Read speaker distances", "distances", true),
                new TesterCommand("Speaker Setup", "Set Front_L level (verified)", "set-level", true, true),
                new TesterCommand("Speaker Setup", "Set Front_L distance (verified)", "set-distance", true, true),
                new TesterCommand("YNCA / TCP", "Read main-zone status (PWR/VOL/MUTE/INP)", "ynca-main"),
                new TesterCommand("YNCA / TCP", "Read receiver model (SYS/MODELNAME)", "ynca-model"),
                new TesterCommand("YNCA / TCP", "Read custom YNCA function", "ynca-custom"),
                new TesterCommand("YNCA / Zones", "Refresh zone status", "zone-status"),
                new TesterCommand("YNCA / Zones", "Show observed zone state cache", "zone-cache"),
                new TesterCommand("YNCA / Zones", "Set zone power (On/Standby)", "zone-set-power", false, true),
                new TesterCommand("YNCA / Zones", "Set zone mute (On/Off)", "zone-set-mute", false, true),
                new TesterCommand("YNCA / Zones", "Set zone volume (dB)", "zone-set-volume", false, true),
                new TesterCommand("YNCA / Zones", "Set zone input (native ID)", "zone-set-input", false, true),
                new TesterCommand("Sound / DSP", "Read YNCA Main DSP status", "sound-ynca"),
                new TesterCommand("Sound / DSP", "Read zone sound status", "sound-status"),
                new TesterCommand("Sound / DSP", "Read audio signal information", "sound-signal-info"),
                new TesterCommand("Sound / DSP", "Read extended YNCA MAIN DSP", "sound-ynca-advanced"),
                new TesterCommand("Sound / DSP", "Show available decoder types", "sound-decoders"),
                new TesterCommand("Sound / DSP", "Set surround decoder (native ID)", "sound-set-decoder", false, true),
                new TesterCommand("Sound / DSP", "Show available sound programs", "sound-programs"),
                new TesterCommand("Sound / DSP", "Set sound program (native ID)", "sound-set-program", false, true),
                new TesterCommand("Sound / DSP", "Set Pure Direct (On/Off; MAIN)", "sound-pure-direct", false, true),
                new TesterCommand("Sound / DSP", "Set Straight (On/Off; MAIN)", "sound-straight", false, true),
                new TesterCommand("Sound / DSP", "Set Enhancer (On/Off; MAIN)", "sound-enhancer", false, true),
                new TesterCommand("Scenes", "Show available scene numbers", "scene-list"),
                new TesterCommand("Scenes", "Recall scene by number", "scene-recall", false, true)
            };
            LstCommands.ItemsSource = commands;
            ICollectionView view = CollectionViewSource.GetDefaultView(LstCommands.ItemsSource);
            view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            LstCommands.SelectedIndex = 0;
            UpdateControls();
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;
            int port;
            if (!int.TryParse(TxtPort.Text.Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
            {
                AppendResult("Enter a valid HTTP port (1-65535).");
                return;
            }
            int yncaPort;
            if (!int.TryParse(TxtYncaPort.Text.Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out yncaPort) || yncaPort < 1 || yncaPort > 65535)
            {
                AppendResult("Enter a valid YNCA TCP port (1-65535).");
                return;
            }
            YamahaAvrClient next = null;
            BeginOperation();
            try
            {
                next = new YamahaAvrClient(new YamahaAvrOptions
                {
                    Host = TxtHost.Text,
                    HttpPort = port,
                    YncaPort = yncaPort,
                    EnableExtendedControl = true,
                    EnableLegacyXml = true,
                    EnableYnca = true,
                    EnableVerifiedSpeakerWrites = ChkEnableVerifiedWrites.IsChecked == true,
                    EnableYncaZoneWrites = ChkEnableYncaWrites.IsChecked == true,
                    EnableSoundSceneWrites = ChkEnableSoundWrites.IsChecked == true
                });
                TxtConnectionStatus.Text = "Connecting...";
                await next.InitializeAsync();
                AppendHttp("Read-only multi-protocol discovery completed; inspect Protocol discovery results.");
                var old = _receiver;
                if (old != null && old.Ynca != null)
                {
                    old.Ynca.MessageReceived -= ReceiverYnca_MessageReceived;
                    old.Ynca.ConnectionChanged -= ReceiverYnca_ConnectionChanged;
                }
                _receiver = next;
                _receiver.Ynca.MessageReceived += ReceiverYnca_MessageReceived;
                _receiver.Ynca.ConnectionChanged += ReceiverYnca_ConnectionChanged;
                next = null;
                if (old != null)
                    old.Dispose();
                TxtConnectionStatus.Text = "Connected: " + Show(_receiver.Model);
                AppendResult(BuildSummary());
            }
            catch (Exception ex)
            {
                AppendResult("Connection failed: " + ex.Message);
                TxtConnectionStatus.Text = _receiver == null ? "Not connected"
                    : "Connected: " + Show(_receiver.Model);
            }
            finally
            {
                if (next != null)
                    next.Dispose();
                EndOperation();
            }
        }

        private async void BtnYncaConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || _receiver == null || _receiver.Ynca == null) return;
            BeginOperation();
            try
            {
                await _receiver.Ynca.ConnectAsync();
                AppendResult("YNCA TCP connected. Receive notifications appear in the YNCA pane.");
            }
            catch (Exception ex) { AppendResult("YNCA connect failed: " + ex.Message); }
            finally { EndOperation(); }
        }

        private void BtnYncaDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || _receiver == null || _receiver.Ynca == null) return;
            _receiver.Ynca.Disconnect();
            AppendResult("YNCA session closed. HTTP discovery remains available.");
            UpdateControls();
        }

        private void ReceiverYnca_MessageReceived(object sender, YamahaYncaMessageEventArgs e)
        {
            string line = e.Message.Raw;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_receiver == null || !ReferenceEquals(sender, _receiver.Ynca)) return;
                TxtYncaOutput.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                    " < " + line + Environment.NewLine);
                TxtYncaOutput.ScrollToEnd();
            }));
        }

        private void ReceiverYnca_ConnectionChanged(object sender, YamahaYncaConnectionEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_receiver == null || !ReferenceEquals(sender, _receiver.Ynca)) return;
                TxtYncaOutput.AppendText((e.IsConnected ? "CONNECTED: " : "DISCONNECTED: ") +
                    e.Reason + Environment.NewLine);
                TxtYncaOutput.ScrollToEnd();
                UpdateControls();
            }));
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || _receiver == null)
                return;
            BeginOperation();
            try
            {
                AppendHttp("Refresh: Extended function status or multi-protocol rediscovery");
                await _receiver.RefreshAsync();
                AppendResult(BuildSummary());
            }
            catch (Exception ex) { AppendResult("Refresh failed: " + ex.Message); }
            finally { EndOperation(); }
        }

        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || _receiver == null)
                return;
            BeginOperation();
            try
            {
                await _receiver.DisconnectAsync();
                if (_receiver.Ynca != null)
                {
                    _receiver.Ynca.MessageReceived -= ReceiverYnca_MessageReceived;
                    _receiver.Ynca.ConnectionChanged -= ReceiverYnca_ConnectionChanged;
                }
                _receiver.Dispose();
                _receiver = null;
                TxtConnectionStatus.Text = "Not connected";
                AppendResult("Disconnected. Previously displayed values are not live.");
            }
            catch (Exception ex) { AppendResult("Disconnect failed: " + ex.Message); }
            finally { EndOperation(); }
        }

        private void LstCommands_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var command = LstCommands.SelectedItem as TesterCommand;
            bool pattern = command != null && command.RequiresPattern;
            if (LblPattern != null)
                LblPattern.Visibility = pattern ? Visibility.Visible : Visibility.Collapsed;
            if (TxtPattern != null)
                TxtPattern.Visibility = pattern ? Visibility.Visible : Visibility.Collapsed;
            bool soundCommand = command != null &&
                (command.Key.StartsWith("sound-", StringComparison.Ordinal) ||
                 command.Key.StartsWith("scene-", StringComparison.Ordinal));
            bool zoneCommand = command != null &&
                (command.Key.StartsWith("zone-", StringComparison.Ordinal) || soundCommand);
            LblZone.Visibility = zoneCommand ? Visibility.Visible : Visibility.Collapsed;
            TxtZone.Visibility = zoneCommand ? Visibility.Visible : Visibility.Collapsed;
            bool write = command != null && command.RequiresValue;
            if (LblWriteValue != null)
                LblWriteValue.Visibility = write ? Visibility.Visible : Visibility.Collapsed;
            if (TxtWriteValue != null)
                TxtWriteValue.Visibility = write ? Visibility.Visible : Visibility.Collapsed;
            if (TxtCommandHint != null)
                TxtCommandHint.Text = soundCommand
                    ? (write ? "Sound and scene writes require the separate opt-in and confirmation. " +
                        "Programs use Extended Control native IDs; On/Off switches use YNCA MAIN. " +
                        "Scene actions are accepted by HTTP but cannot be fully verified by readback."
                        : "Read sound status, supported sound programs and scene counts for the selected zone.")
                    : zoneCommand
                    ? (write ? "YNCA zone writes change receiver settings. Require explicit opt-in, " +
                        "device-reported validation, confirmation and readback. " +
                        "Use On/Standby, On/Off, a dB value or a reported input ID as appropriate."
                        : "Read discovered zone values. A missing value stays unknown.")
                    : write
                        ? "Verified scope: RX-A3060 FW 2.86, active Pattern 1, Front_L only. " +
                          (command.Key == "set-level" ? "Allowed: 0.0 or 0.5 dB." : "Allowed: 2.95 or 3.00 m.") +
                          " Requires opt-in before connecting and confirmation for each write."
                        : pattern
                            ? "Use a pattern reported by your receiver. Reads do not alter settings."
                            : "Commands read device information only.";
            bool yncaCustom = command != null && command.Key == "ynca-custom";
            LblYncaSubunit.Visibility = yncaCustom ? Visibility.Visible : Visibility.Collapsed;
            TxtYncaSubunit.Visibility = yncaCustom ? Visibility.Visible : Visibility.Collapsed;
            LblYncaFunction.Visibility = yncaCustom ? Visibility.Visible : Visibility.Collapsed;
            TxtYncaFunction.Visibility = yncaCustom ? Visibility.Visible : Visibility.Collapsed;
            UpdateControls();
        }

        private async void LstCommands_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await ExecuteSelectedAsync();
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteSelectedAsync();
        }

        private async Task ExecuteSelectedAsync()
        {
            if (_busy || _receiver == null)
                return;
            TesterCommand command = LstCommands.SelectedItem as TesterCommand;
            if (command == null)
                return;
            int pattern = 0;
            if (command.RequiresPattern)
            {
                if (!int.TryParse(TxtPattern.Text, NumberStyles.None,
                        CultureInfo.InvariantCulture, out pattern) || pattern < 1 || pattern > 99)
                {
                    AppendResult("Enter a valid speaker pattern number (1-99).");
                    return;
                }
                int? reported = _receiver.DeviceFeatures == null ? (int?)null
                    : _receiver.DeviceFeatures.SpeakerPatternCount;
                if (reported.HasValue && pattern > reported.Value)
                {
                    AppendResult("Pattern " + pattern + " exceeds the device-reported count.");
                    return;
                }
            }
            BeginOperation();
            try
            {
                if (command.Key.StartsWith("zone-set-", StringComparison.Ordinal))
                {
                    await ExecuteZoneWriteAsync(command.Key);
                    return;
                }
                if (command.Key.StartsWith("sound-", StringComparison.Ordinal) ||
                    command.Key.StartsWith("scene-", StringComparison.Ordinal))
                {
                    await ExecuteSoundCommandAsync(command);
                    return;
                }
                if (command.RequiresValue)
                {
                    if (ChkEnableVerifiedWrites.IsChecked != true)
                    {
                        AppendResult("Write mode is disabled. Enable it and reconnect to the receiver.");
                        return;
                    }
                    if (pattern != 1)
                    {
                        AppendResult("Verified writes are limited to Pattern 1. No PUT was sent.");
                        return;
                    }
                    decimal target;
                    if (!decimal.TryParse(TxtWriteValue.Text.Trim(),
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out target))
                    {
                        AppendResult("Enter a decimal value using a dot (e.g. 0.5 or 2.95).");
                        return;
                    }
                    bool distance = command.Key == "set-distance";
                    var currentValues = distance
                        ? await _receiver.Speakers.GetDistancesAsync(pattern)
                        : await _receiver.Speakers.GetLevelsAsync(pattern);
                    YamahaSpeakerValue oldValue;
                    if (!currentValues.TryGetValue("Front_L", out oldValue))
                    {
                        AppendResult("The Front_L channel was not returned. No write was sent.");
                        return;
                    }
                    var question = "Write " + target.ToString(CultureInfo.InvariantCulture) +
                        (distance ? " m" : " dB") + " to Pattern " + pattern + " / Front_L?" +
                        "\nCurrent value: " + oldValue.Value.ToString(CultureInfo.InvariantCulture) +
                        " " + oldValue.Unit +
                        "\nThis changes the AVR configuration. It is not automatically restored.";
                    if (MessageBox.Show(this, question, "Confirm Yamaha speaker write",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning,
                        MessageBoxResult.No) != MessageBoxResult.Yes)
                    {
                        AppendResult("Write canceled by user. No PUT was sent.");
                        return;
                    }
                    AppendHttp("POST /YamahaRemoteControl/ctrl (verified PUT Pattern_1/" +
                        (distance ? "Distance/Meter" : "Lvl") + "/Front_L; readback required)");
                    if (distance)
                        await _receiver.Speakers.SetDistanceAsync(pattern, "Front_L", target);
                    else
                        await _receiver.Speakers.SetLevelAsync(pattern, "Front_L", target);
                    AppendResult("Write completed and verified by the library. Previous value: " +
                        oldValue.Value.ToString(CultureInfo.InvariantCulture) + " " + oldValue.Unit +
                        "; confirmed value: " + target.ToString(CultureInfo.InvariantCulture) +
                        (distance ? " m" : " dB"));
                    return;
                }
                switch (command.Key)
                {
                    case "zone-status":
                        var zoneState = await _receiver.ZoneControl.RefreshAsync(TxtZone.Text.Trim());
                        AppendResult(FormatZone(zoneState));
                        break;
                    case "zone-cache":
                        var snapshots = _receiver.ZoneControl.GetStates();
                        AppendResult(snapshots.Count == 0 ? "No zone values have been observed yet."
                            : string.Join(Environment.NewLine, snapshots.Values.Select(FormatZone)));
                        break;
                    case "ynca-main":
                        foreach (string function in new[] { "PWR", "VOL", "MUTE", "INP" })
                            await DisplayYncaGetAsync("MAIN", function);
                        break;
                    case "ynca-model":
                        await DisplayYncaGetAsync("SYS", "MODELNAME");
                        break;
                    case "ynca-custom":
                        await DisplayYncaGetAsync(TxtYncaSubunit.Text.Trim(), TxtYncaFunction.Text.Trim());
                        break;
                    case "protocols":
                        AppendResult(BuildProtocols());
                        break;
                    case "summary":
                        AppendResult(BuildSummary());
                        break;
                    case "refresh":
                        AppendHttp("Refresh: Extended function status or protocol rediscovery");
                        await _receiver.RefreshAsync();
                        AppendResult(BuildSummary());
                        break;
                    case "zones":
                        AppendResult(BuildZones());
                        break;
                    case "capabilities":
                        AppendResult(BuildCapabilities());
                        break;
                    case "function-routes":
                        AppendResult(BuildFunctionRoutes());
                        break;
                    case "active":
                        AppendHttp("POST /YamahaRemoteControl/ctrl (GET Pattern_Sel)");
                        int? active = await _receiver.Speakers.GetActivePatternAsync();
                        AppendResult("Active speaker pattern (Legacy XML): " + Show(active));
                        break;
                    case "patterns":
                        AppendHttp("POST /YamahaRemoteControl/ctrl (GET Pattern_Sel, Lvl/Distance for each pattern)");
                        var patterns = await _receiver.Speakers.GetPatternsAsync();
                        var lines = new StringBuilder();
                        foreach (var item in patterns)
                        {
                            lines.AppendLine("Pattern " + item.Number +
                                " (active: " + (item.IsActive.HasValue
                                ? item.IsActive.Value.ToString() : "unknown") + ")");
                            foreach (var channel in item.Channels)
                                lines.AppendLine("  " + channel.NativeId +
                                    " | level: " + Permission(channel.CanReadLevel) +
                                    " | distance: " + Permission(channel.CanReadDistance) +
                                    " | configured: unknown");
                        }
                        AppendResult(lines.ToString());
                        break;
                    case "levels":
                        AppendHttp("POST /YamahaRemoteControl/ctrl (GET Pattern_" + pattern + "/Lvl)");
                        AppendResult(FormatChannels("Levels", pattern,
                            await _receiver.Speakers.GetLevelsAsync(pattern)));
                        break;
                    case "distances":
                        AppendHttp("POST /YamahaRemoteControl/ctrl (GET Pattern_" + pattern + "/Distance)");
                        AppendResult(FormatChannels("Distances", pattern,
                            await _receiver.Speakers.GetDistancesAsync(pattern)));
                        break;
                }
            }
            catch (Exception ex) { AppendResult("Operation failed: " + ex.Message); }
            finally { EndOperation(); }
        }

        private async Task ExecuteSoundCommandAsync(TesterCommand command)
        {
            if (_receiver.Sound == null)
                throw new InvalidOperationException("Extended Control is unavailable.");
            string zone = TxtZone.Text.Trim();
            if (command.Key == "sound-ynca")
            {
                if (_receiver.Ynca == null)
                    throw new InvalidOperationException("YNCA is disabled for this receiver.");
                foreach (string field in new[] { "SOUNDPRG", "STRAIGHT", "PUREDIRMODE", "ENHANCER" })
                    await DisplayYncaGetAsync("MAIN", field);
                return;
            }
            if (command.Key == "sound-ynca-advanced")
            {
                var dsp = await _receiver.Sound.GetMainYncaDspStatusAsync();
                var sb = new StringBuilder("YNCA MAIN DSP values (native, read-only):" + Environment.NewLine);
                foreach (var field in dsp.Values)
                    sb.AppendLine("  " + field.Key + " = " + field.Value);
                foreach (var field in dsp.Unavailable)
                    sb.AppendLine("  " + field.Key + " = " + field.Value);
                sb.AppendLine("SOUNDPRG is stored DSP; Straight can change effective playback.");
                AppendResult(sb.ToString());
                return;
            }
            if (command.Key == "sound-signal-info")
            {
                AppendHttp("GET /YamahaExtendedControl/v1/" + zone + "/getSignalInfo");
                var signal = await _receiver.Sound.GetSignalInfoAsync(zone);
                var sb = new StringBuilder("Zone " + zone + " audio signal (read-only):" + Environment.NewLine);
                sb.AppendLine("  Format: " + Show(signal.AudioFormat));
                sb.AppendLine("  Sample rate: " + Show(signal.SampleRate));
                sb.AppendLine("  Audio error: " + Show(signal.AudioErrorCode));
                foreach (var field in signal.AudioFields)
                    sb.AppendLine("  audio." + field.Key + " = " + field.Value);
                foreach (var field in signal.OtherFields)
                    sb.AppendLine("  " + field.Key + " = " + field.Value);
                sb.AppendLine("Missing/--- does not imply no decoder or no current input support.");
                AppendResult(sb.ToString());
                return;
            }
            if (command.Key == "sound-decoders")
            {
                AppendResult("Advertised surround decoder IDs for " + zone + ": " +
                    string.Join(", ", _receiver.Sound.GetAdvertisedSurroundDecoderTypes(zone)));
                return;
            }
            if (command.Key == "sound-status")
            {
                AppendHttp("GET /YamahaExtendedControl/v1/" + zone + "/getStatus");
                AppendResult(FormatSound(await _receiver.Sound.GetStatusAsync(zone)));
                return;
            }
            if (command.Key == "sound-programs")
            {
                AppendResult("Advertised programs for " + zone + ": " +
                    string.Join(", ", _receiver.Sound.GetAdvertisedSoundPrograms(zone)));
                return;
            }
            if (command.Key == "scene-list")
            {
                AppendResult("Advertised scene count for " + zone + ": " +
                    Show(_receiver.Sound.GetAdvertisedSceneCount(zone)));
                return;
            }
            if (ChkEnableSoundWrites.IsChecked != true)
            {
                AppendResult("Sound/scene writes are disabled. Enable the checkbox and reconnect.");
                return;
            }
            string value = TxtWriteValue.Text.Trim();
            bool switchAction = command.Key == "sound-pure-direct" ||
                command.Key == "sound-straight" || command.Key == "sound-enhancer";
            bool enabled = false;
            if (switchAction)
            {
                if (string.Equals(value, "On", StringComparison.OrdinalIgnoreCase)) enabled = true;
                else if (!string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Use On or Off for a DSP switch.");
                if (!string.Equals(zone, "main", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("This YNCA DSP switch is currently MAIN-only.");
            }
            int scene = 0;
            if (command.Key == "scene-recall" &&
                (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out scene) || scene < 1))
                throw new ArgumentException("Enter a positive scene number.");
            // Validate the selected ID / scene count before asking for confirmation.
            if (command.Key == "sound-set-decoder" &&
                !_receiver.Sound.GetAdvertisedSurroundDecoderTypes(zone).Any(x =>
                    string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Select a receiver-advertised native decoder ID.");
            if (command.Key == "sound-set-program" &&
                !_receiver.Sound.GetAdvertisedSoundPrograms(zone).Any(x =>
                    string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Select an advertised native sound program ID.");
            if (command.Key == "scene-recall")
            {
                int? count = _receiver.Sound.GetAdvertisedSceneCount(zone);
                if (!count.HasValue || scene > count.Value)
                    throw new ArgumentException("Scene number exceeds the advertised count.");
            }
            string question = "Execute " + command.Name + " on " + zone + " with value '" + value + "'?" +
                "\nThis changes receiver settings and is not automatically restored.";
            if (MessageBox.Show(this, question, "Confirm Yamaha sound/scene action",
                MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                AppendResult("Action canceled; nothing sent.");
                return;
            }
            switch (command.Key)
            {
                case "sound-set-decoder":
                    AppendHttp("GET /YamahaExtendedControl/v1/" + zone +
                        "/setSurroundDecoderType (validated ID; GET readback)");
                    await _receiver.Sound.SetSurroundDecoderTypeAsync(zone, value);
                    AppendResult("Surround decoder readback confirmed: " + value);
                    break;
                case "sound-set-program":
                    AppendHttp("GET /YamahaExtendedControl/v1/" + zone + "/setSoundProgram (validated ID)");
                    await _receiver.Sound.SetSoundProgramAsync(zone, value);
                    AppendResult("Sound program readback confirmed: " + value);
                    break;
                case "sound-pure-direct":
                    await _receiver.Sound.SetPureDirectAsync(enabled);
                    AppendResult("Pure Direct YNCA readback matched the requested value.");
                    break;
                case "sound-straight":
                    await _receiver.Sound.SetStraightAsync(enabled);
                    AppendResult("Straight YNCA readback matched the requested value.");
                    break;
                case "sound-enhancer":
                    await _receiver.Sound.SetEnhancerAsync(enabled);
                    AppendResult("Enhancer YNCA readback matched the requested value.");
                    break;
                case "scene-recall":
                    AppendHttp("GET /YamahaExtendedControl/v1/" + zone + "/recallScene (validated number)");
                    await _receiver.Sound.RecallSceneAsync(zone, scene);
                    AppendResult("Scene recall accepted by Yamaha (response_code=0). " +
                        "The resulting input/DSP configuration is NOT independently verified.");
                    break;
            }
        }

        private static string FormatSound(YamahaSoundStatus state)
        {
            return "Zone " + state.ZoneNativeId + " | program=" + Show(state.SoundProgramNativeId) +
                " | pure direct=" + ShowBool(state.PureDirectEnabled) +
                " | enhancer=" + ShowBool(state.EnhancerEnabled) +
                " | direct=" + ShowBool(state.DirectEnabled) +
                " | surround decoder=" + Show(state.SurroundDecoderNativeId) +
                " | surround 3D=" + ShowBool(state.Surround3dEnabled) +
                " | observed UTC=" + state.ObservedUtc.ToString("u", CultureInfo.InvariantCulture);
        }

        private static string ShowBool(bool? value)
        {
            return value.HasValue ? (value.Value ? "On" : "Off") : "unknown";
        }

        private async Task ExecuteZoneWriteAsync(string key)
        {
            if (ChkEnableYncaWrites.IsChecked != true)
            {
                AppendResult("YNCA writes are disabled. Enable the checkbox and reconnect.");
                return;
            }
            string zone = TxtZone.Text.Trim();
            string input = TxtWriteValue.Text.Trim();
            // Only the library decides whether a specific zone/value can be written.
            string question = "Send " + key + " to zone '" + zone + "' with value '" + input + "'?" +
                "\nThis changes receiver settings and is NOT automatically restored.";
            if (MessageBox.Show(this, question, "Confirm YNCA zone write",
                MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                AppendResult("Write canceled. No command sent.");
                return;
            }
            switch (key)
            {
                case "zone-set-power":
                    if (string.Equals(input, "On", StringComparison.OrdinalIgnoreCase))
                        await _receiver.ZoneControl.SetPowerAsync(zone, true);
                    else if (string.Equals(input, "Standby", StringComparison.OrdinalIgnoreCase))
                        await _receiver.ZoneControl.SetPowerAsync(zone, false);
                    else throw new ArgumentException("Use On or Standby for power.");
                    break;
                case "zone-set-mute":
                    if (string.Equals(input, "On", StringComparison.OrdinalIgnoreCase))
                        await _receiver.ZoneControl.SetMuteAsync(zone, true);
                    else if (string.Equals(input, "Off", StringComparison.OrdinalIgnoreCase))
                        await _receiver.ZoneControl.SetMuteAsync(zone, false);
                    else throw new ArgumentException("Use On or Off for mute.");
                    break;
                case "zone-set-volume":
                    decimal db;
                    if (!decimal.TryParse(input, NumberStyles.AllowLeadingSign |
                        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out db))
                        throw new ArgumentException("Use an invariant decimal volume in dB (e.g. -36.5).");
                    await _receiver.ZoneControl.SetVolumeDbAsync(zone, db);
                    break;
                case "zone-set-input":
                    await _receiver.ZoneControl.SetInputAsync(zone, input);
                    break;
                default: throw new ArgumentException("Unknown zone write action.");
            }
            AppendResult("Readback observed the requested value. YNCA unsolicited updates " +
                "cannot be distinguished from GET responses with absolute certainty.");
            AppendResult(FormatZone(_receiver.ZoneControl.GetStates()
                .Values.FirstOrDefault(x => string.Equals(x.NativeId, zone, StringComparison.OrdinalIgnoreCase))));
        }

        private static string FormatZone(YamahaZoneState zone)
        {
            if (zone == null) return "No zone value observed.";
            return "Zone " + zone.NativeId + " | power=" +
                (zone.IsPoweredOn.HasValue ? (zone.IsPoweredOn.Value ? "On" : "Standby") : "unknown") +
                " | volume=" + (zone.VolumeDb.HasValue
                    ? zone.VolumeDb.Value.ToString(CultureInfo.InvariantCulture) + " dB" : "unknown") +
                " | mute=" + (zone.IsMuted.HasValue ? zone.IsMuted.Value.ToString() : "unknown") +
                " | input=" + Show(zone.InputNativeId) + (zone.IsStale ? " [STALE]" : "");
        }

        private async Task DisplayYncaGetAsync(string subunit, string function)
        {
            if (_receiver.Ynca == null)
            {
                AppendResult("YNCA is disabled in receiver options.");
                return;
            }
            try
            {
                YamahaYncaMessage observed = await _receiver.Ynca.GetAsync(subunit, function);
                AppendResult("YNCA observed " + observed.Raw +
                    " (a matching unsolicited update cannot be distinguished from a GET response)");
            }
            catch (Exception ex)
            {
                AppendResult("YNCA " + subunit + ":" + function + " failed: " + ex.Message);
            }
        }

        private string BuildSummary()
        {
            var info = _receiver.DeviceInfo;
            var features = _receiver.DeviceFeatures;
            var status = _receiver.FunctionStatus;
            return "Receiver: " + Show(_receiver.Model) +
                "\nFirmware: " + Show(info == null ? null : info.SystemVersion) +
                "\nExtended Control API: " + Show(info == null ? null : info.ApiVersion) +
                "\nReported zones: " + Show(features == null ? (int?)null : features.ReportedZoneCount) +
                "\nSpeaker patterns: " + Show(features == null ? (int?)null : features.SpeakerPatternCount) +
                "\nActive pattern (Extended): " + Show(status == null ? (int?)null : status.ActiveSpeakerPattern) +
                "\nConnection state: " + _receiver.ConnectionState +
                "\nYNCA TCP connected: " + (_receiver.Ynca != null && _receiver.Ynca.IsConnected) +
                "\nProtocols: " + string.Join(", ", _receiver.Protocols.Values
                    .OrderBy(x => x.Protocol).Select(x => x.Protocol + "=" + x.Availability)) +
                "\n\nSpeaker settings are addressable values, not proof of configured speakers.";
        }

        private string BuildZones()
        {
            var features = _receiver.DeviceFeatures;
            if (features == null)
                return "Extended Control features unavailable. Observed YNCA zones: " +
                    string.Join(", ", _receiver.Zones.Select(x => x.NativeId)) +
                    ". Inputs, scenes and writable ranges remain unknown.";
            var sb = new StringBuilder();
            foreach (var zone in features.Zones)
            {
                sb.AppendLine(zone.NativeId + " | scenes: " + Show(zone.SceneCount));
                sb.AppendLine("  Inputs: " + string.Join(", ", zone.Inputs));
                sb.AppendLine("  Sound programs: " + string.Join(", ", zone.SoundPrograms));
            }
            return sb.ToString();
        }

        private string BuildProtocols()
        {
            var sb = new StringBuilder();
            foreach (var probe in _receiver.Protocols.Values.OrderBy(x => x.Protocol))
                sb.AppendLine(probe.Protocol + " | " + probe.Availability +
                    " | " + (probe.Detail ?? "not probed"));
            sb.AppendLine("Unavailable is not proof of permanent lack of support.");
            return sb.ToString();
        }

        private string BuildCapabilities()
        {
            var sb = new StringBuilder();
            foreach (var cap in _receiver.Capabilities.OrderBy(x => x.FeatureId))
                sb.AppendLine(cap.FeatureId + " | " + cap.Status + " | read " +
                    Permission(cap.CanRead) + " | write " + Permission(cap.CanWrite) +
                    " | execute " + Permission(cap.CanExecute) + " | " + cap.Evidence);
            return sb.ToString();
        }

        private string BuildFunctionRoutes()
        {
            var sb = new StringBuilder();
            var description = _receiver.LegacyDescriptor;
            if (description == null)
                sb.AppendLine("Legacy description: unavailable / not verified");
            else
            {
                sb.AppendLine("Legacy description: " + description.ResourcePath +
                    " | root: " + description.RootElement +
                    " | command elements: " + description.CommandElementCount);
                sb.AppendLine("Descriptor entries are incomplete and do not authorize GET or PUT.");
                if (description.CommandIdentifiers.Count > 0)
                    sb.AppendLine("Native command identifiers: " +
                        string.Join(", ", description.CommandIdentifiers));
            }
            var diagnostic = _receiver.LegacyDescriptorDiagnostic;
            if (diagnostic != null)
                foreach (var attempt in diagnostic.Attempts)
                    sb.AppendLine("  " + attempt.Path + " | " + attempt.Status +
                        (attempt.HttpStatusCode.HasValue ? " | HTTP " + attempt.HttpStatusCode : "") +
                        " | " + attempt.Detail);
            sb.AppendLine();
            sb.AppendLine("Function routes (implemented / advertised / verified; evidence only):");
            foreach (var route in _receiver.FunctionRoutes.OrderBy(x => x.FunctionId)
                .ThenBy(x => x.Protocol))
                sb.AppendLine(route.FunctionId + " | " + route.Protocol + " | " +
                    route.NativeIdentifier + " | " + route.Status +
                    " | impl R/W/E=" + (route.ImplementedRead ? "Y" : "-") + "/" +
                    (route.ImplementedWrite ? "Y" : "-") + "/" +
                    (route.ImplementedExecute ? "Y" : "-") +
                    " | verified R/W/E=" + Permission(route.CanRead) + "/" +
                    Permission(route.CanWrite) + "/" + Permission(route.CanExecute) +
                    " | " + route.ReportedBy);
            sb.AppendLine("A route does not grant write permission or guarantee a typed operation.");
            return sb.ToString();
        }

        private static string FormatChannels(string kind, int pattern,
            IReadOnlyDictionary<string, YamahaSpeakerValue> values)
        {
            var sb = new StringBuilder();
            sb.AppendLine(kind + " for Pattern " + pattern + " (" + values.Count + " channels)");
            foreach (var kv in values.OrderBy(x => x.Key, StringComparer.Ordinal))
                sb.AppendLine(kv.Key.PadRight(24) + " " +
                    kv.Value.Value.ToString(CultureInfo.InvariantCulture) + " " + kv.Value.Unit +
                    "  [Val=" + kv.Value.RawValue.ToString(CultureInfo.InvariantCulture) +
                    ", Exp=" + kv.Value.Exponent + "]");
            return sb.ToString();
        }

        private static string Permission(bool? permission)
        {
            return !permission.HasValue ? "Unknown" : permission.Value ? "Yes" : "No";
        }

        private static string Show(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string Show(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";
        }

        private void BeginOperation()
        {
            if (ChkClearBeforeOperation.IsChecked == true)
                ClearOutputs();
            _busy = true;
            UpdateControls();
        }

        private void EndOperation()
        {
            _busy = false;
            UpdateControls();
        }

        private void UpdateControls()
        {
            if (BtnConnect == null || BtnSend == null)
                return;
            BtnConnect.IsEnabled = !_busy;
            BtnRefresh.IsEnabled = !_busy && _receiver != null;
            BtnDisconnect.IsEnabled = !_busy && _receiver != null;
            BtnSend.IsEnabled = !_busy && _receiver != null && LstCommands.SelectedItem != null;
            TxtHost.IsEnabled = !_busy;
            TxtPort.IsEnabled = !_busy;
            TxtYncaPort.IsEnabled = !_busy && _receiver == null;
            BtnYncaConnect.IsEnabled = !_busy && _receiver != null &&
                _receiver.Ynca != null && !_receiver.Ynca.IsConnected;
            BtnYncaDisconnect.IsEnabled = !_busy && _receiver != null &&
                _receiver.Ynca != null && _receiver.Ynca.IsConnected;
            TxtYncaSubunit.IsEnabled = !_busy;
            TxtYncaFunction.IsEnabled = !_busy;
            LstCommands.IsEnabled = !_busy;
            TxtPattern.IsEnabled = !_busy;
            TxtWriteValue.IsEnabled = !_busy;
            TxtZone.IsEnabled = !_busy;
            ChkEnableVerifiedWrites.IsEnabled = !_busy && _receiver == null;
            ChkEnableYncaWrites.IsEnabled = !_busy && _receiver == null;
            ChkEnableSoundWrites.IsEnabled = !_busy && _receiver == null;
        }

        private void AppendResult(string message)
        {
            TxtResult.AppendText(message + Environment.NewLine + Environment.NewLine);
            TxtResult.ScrollToEnd();
        }

        private void AppendHttp(string message)
        {
            // These are operation summaries, not captured raw network traffic.
            TxtHttpOutput.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                " " + message + Environment.NewLine);
            TxtHttpOutput.ScrollToEnd();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearOutputs();
        }

        private void ClearOutputs()
        {
            TxtResult.Clear();
            TxtHttpOutput.Clear();
            TxtYncaOutput.Clear();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_receiver != null)
            {
                if (_receiver.Ynca != null)
                {
                    _receiver.Ynca.MessageReceived -= ReceiverYnca_MessageReceived;
                    _receiver.Ynca.ConnectionChanged -= ReceiverYnca_ConnectionChanged;
                }
                _receiver.Dispose();
            }
        }

        private sealed class TesterCommand
        {
            internal TesterCommand(string group, string name, string key, bool requiresPattern = false,
                bool requiresValue = false)
            {
                Group = group;
                Name = name;
                Key = key;
                RequiresPattern = requiresPattern;
                RequiresValue = requiresValue;
            }

            public string Group { get; private set; }
            public string Name { get; private set; }
            public string Key { get; private set; }
            public bool RequiresPattern { get; private set; }
            public bool RequiresValue { get; private set; }
        }
    }
}
