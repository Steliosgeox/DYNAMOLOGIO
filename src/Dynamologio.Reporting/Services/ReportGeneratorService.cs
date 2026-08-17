using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    public enum TemplateVerificationStatus
    {
        Verified,
        Missing,
        ShaMismatch,
        Unmapped
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
        TemplateVerificationStatus CheckTemplateStatus(out string templatePath, out string expectedSha, out string actualSha);
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

        public TemplateVerificationStatus CheckTemplateStatus(out string templatePath, out string expectedSha, out string actualSha)
        {
            expectedSha = string.Empty;
            actualSha = string.Empty;
            templatePath = string.Empty;

            var tpl = _uow.ReportTemplates.Find(t => t.IsActive).FirstOrDefault();
            if (tpl != null && !string.IsNullOrWhiteSpace(tpl.Sha256Hash))
            {
                expectedSha = tpl.Sha256Hash;
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrEmpty(appData)) appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            templatePath = Path.Combine(appData, "Dynamologio", "Templates", "Standard_Dynamologio_Template.xlsx");

            if (!File.Exists(templatePath))
            {
                string localRef = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates-reference", "Standard_Dynamologio_Template.xlsx");
                if (File.Exists(localRef))
                {
                    templatePath = localRef;
                }
                else
                {
                    return TemplateVerificationStatus.Missing;
                }
            }

            actualSha = ComputeSha256(templatePath);

            if (!string.IsNullOrEmpty(expectedSha) && !string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                return TemplateVerificationStatus.ShaMismatch;
            }

            return TemplateVerificationStatus.Verified;
        }

        public string ExportToExcel(ReportGenerationRequest request, string outputFilePath)
        {
            var status = CheckTemplateStatus(out string templatePath, out string expectedSha, out string actualSha);
            if (status == TemplateVerificationStatus.Missing)
            {
                throw new InvalidOperationException("Δεν βρέθηκε αρχείο επίσημου προτύπου (Missing template).");
            }
            if (status == TemplateVerificationStatus.ShaMismatch)
            {
                throw new InvalidOperationException($"Ασυμφωνία SHA-256 προτύπου Excel. Αναμενόμενο: {expectedSha}, Πραγματικό: {actualSha}");
            }

            var snapshot = PrepareSnapshot(request);

            switch (request.Type)
            {
                case ReportType.DailyDynamologio:
                    _templateWriter.GenerateDynamologioWorkbook(templatePath, outputFilePath, snapshot, request.UnitTitle);
                    break;
                case ReportType.AbsentPersonnel:
                    _templateWriter.GenerateDynamologioWorkbook(templatePath, outputFilePath, snapshot, $"{request.UnitTitle} - ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ");
                    break;
                case ReportType.PresentPersonnel:
                    _templateWriter.GenerateDynamologioWorkbook(templatePath, outputFilePath, snapshot, $"{request.UnitTitle} - ΚΑΤΑΣΤΑΣΗ ΠΑΡΟΝΤΩΝ");
                    break;
                case ReportType.ServiceRoster:
                    _templateWriter.GenerateDynamologioWorkbook(templatePath, outputFilePath, snapshot, $"{request.UnitTitle} - ΠΡΟΓΡΑΜΜΑ ΥΠΗΡΕΣΙΩΝ");
                    break;
                default:
                    _templateWriter.GenerateDynamologioWorkbook(templatePath, outputFilePath, snapshot, request.UnitTitle);
                    break;
            }

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

            // Header Title
            doc.Blocks.Add(new Paragraph(new Run(request.UnitTitle))
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            string reportTitle = request.Type switch
            {
                ReportType.AbsentPersonnel => "ΟΝΟΜΑΣΤΙΚΗ ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ",
                ReportType.PresentPersonnel => "ΟΝΟΜΑΣΤΙΚΗ ΚΑΤΑΣΤΑΣΗ ΠΑΡΟΝΤΩΝ",
                ReportType.ServiceRoster => "ΗΜΕΡΗΣΙΟ ΠΡΟΓΡΑΜΜΑ ΥΠΗΡΕΣΙΩΝ",
                _ => "ΗΜΕΡΗΣΙΟ ΔΥΝΑΜΟΛΟΓΙΟ"
            };

            doc.Blocks.Add(new Paragraph(new Run($"{reportTitle} - {snapshot.AsOfTimestamp:dd/MM/yyyy}"))
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Table Content
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(200) });
            table.Columns.Add(new TableColumn { Width = new GridLength(150) });
            table.Columns.Add(new TableColumn { Width = new GridLength(120) });

            var rowGroup = new TableRowGroup();
            var headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Βαθμός")) { FontWeight = FontWeights.Bold }));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Ονοματεπώνυμο")) { FontWeight = FontWeights.Bold }));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Υπομονάδα/Λόχος")) { FontWeight = FontWeights.Bold }));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Κατάσταση")) { FontWeight = FontWeights.Bold }));
            rowGroup.Rows.Add(headerRow);

            var listToDisplay = request.Type == ReportType.AbsentPersonnel
                ? snapshot.AbsentPersonnel
                : snapshot.PresentPersonnel;

            foreach (var p in listToDisplay.Take(50))
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(p.Rank?.ShortName ?? "-"))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(p.Person?.FullName ?? "-"))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(p.Unit?.Name ?? "-"))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(p.StatusDisplayLabel ?? "-"))));
                rowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);

            return doc;
        }

        private static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
