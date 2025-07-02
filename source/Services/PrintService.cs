using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Views;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AD_User_Reset_Print.Services
{
    public static class PrintService
    {
        public static FlowDocument CreatePrintDocument(User user, string tempPassword)
        {
            string fullName = user.DisplayName;
            string username = user.SAMAccountName;

            var doc = new FlowDocument
            {
                PagePadding = new Thickness(50), // Standard A4-like padding
                ColumnWidth = 800 // Prevents text from wrapping too early
            };

            // --- 1. Add Watermark ---
            // A watermark is best added as a background brush to the document itself.
            var watermark = new TextBlock
            {
                Text = "CONFIDENTIEL",
                FontSize = 80,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Gray) { Opacity = 0.15 },
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-45)
            };

            doc.Background = new VisualBrush(watermark)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
                TileMode = TileMode.None
            };


            // --- 2. Title ---
            var title = new Paragraph(new Bold(new Run("Instructions de Compte Utilisateur")))
            {
                FontSize = 24,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            doc.Blocks.Add(title);

            // --- 3. User Details ---
            var userDetails = new Paragraph
            {
                FontSize = 14,
                LineHeight = 28 // Add some space between lines
            };
            userDetails.Inlines.Add(new Run("Compte pour : "));
            userDetails.Inlines.Add(new Bold(new Run(fullName)));
            userDetails.Inlines.Add(new LineBreak());
            userDetails.Inlines.Add(new Run("Nom d'utilisateur : "));
            userDetails.Inlines.Add(new Bold(new Run(username)));
            userDetails.Inlines.Add(new LineBreak());
            userDetails.Inlines.Add(new Run("Mot de passe temporaire : "));
            userDetails.Inlines.Add(new Bold(new Run(tempPassword)));
            doc.Blocks.Add(userDetails);

            // --- 4. Separator Line ---
            var separator = new BlockUIContainer(new Rectangle
            {
                Height = 1,
                Fill = Brushes.Black,
                Margin = new Thickness(0, 20, 0, 20)
            });
            doc.Blocks.Add(separator);

            // --- 5. Important Instructions Section ---
            var instructionsTitle = new Paragraph(new Bold(new Run("Instructions importantes :")))
            {
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            };
            doc.Blocks.Add(instructionsTitle);

            var instructionsList = new List
            {
                MarkerStyle = TextMarkerStyle.Decimal,
                Padding = new Thickness(20, 0, 0, 0) // Indent the list
            };

            instructionsList.ListItems.Add(new ListItem(new Paragraph(new Run("Votre mot de passe temporaire est sensible à la casse."))));
            instructionsList.ListItems.Add(new ListItem(new Paragraph(new Run("Il vous sera demandé de modifier votre mot de passe lors de votre première connexion."))));
            instructionsList.ListItems.Add(new ListItem(new Paragraph(new Run("Veuillez vous assurer de choisir un mot de passe fort et unique."))));
            instructionsList.ListItems.Add(new ListItem(new Paragraph(new Run("Conservez ce document dans un endroit sûr jusqu'à ce que votre mot de passe soit modifié."))));
            instructionsList.ListItems.Add(new ListItem(new Paragraph(new Run("Pour obtenir de l'aide, contactez le support informatique à [Vos informations de contact IT]."))));

            doc.Blocks.Add(instructionsList);

            // --- 6. Second Separator ---
            var separator2 = new BlockUIContainer(new Rectangle
            {
                Height = 1,
                Fill = Brushes.Black,
                Margin = new Thickness(0, 20, 0, 20)
            });
            doc.Blocks.Add(separator2);

            // --- 7. Print Date Footer ---
            var printDate = new Paragraph(new Run($"Date d'impression : {DateTime.Now:dd.MM.yyyy HH:mm}"))
            {
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                TextAlignment = TextAlignment.Left
            };
            doc.Blocks.Add(printDate);

            return doc;
        }

        /// <summary>
        /// Shows a print preview window for the user instructions.
        /// </summary>
        public static void ShowPrintPreview(User user, string tempPassword)
        {
            FlowDocument doc = CreatePrintDocument(user, tempPassword);
            if (doc == null) return;

            PrintPreviewWindow previewWindow = new PrintPreviewWindow(doc);
            previewWindow.ShowDialog();
        }

        /// <summary>
        /// Directly sends user instructions to the printer, opening the PrintDialog.
        /// </summary>
        public static void CreatePrintDocumentDirect(User user, string tempPassword)
        {
            FlowDocument doc = CreatePrintDocument(user, tempPassword);
            if (doc == null) return;

            try
            {
                PrintDialog printDialog = new PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    PrintCapabilities capabilities = printDialog.PrintQueue.GetPrintCapabilities(printDialog.PrintTicket);

                    // Set page size for the FlowDocument based on the printer's printable area
                    doc.PageWidth = printDialog.PrintableAreaWidth;
                    doc.PageHeight = printDialog.PrintableAreaHeight;

                    // Adjust PagePadding to account for the non-printable margins (imageable area origin)
                    doc.PagePadding = new Thickness(
                        capabilities.PageImageableArea.OriginWidth,
                        capabilities.PageImageableArea.OriginHeight,
                        printDialog.PrintableAreaWidth - (capabilities.PageImageableArea.OriginWidth + capabilities.PageImageableArea.ExtentWidth),
                        printDialog.PrintableAreaHeight - (capabilities.PageImageableArea.OriginHeight + capabilities.PageImageableArea.ExtentHeight)
                    );

                    IDocumentPaginatorSource idpSource = doc;
                    printDialog.PrintDocument(idpSource.DocumentPaginator, $"Instructions utilisateur pour {user.DisplayName}");
                    MessageBox.Show("Tâche d'impression envoyée.", "Confirmation d'impression", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'impression : {ex.Message}", "Erreur d'impression", MessageBoxButton.OK, MessageBoxImage.Error);
                // Log the exception
            }
        }
    }
}