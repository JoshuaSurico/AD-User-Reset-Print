using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Interop;
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

            // Configure buttons based on MessageBoxButton enum (as before)
            if (buttons == MessageBoxButton.YesNo)
            {
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
            }
            else
            {
                // If you had an "OK" button in your XAML, you'd make it visible here
                // and hide Yes/No if only OK is desired. For now, we hide Yes/No if not explicitly YesNo.
                BtnYes.Visibility = Visibility.Collapsed;
                BtnNo.Visibility = Visibility.Collapsed;
            }

            // Set icon using System.Drawing.SystemIcons and convert to WPF ImageSource
            ImageSource? iconSource = null;
            switch (icon)
            {
                case MessageBoxImage.Information:
                    iconSource = ConvertIconToImageSource(SystemIcons.Information);
                    break;
                case MessageBoxImage.Question:
                    iconSource = ConvertIconToImageSource(SystemIcons.Question);
                    break;
                case MessageBoxImage.Warning:
                    iconSource = ConvertIconToImageSource(SystemIcons.Warning);
                    break;
                case MessageBoxImage.Error:
                    iconSource = ConvertIconToImageSource(SystemIcons.Error);
                    break;
                case MessageBoxImage.None:
                default:
                    // If no specific icon or MessageBoxImage.None, ensure it's hidden
                    IconImage.Visibility = Visibility.Collapsed;
                    break;
            }

            if (iconSource != null)
            {
                IconImage.Source = iconSource;
                IconImage.Visibility = Visibility.Visible; // Make sure the Image control is visible
            }
            else
            {
                IconImage.Visibility = Visibility.Collapsed; // Hide if no valid icon was loaded
            }
        }

        // Helper method to convert System.Drawing.Icon to System.Windows.Media.ImageSource
        private ImageSource ConvertIconToImageSource(Icon icon)
        {
            if (icon == null) return null;

            // This is the key part: converting the GDI+ Icon handle to a WPF BitmapSource.
            // Int32Rect.Empty means use the entire icon.
            // BitmapSizeOptions.FromEmptyOptions() uses default size options.
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()
            );
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true; // Simulates MessageBoxResult.Yes
            this.Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Simulates MessageBoxResult.No
            this.Close();
        }
    }
}