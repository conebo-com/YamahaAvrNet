using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace YamahaAvrNet.TesterGui
{
    public partial class JsonPreviewWindow : Window
    {
        public JsonPreviewWindow(Window owner, string json)
        {
            InitializeComponent();
            Owner = owner;
            TxtJson.Text = json;
            Title = Loc.Get("Check report before sending");
            TxtHint.Text = Loc.Get($"This is the report for the compatibility check of your receiver. Please read it and make sure you are happy with its content.\n1. Click 'Copy to clipboard'.\n2. Paste the report into an e-mail to {MainWindow.EMail} and send it yourself.\nNothing is sent automatically.");
            BtnSave.Content = Loc.Get("Save as TXT...");
            BtnCopy.Content = Loc.Get("Copy to clipboard");
            BtnClose.Content = Loc.Get("Close");
            TxtMailRequest.Text = Loc.Format("Please send this report to us by e-mail: {0}", MainWindow.EMail) +
                Environment.NewLine + Loc.Get("This supports the open source project. Thank you very much!");
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs eventArgs)
        {
            try
            {
                Clipboard.SetText(TxtJson.Text);
                TxtCopyStatus.Text = Loc.Get("Copied to the clipboard.");
            }
            catch (ExternalException exception)
            {
                TxtCopyStatus.Text = string.Empty;
                MessageBox.Show(
                    this,
                    Loc.Format("The clipboard is currently not available: {0}", exception.Message) + Environment.NewLine +
                    Loc.Get("Please try again, or select the text and copy it with Ctrl+C."),
                    Loc.Get("Copy to clipboard"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs eventArgs)
        {
            var dialog = new SaveFileDialog
            {
                FileName = "YamahaAvrNet_CompatibilityReport.txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                AddExtension = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return; // <-----------
            }

            try
            {
                File.WriteAllText(dialog.FileName, TxtJson.Text, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    Loc.Get("Save report"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs eventArgs)
        {
            Close();
        }
    }
}
