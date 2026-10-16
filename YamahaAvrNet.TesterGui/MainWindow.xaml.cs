using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AvrNet.Abstractions.Models;
using YamahaAvrNet.Models;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.TesterGui
{
    /// <summary>Manual UI only. Receiver networking remains in YamahaAvrNet.</summary>
    public partial class MainWindow : Window
    {
        private YamahaAvrClient _receiver;
        private bool _busy;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            int port;
            if (!int.TryParse(PortTextBox.Text, NumberStyles.None,
                CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
            {
                MessageText.Text = "Enter a valid HTTP port (1-65535).";
                return;
            }

            YamahaAvrClient next = null;
            SetBusy(true);
            try
            {
                next = new YamahaAvrClient(new YamahaAvrOptions
                {
                    Host = HostTextBox.Text,
                    HttpPort = port,
                    EnableExtendedControl = true,
                    EnableLegacyXml = false,
                    EnableYnca = false
                });
                ConnectionStatusText.Text = "Discovering receiver...";
                MessageText.Text = "Reading device info, advertised features and current function status.";
                await next.InitializeAsync();

                var previous = _receiver;
                _receiver = next;
                next = null;
                if (previous != null)
                    previous.Dispose();

                ShowSnapshot();
                MessageText.Text = "Discovery completed using read-only requests.";
            }
            catch (Exception ex)
            {
                MessageText.Text = "Discovery failed: " + ex.Message;
                ConnectionStatusText.Text = _receiver == null
                    ? "Disconnected" : _receiver.ConnectionState.ToString();
            }
            finally
            {
                if (next != null)
                    next.Dispose();
                SetBusy(false);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || _receiver == null)
                return;
            SetBusy(true);
            try
            {
                ConnectionStatusText.Text = "Refreshing status...";
                await _receiver.RefreshAsync();
                ShowSnapshot();
                MessageText.Text = "Current system function status refreshed.";
            }
            catch (Exception ex)
            {
                ConnectionStatusText.Text = _receiver.ConnectionState.ToString();
                MessageText.Text = "Refresh failed: " + ex.Message;
            }
            finally { SetBusy(false); }
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || _receiver == null)
                return;
            SetBusy(true);
            try
            {
                await _receiver.DisconnectAsync();
                _receiver.Dispose();
                _receiver = null;
                ConnectionStatusText.Text = "Disconnected";
                MessageText.Text = "The previous device snapshot is no longer live.";
            }
            catch (Exception ex)
            {
                MessageText.Text = "Disconnect failed: " + ex.Message;
            }
            finally { SetBusy(false); }
        }

        private void ShowSnapshot()
        {
            if (_receiver == null)
                return;

            var info = _receiver.DeviceInfo;
            var features = _receiver.DeviceFeatures;
            var status = _receiver.FunctionStatus;

            ConnectionStatusText.Text = _receiver.ConnectionState.ToString();
            ModelText.Text = info == null || string.IsNullOrWhiteSpace(info.ModelName)
                ? "Model not reported" : info.ModelName;
            VersionText.Text = "Firmware: " + Show(info == null ? null : info.SystemVersion) +
                " | Extended Control API: " + Show(info == null ? null : info.ApiVersion);
            PatternText.Text = "Speaker patterns: " +
                (features == null ? "unknown" : Show(features.SpeakerPatternCount)) +
                " | Active pattern: " + (status == null ? "unknown" : Show(status.ActiveSpeakerPattern));

            var zoneRows = new List<ZoneDisplayRow>();
            if (features != null)
            {
                foreach (var zone in features.Zones)
                {
                    YamahaExtendedNumericRange volume;
                    zone.Ranges.TryGetValue("actual_volume_db", out volume);
                    zoneRows.Add(new ZoneDisplayRow
                    {
                        NativeId = zone.NativeId,
                        InputCount = zone.Inputs.Count,
                        SoundProgramCount = zone.SoundPrograms.Count,
                        SceneCount = Show(zone.SceneCount),
                        VolumeRangeDb = FormatRange(volume)
                    });
                }
            }
            ZonesGrid.ItemsSource = zoneRows;
            CapabilitiesGrid.ItemsSource = _receiver.Capabilities
                .OrderBy(x => x.FeatureId)
                .Select(x => new CapabilityDisplayRow
                {
                    FeatureId = x.FeatureId,
                    Status = x.Status.ToString(),
                    CanRead = PermissionText(x.CanRead),
                    CanWrite = PermissionText(x.CanWrite),
                    Evidence = x.Evidence.ToString()
                }).ToArray();

            OverviewDetailsText.Text = features == null ? "No feature data available." :
                "Reported zones: " + Show(features.ReportedZoneCount) +
                "\nZones in response: " + features.Zones.Count +
                "\nSystem input definitions: " + features.SystemInputs.Count +
                "\nSystem functions: " + features.SystemFunctions.Count +
                "\nTuner presets: " + Show(features.TunerPresetCount) +
                "\nNETUSB presets: " + Show(features.NetUsbPresetCount) +
                "\n\nVolume scale (numeric) and actual_volume_db (dB) are separate parameters.";

            DiagnosticsText.Text = status == null ? "No status available." :
                "GET /YamahaExtendedControl/v1/system/getFuncStatus\n" +
                "Received at (UTC): " + status.ObservedUtc.ToString("O", CultureInfo.InvariantCulture) +
                "\nReturned keys: " + string.Join(", ", status.Values.Keys.OrderBy(x => x)) +
                "\n\nValues and raw network identifiers are intentionally not included in this view.";
        }

        private static string Show(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string Show(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";
        }

        private static string PermissionText(bool? allowed)
        {
            return !allowed.HasValue ? "Unknown" : allowed.Value ? "Yes" : "No";
        }

        private static string FormatRange(YamahaExtendedNumericRange range)
        {
            if (range == null || !range.Minimum.HasValue || !range.Maximum.HasValue)
                return "unknown";
            string s = range.Minimum.Value.ToString(CultureInfo.InvariantCulture) + " to " +
                range.Maximum.Value.ToString(CultureInfo.InvariantCulture);
            if (range.Step.HasValue)
                s += " (step " + range.Step.Value.ToString(CultureInfo.InvariantCulture) + ")";
            return s;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            ConnectButton.IsEnabled = !busy;
            RefreshButton.IsEnabled = !busy && _receiver != null;
            DisconnectButton.IsEnabled = !busy && _receiver != null;
            HostTextBox.IsEnabled = !busy;
            PortTextBox.IsEnabled = !busy;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_receiver != null)
                _receiver.Dispose();
        }
    }

    public sealed class CapabilityDisplayRow
    {
        public string FeatureId { get; set; }
        public string Status { get; set; }
        public string CanRead { get; set; }
        public string CanWrite { get; set; }
        public string Evidence { get; set; }
    }

    public sealed class ZoneDisplayRow
    {
        public string NativeId { get; set; }
        public int InputCount { get; set; }
        public int SoundProgramCount { get; set; }
        public string SceneCount { get; set; }
        public string VolumeRangeDb { get; set; }
    }
}
