using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace YamahaAvrNet.TesterGui
{
    /// <summary>
    /// Previews a locally assembled metadata report. No network operations or uploads.
    /// The receiver address is excluded unless the user checks its dedicated option.
    /// </summary>
    public partial class CompatibilityReportWindow : Window
    {
        private readonly Func<bool, string> _reportBuilder;

        internal CompatibilityReportWindow(Window owner, Func<bool, string> reportBuilder, bool german)
        {
            if (reportBuilder == null) throw new ArgumentNullException(nameof(reportBuilder));
            InitializeComponent();
            Owner = owner;
            _reportBuilder = reportBuilder;
            if (german)
            {
                Title = "Receiver-Kompatibilitätsbericht";
                TxtExplanation.Text = "Der Bericht enthält bereits lokal erfasste Geräteinformationen. " +
                    "Er sendet keine Befehle und wird nicht automatisch übertragen. " +
                    "Bitte den Inhalt vor dem Weitergeben prüfen.";
                ChkIncludeHost.Content = "Hostname/IP-Adresse aufnehmen (standardmäßig aus)";
                BtnCopy.Content = "Bericht kopieren";
                BtnSave.Content = "Als TXT speichern...";
                BtnClose.Content = "Schließen";
            }
            UpdateReport();
        }

        private void ChkIncludeHost_Changed(object sender, RoutedEventArgs e)
        {
            // Checked/Unchecked may fire during XAML initialization.
            if (_reportBuilder != null && TxtReport != null)
                UpdateReport();
        }

        private void UpdateReport()
        {
            TxtReport.Text = _reportBuilder(ChkIncludeHost.IsChecked == true);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(TxtReport.Text); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Clipboard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                FileName = "YamahaAvrNet_CompatibilityReport.txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                AddExtension = true
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllText(dialog.FileName, TxtReport.Text, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save report", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
