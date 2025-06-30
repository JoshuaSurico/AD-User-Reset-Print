using AD_User_Reset_Print.Models;
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

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Logique d'interaction pour SingleAccountDetails.xaml
    /// </summary>
    public partial class SingleAccountDetails : Window
    {
        // Parameterless constructor (good practice to keep for XAML designer)
        public SingleAccountDetails()
        {
            InitializeComponent();
        }

        // Constructor to receive the User object
        public SingleAccountDetails(User user)
        {
            InitializeComponent();
            // Set the DataContext of the window to the passed User object.
            // This allows the XAML bindings (e.g., Text="{Binding DisplayName}") to work.
            this.DataContext = user;

            // You can still access individual controls if needed, but binding is preferred.
            // Example:
            // if (user != null)
            // {
            //     lblDomain.Content = $"Domain: {user.Domain}";
            //     txtDisplayName.Text = user.DisplayName;
            //     // ... and so on
            // }
        }
    }
}
