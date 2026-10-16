using System;
using System.Collections.Generic;
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
                new TesterCommand("Overview", "Refresh function status", "refresh"),
                new TesterCommand("Overview", "Available zones and sources", "zones"),
                new TesterCommand("Overview", "Capabilities and evidence", "capabilities"),
                new TesterCommand("Speaker Setup", "Read active speaker pattern", "active"),
                new TesterCommand("Speaker Setup", "Read all advertised patterns", "patterns"),
                new TesterCommand("Speaker Setup", "Read speaker levels", "levels", true),
                new TesterCommand("Speaker Setup", "Read speaker distances", "distances", true)
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
            YamahaAvrClient next = null;
            BeginOperation();
            try
            {
                next = new YamahaAvrClient(new YamahaAvrOptions
                {
                    Host = TxtHost.Text,
                    HttpPort = port,
                    EnableExtendedControl = true,
                    EnableLegacyXml = true,
                    EnableYnca = false
                });
                TxtConnectionStatus.Text = "Connecting...";
                await next.InitializeAsync();
                AppendHttp("Discovery completed: GET getDeviceInfo, getFeatures, getFuncStatus");
                var old = _receiver;
                _receiver = next;
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

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || _receiver == null)
                return;
            BeginOperation();
            try
            {
                AppendHttp("GET /YamahaExtendedControl/v1/system/getFuncStatus");
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
            if (TxtCommandHint != null)
                TxtCommandHint.Text = pattern
                    ? "Use a pattern reported by your receiver. No values are changed."
                    : "Commands read device information only.";
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
                switch (command.Key)
                {
                    case "summary":
                        AppendResult(BuildSummary());
                        break;
                    case "refresh":
                        AppendHttp("GET /YamahaExtendedControl/v1/system/getFuncStatus");
                        await _receiver.RefreshAsync();
                        AppendResult(BuildSummary());
                        break;
                    case "zones":
                        AppendResult(BuildZones());
                        break;
                    case "capabilities":
                        AppendResult(BuildCapabilities());
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

        private string BuildSummary()
        {
            var info = _receiver.DeviceInfo;
            var features = _receiver.DeviceFeatures;
            var status = _receiver.FunctionStatus;
            return "Receiver: " + Show(info == null ? null : info.ModelName) +
                "\nFirmware: " + Show(info == null ? null : info.SystemVersion) +
                "\nExtended Control API: " + Show(info == null ? null : info.ApiVersion) +
                "\nReported zones: " + Show(features == null ? (int?)null : features.ReportedZoneCount) +
                "\nSpeaker patterns: " + Show(features == null ? (int?)null : features.SpeakerPatternCount) +
                "\nActive pattern (Extended): " + Show(status == null ? (int?)null : status.ActiveSpeakerPattern) +
                "\nConnection state: " + _receiver.ConnectionState +
                "\n\nSpeaker settings are addressable values, not proof of configured speakers.";
        }

        private string BuildZones()
        {
            var features = _receiver.DeviceFeatures;
            if (features == null)
                return "Discover receiver features first.";
            var sb = new StringBuilder();
            foreach (var zone in features.Zones)
            {
                sb.AppendLine(zone.NativeId + " | scenes: " + Show(zone.SceneCount));
                sb.AppendLine("  Inputs: " + string.Join(", ", zone.Inputs));
                sb.AppendLine("  Sound programs: " + string.Join(", ", zone.SoundPrograms));
            }
            return sb.ToString();
        }

        private string BuildCapabilities()
        {
            var sb = new StringBuilder();
            foreach (var cap in _receiver.Capabilities.OrderBy(x => x.FeatureId))
                sb.AppendLine(cap.FeatureId + " | " + cap.Status + " | read " +
                    Permission(cap.CanRead) + " | write " + Permission(cap.CanWrite) +
                    " | " + cap.Evidence);
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
            LstCommands.IsEnabled = !_busy;
            TxtPattern.IsEnabled = !_busy;
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
            TxtYncaOutput.Text = "YNCA will be available in a later phase.";
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_receiver != null)
                _receiver.Dispose();
        }

        private sealed class TesterCommand
        {
            internal TesterCommand(string group, string name, string key, bool requiresPattern = false)
            {
                Group = group;
                Name = name;
                Key = key;
                RequiresPattern = requiresPattern;
            }

            public string Group { get; private set; }
            public string Name { get; private set; }
            public string Key { get; private set; }
            public bool RequiresPattern { get; private set; }
        }
    }
}
