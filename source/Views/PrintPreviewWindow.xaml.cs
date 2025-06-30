using System.IO; // Required for MemoryStream
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Xps.Packaging; // Required for XpsDocument

namespace AD_User_Reset_Print.Views
{
    public partial class PrintPreviewWindow : Window
    {
        private readonly FlowDocument _documentToPrint;

        public PrintPreviewWindow(FlowDocument document)
        {
            InitializeComponent();
            _documentToPrint = document;
            LoadDocumentForPreview();
        }

        private void LoadDocumentForPreview()
        {
            // The DocumentViewer needs an IDocumentPaginatorSource or an XPS Document
            // FlowDocument is IDocumentPaginatorSource, so we can directly assign its DocumentPaginator
            documentViewer.Document = _documentToPrint;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();

            if (printDialog.ShowDialog() == true)
            {
                // Set page size for the FlowDocument based on the printer's printable area
                _documentToPrint.PageWidth = printDialog.PrintableAreaWidth;
                _documentToPrint.PageHeight = printDialog.PrintableAreaHeight;

                // Adjust PagePadding to account for the non-printable margins (imageable area origin)
                // This logic is crucial for accurate printing from preview
                PrintCapabilities capabilities = printDialog.PrintQueue.GetPrintCapabilities(printDialog.PrintTicket);
                _documentToPrint.PagePadding = new Thickness(
                    capabilities.PageImageableArea.OriginWidth,
                    capabilities.PageImageableArea.OriginHeight,
                    printDialog.PrintableAreaWidth - (capabilities.PageImageableArea.OriginWidth + capabilities.PageImageableArea.ExtentWidth),
                    printDialog.PrintableAreaHeight - (capabilities.PageImageableArea.OriginHeight + capabilities.PageImageableArea.ExtentHeight)
                );

                IDocumentPaginatorSource idpSource = _documentToPrint;
                printDialog.PrintDocument(idpSource.DocumentPaginator, "Instructions utilisateur");
            }
        }
    }
}