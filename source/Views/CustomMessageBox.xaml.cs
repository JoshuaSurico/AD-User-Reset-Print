using MahApps.Metro.IconPacks;
using System.Windows;
using System.Windows.Media;

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Logique d'interaction pour CustomMessageBox.xaml
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            InitializeComponent();
            MessageText.Text = message;
            TitleText.Text = title;

            // Button configuration remains the same
            if (buttons == MessageBoxButton.YesNo)
            {
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
            }
            else // Hides buttons if not Yes/No (you can add an OK button later if needed)
            {
                BtnYes.Visibility = Visibility.Collapsed;
                BtnNo.Visibility = Visibility.Collapsed;
            }

            IconImage.Visibility = Visibility.Visible;
            switch (icon)
            {
                case MessageBoxImage.Information:
                    IconImage.Kind = PackIconMaterialKind.Information;
                    IconImage.Foreground = Brushes.DodgerBlue; // A nice blue for info
                    break;

                case MessageBoxImage.Question:
                    IconImage.Kind = PackIconMaterialKind.HelpCircle;
                    IconImage.Foreground = Brushes.SlateGray; // A neutral color for questions
                    break;

                case MessageBoxImage.Warning:
                    IconImage.Kind = PackIconMaterialKind.Alert;
                    IconImage.Foreground = Brushes.Orange; // A clear warning color
                    break;

                case MessageBoxImage.Error:
                    IconImage.Kind = PackIconMaterialKind.CloseCircle;
                    IconImage.Foreground = Brushes.Firebrick; // A strong red for errors
                    break;

                case MessageBoxImage.None:
                default:
                    IconImage.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Add this method if you want the window to be draggable from the border
        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}