using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AD_User_Reset_Print.Services;

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Logique d'interaction pour LogsWindow.xaml
    /// </summary>
    public partial class LogsWindow : Window
    {
        public LogsWindow()
        {
            InitializeComponent();
            this.Loaded += LogsWindow_Loaded;
            this.Closed += LogsWindow_Closed;
        }

        private void LogsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate with existing logs when the window opens
            txtLogDisplay.Text = string.Join("\n", LoggingService.GetLogs());
            txtLogDisplay.ScrollToEnd();

            // Subscribe to receive new logs while the window is open
            LoggingService.OnLogAdded += HandleLogAdded;
        }

        private void HandleLogAdded(string logMessage)
        {
            // This needs to run on the UI thread
            Dispatcher.Invoke(() =>
            {
                txtLogDisplay.AppendText($"\n{logMessage}");
                txtLogDisplay.ScrollToEnd();
            });
        }

        private void LogsWindow_Closed(object sender, System.EventArgs e)
        {
            // IMPORTANT: Unsubscribe to prevent memory leaks
            LoggingService.OnLogAdded -= HandleLogAdded;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtLogDisplay.Text);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}