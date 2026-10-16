using System.Windows;
using YamahaAvrNet;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.TesterGui
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
            {
                Host = "192.0.2.1" // Documentation-only, no network connection.
            }))
            {
                FrameworkInfoText.Text = "Bibliothek: " + YamahaAvrNetInfo.LibraryName +
                    " | Target: " + YamahaAvrNetInfo.TargetFrameworkMoniker +
                    " | Status: " + receiver.ConnectionState +
                    " | Capabilities: " + receiver.Capabilities.Count + " ermittelt";
            }
        }
    }
}
