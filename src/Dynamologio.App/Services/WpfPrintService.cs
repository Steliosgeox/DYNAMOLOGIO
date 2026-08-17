using System;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Dynamologio.App.Services
{
    public class WpfPrintService : IPrintService
    {
        public bool PrintDocument(object document, string title)
        {
            if (document is FlowDocument doc)
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Calculate and set page size if needed, based on current implementation
                    doc.PageHeight = printDialog.PrintableAreaHeight;
                    doc.PageWidth = printDialog.PrintableAreaWidth;
                    doc.PagePadding = new System.Windows.Thickness(50);
                    doc.ColumnGap = 0;
                    doc.ColumnWidth = printDialog.PrintableAreaWidth;

                    var dps = (IDocumentPaginatorSource)doc;
                    printDialog.PrintDocument(dps.DocumentPaginator, title);
                    return true;
                }
            }
            return false;
        }
    }
}
