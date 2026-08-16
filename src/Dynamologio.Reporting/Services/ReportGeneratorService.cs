using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Dynamologio.ImportExport.Excel;

namespace Dynamologio.Reporting.Services
{
    public enum ReportType
    {
        DailyDynamologio = 1,
        AbsentPersonnel = 2,
        PresentPersonnel = 3,
        ServiceRoster = 4
    }

    public class ReportGenerationRequest
    {
        public ReportType Type { get; set; } = ReportType.DailyDynamologio;
        public DateTime AsOfTimestamp { get; set; } = DateTime.Today;
        public Guid? OrganisationUnitId { get; set; }
        public string UnitTitle { get; set; } = "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ";
        public string CustomTemplatePath { get; set; }
    }

    public interface IReportGeneratorService
    {
        UnitStrengthSnapshot PrepareSnapshot(ReportGenerationRequest request);
        string ExportToExcel(ReportGenerationRequest request, string outputFilePath);
        FlowDocument GeneratePrintableDocument(ReportGenerationRequest request);
    }

    public class ReportGeneratorService : IReportGeneratorService
    {
        private readonly IUnitOfWork _uow;
        private readonly IStrengthCalculator _strengthCalculator;
        private readonly IExcelTemplateWriter _templateWriter;

        public ReportGeneratorService(
            IUnitOfWork uow,
            IStrengthCalculator strengthCalculator,
            IExcelTemplateWriter templateWriter = null)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _strengthCalculator = strengthCalculator ?? throw new ArgumentNullException(nameof(strengthCalculator));
            _templateWriter = templateWriter ?? new NpoiTemplateWriter();
        }

        public UnitStrengthSnapshot PrepareSnapshot(ReportGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var personnel = _uow.Personnel.GetAll();
            var statusEvents = _uow.StatusEvents.GetAll();
            var statusTypes = _uow.StatusTypes.GetAll();
            var ranks = _uow.Ranks.GetAll();
            var units = _uow.OrganisationUnits.GetAll();
            var services = _uow.ServiceAssignments.GetAll();
            var serviceTypes = _uow.ServiceTypes.GetAll();

            return _strengthCalculator.CalculateSnapshot(
                personnel,
                statusEvents,
                statusTypes,
                ranks,
                units,
                services,
                serviceTypes,
                request.AsOfTimestamp,
                request.OrganisationUnitId);
        }

        public string ExportToExcel(ReportGenerationRequest request, string outputFilePath)
        {
            var snapshot = PrepareSnapshot(request);

            string templatePath = request.CustomTemplatePath;
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                // Fallback to default reference template
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (string.IsNullOrEmpty(appData)) appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                templatePath = Path.Combine(appData, "Dynamologio", "Templates", "Standard_Dynamologio_Template.xlsx");

                if (!File.Exists(templatePath))
                {
                    GoldenTemplateGenerator.GenerateDefaultGoldenTemplate(templatePath);
                }
            }

            _templateWriter.GenerateDynamologioWorkbook(templatePath, outputFilePath, snapshot, request.UnitTitle);
            return outputFilePath;
        }

        public FlowDocument GeneratePrintableDocument(ReportGenerationRequest request)
        {
            var snapshot = PrepareSnapshot(request);
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI, Arial"),
                FontSize = 12,
                PagePadding = new Thickness(40),
                ColumnWidth = 800
            };

            // Title
            var titleParagraph = new Paragraph(new Run(request.UnitTitle))
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            doc.Blocks.Add(titleParagraph);

            var subTitleParagraph = new Paragraph(new Run($"ΗΜΕΡΗΣΙΟ ΔΥΝΑΜΟΛΟΓΙΟ - {snapshot.AsOfTimestamp:dd/MM/yyyy}"))
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            doc.Blocks.Add(subTitleParagraph);

            // Strength Summary Table
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
            for (int i = 0; i < 4; i++) table.Columns.Add(new TableColumn());

            var rowGroup = new TableRowGroup();

            // Header Row
            var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(230, 235, 245)) };
            headerRow.Cells.Add(CreateTableCell("ΚΑΤΗΓΟΡΙΑ", true));
            headerRow.Cells.Add(CreateTableCell("ΥΠΑΡΧΟΥΣΑ ΔΥΝΑΜΗ", true));
            headerRow.Cells.Add(CreateTableCell("ΠΑΡΟΝΤΕΣ", true));
            headerRow.Cells.Add(CreateTableCell("ΑΠΟΝΤΕΣ", true));
            rowGroup.Rows.Add(headerRow);

            // Data Rows
            rowGroup.Rows.Add(CreateDataRow("ΣΤΕΛΕΧΗ", snapshot.OfficersAndNcosActive, snapshot.OfficersAndNcosPresent, snapshot.OfficersAndNcosAbsent));
            rowGroup.Rows.Add(CreateDataRow("ΟΠΛΙΤΕΣ", snapshot.ConscriptsActive, snapshot.ConscriptsPresent, snapshot.ConscriptsAbsent));

            // Totals Row
            var totalRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)) };
            totalRow.Cells.Add(CreateTableCell("ΓΕΝΙΚΟ ΣΥΝΟΛΟ", true));
            totalRow.Cells.Add(CreateTableCell(snapshot.TotalActiveStrength.ToString(), true));
            totalRow.Cells.Add(CreateTableCell(snapshot.TotalPresent.ToString(), true));
            totalRow.Cells.Add(CreateTableCell(snapshot.TotalAbsent.ToString(), true));
            rowGroup.Rows.Add(totalRow);

            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);

            // Absent list section
            if (snapshot.AbsentPersonnel.Count > 0)
            {
                var absentHeading = new Paragraph(new Run("\nΟΝΟΜΑΣΤΙΚΗ ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ"))
                {
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 16, 0, 8)
                };
                doc.Blocks.Add(absentHeading);

                var absentTable = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
                for (int i = 0; i < 6; i++) absentTable.Columns.Add(new TableColumn());

                var absentGroup = new TableRowGroup();
                var aHeader = new TableRow { Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)) };
                aHeader.Cells.Add(CreateTableCell("Α/Α", true));
                aHeader.Cells.Add(CreateTableCell("ΒΑΘΜΟΣ", true));
                aHeader.Cells.Add(CreateTableCell("ΟΝΟΜΑΤΕΠΩΝΥΜΟ", true));
                aHeader.Cells.Add(CreateTableCell("ΑΙΤΙΟΛΟΓΙΑ", true));
                aHeader.Cells.Add(CreateTableCell("ΕΠΙΣΤΡΟΦΗ", true));
                aHeader.Cells.Add(CreateTableCell("ΔΙΑΤΑΓΗ", true));
                absentGroup.Rows.Add(aHeader);

                int idx = 1;
                foreach (var p in snapshot.AbsentPersonnel.OrderBy(x => x.Rank?.SortOrder ?? 99))
                {
                    var r = new TableRow();
                    r.Cells.Add(CreateTableCell(idx++.ToString()));
                    r.Cells.Add(CreateTableCell(p.Rank?.ShortName ?? ""));
                    r.Cells.Add(CreateTableCell(p.Person?.FullName ?? ""));
                    r.Cells.Add(CreateTableCell(p.ActiveStatusType?.Name ?? "Απουσία"));
                    r.Cells.Add(CreateTableCell(p.ReturnDisplayLabel));
                    r.Cells.Add(CreateTableCell(p.ActiveStatusEvent?.ReferenceDocument ?? "-"));
                    absentGroup.Rows.Add(r);
                }

                absentTable.RowGroups.Add(absentGroup);
                doc.Blocks.Add(absentTable);
            }

            return doc;
        }

        private static TableCell CreateTableCell(string text, bool isBold = false)
        {
            var p = new Paragraph(new Run(text))
            {
                Margin = new Thickness(4),
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = TextAlignment.Center
            };
            return new TableCell(p)
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0.5)
            };
        }

        private static TableRow CreateDataRow(string category, int active, int present, int absent)
        {
            var row = new TableRow();
            row.Cells.Add(CreateTableCell(category, false));
            row.Cells.Add(CreateTableCell(active.ToString()));
            row.Cells.Add(CreateTableCell(present.ToString()));
            row.Cells.Add(CreateTableCell(absent.ToString()));
            return row;
        }
    }
}
