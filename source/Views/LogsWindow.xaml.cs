﻿using AD_User_Reset_Print.Services;
using System.Windows;

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Logique d'interaction pour LogsWindow.xaml
    /// </summary>
    public partial class LogsWindow : Window
    {
        private readonly ILoggingService _logger;

        public LogsWindow(ILoggingService logger)
        {
            InitializeComponent();

            _logger = logger;

            this.Loaded += LogsWindow_Loaded;
            this.Closed += LogsWindow_Closed;
        }

        private void LogsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate with existing logs when the window opens
            txtLogDisplay.Text = string.Join("\n", _logger.GetLogs());
            txtLogDisplay.ScrollToEnd();

            // Subscribe to receive new logs while the window is open
            _logger.OnLogAdded += HandleLogAdded;
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
            _logger.OnLogAdded -= HandleLogAdded;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtLogDisplay.Text);
        }
    }
}