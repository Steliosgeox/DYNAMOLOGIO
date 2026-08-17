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
using Newtonsoft.Json;

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
        Unmapped,
        Unverified
    }

    public class ReportGenerationRequest
    {
        public ReportType Type { get; set; } = ReportType.DailyDynamologio;
        public DateTime AsOfTimestamp { get; set; } = DateTime.Today;
        public Guid? OrganisationUnitId { get; set; }
        public string UnitTitle { get; set; } = "ΕΛΛΗΝΙΚΟΣ ΣΤΡΑΤΟΣ";
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
            if (tpl == null)
            {
                return TemplateVerificationStatus.Unverified;
            }

            if (string.IsNullOrWhiteSpace(tpl.Sha256Hash))
            {
                return TemplateVerificationStatus.Unverified;
            }

            expectedSha = tpl.Sha256Hash.Trim();

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

            if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                return TemplateVerificationStatus.ShaMismatch;
            }

            if (string.IsNullOrWhiteSpace(tpl.CellMappingJson) || tpl.CellMappingJson == "{}")
            {
                return TemplateVerificationStatus.Unmapped;
            }

            return TemplateVerificationStatus.Verified;
        }

        public string ExportToExcel(ReportGenerationRequest request, string outputFilePath)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var snapshot = PrepareSnapshot(request);
            string title = !string.IsNullOrWhiteSpace(request.UnitTitle) ? request.UnitTitle : "ΕΛΛΗΝΙΚΟΣ ΣΤΡΑΤΟΣ";

            switch (request.Type)
            {
                case ReportType.DailyDynamologio:
                    var status = CheckTemplateStatus(out string templatePath, out string expectedSha, out string actualSha);
                    if (status == TemplateVerificationStatus.Missing)
                    {
                        throw new InvalidOperationException("Δεν βρέθηκε αρχείο επίσημου προτύπου (Missing template).");
                    }
                    if (status == TemplateVerificationStatus.ShaMismatch)
                    {
                        throw new InvalidOperationException($"Ασυμφωνία SHA-256 προτύπου Excel. Αναμενόμενο: {expectedSha}, Πραγματικό: {actualSha}");
                    }
                    if (status == TemplateVerificationStatus.Unverified || status == TemplateVerificationStatus.Unmapped)
                    {
                        throw new InvalidOperationException("Το πρότυπο δεν έχει επαληθευτεί ή δεν έχει αντιστοίχιση.");
                    }

                    TemplateCellMapping mapping = null;
                    var tpl = _uow.ReportTemplates.Find(t => t.IsActive).FirstOrDefault();
                    if (tpl != null && !string.IsNullOrWhiteSpace(tpl.CellMappingJson))
                    {
                        try { mapping = JsonConvert.DeserializeObject<TemplateCellMapping>(tpl.CellMappingJson); } catch { }
                    }

                    _templateWriter.GenerateDynamologioWorkbook(templatePath, outputFilePath, snapshot, title, mapping);
                    break;

                case ReportType.AbsentPersonnel:
                    _templateWriter.GenerateAbsentWorkbook(outputFilePath, snapshot, title);
                    break;

                case ReportType.PresentPersonnel:
                    _templateWriter.GeneratePresentWorkbook(outputFilePath, snapshot, title);
                    break;

                case ReportType.ServiceRoster:
                    _templateWriter.GenerateServiceWorkbook(outputFilePath, snapshot, title);
                    break;
            }

            return outputFilePath;
        }

        public FlowDocument GeneratePrintableDocument(ReportGenerationRequest request)
        {
            var snapshot = PrepareSnapshot(request);
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(40),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            string title = !string.IsNullOrWhiteSpace(request.UnitTitle) ? request.UnitTitle : "ΕΛΛΗΝΙΚΟΣ ΣΤΡΑΤΟΣ";

            var header = new Paragraph(new Bold(new Run(title)))
            {
                FontSize = 16,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            doc.Blocks.Add(header);

            string subtitleText = request.Type switch
            {
                ReportType.DailyDynamologio => $"ΗΜΕΡΗΣΙΟ ΔΥΝΑΜΟΛΟΓΙΟ — {snapshot.AsOfTimestamp:dd/MM/yyyy}",
                ReportType.AbsentPersonnel => $"ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ ΠΡΟΣΩΠΙΚΟΥ — {snapshot.AsOfTimestamp:dd/MM/yyyy}",
                ReportType.PresentPersonnel => $"ΚΑΤΑΣΤΑΣΗ ΠΑΡΟΝΤΩΝ ΠΡΟΣΩΠΙΚΟΥ — {snapshot.AsOfTimestamp:dd/MM/yyyy}",
                ReportType.ServiceRoster => $"ΠΙΝΑΚΑΣ ΥΠΗΡΕΣΙΩΝ & ΚΑΘΗΚΟΝΤΩΝ — {snapshot.AsOfTimestamp:dd/MM/yyyy}",
                _ => $"ΑΝΑΦΟΡΑ ΔΥΝΑΜΗΣ — {snapshot.AsOfTimestamp:dd/MM/yyyy}"
            };

            var subtitle = new Paragraph(new Run(subtitleText))
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            doc.Blocks.Add(subtitle);

            var table = new Table();
            var rowGroup = new TableRowGroup();
            table.RowGroups.Add(rowGroup);

            switch (request.Type)
            {
                case ReportType.ServiceRoster:
                    table.Columns.Add(new TableColumn { Width = new GridLength(40) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(100) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(180) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(140) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(120) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(100) });

                    var headerRowSvc = new TableRow();
                    headerRowSvc.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Α/Α")))));
                    headerRowSvc.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΒΑΘΜΟΣ")))));
                    headerRowSvc.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΟΝΟΜΑΤΕΠΩΝΥΜΟ")))));
                    headerRowSvc.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΥΠΗΡΕΣΙΑ")))));
                    headerRowSvc.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΤΟΠΟΘΕΣΙΑ")))));
                    headerRowSvc.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΩΡΑΡΙΟ")))));
                    rowGroup.Rows.Add(headerRowSvc);

                    var svcPersonnel = snapshot.PresentPersonnel.Where(p => p.ActiveServiceAssignment != null).OrderBy(p => p.Rank?.SortOrder ?? 99).ToList();
                    for (int i = 0; i < svcPersonnel.Count; i++)
                    {
                        var p = svcPersonnel[i];
                        var row = new TableRow();
                        row.Cells.Add(new TableCell(new Paragraph(new Run((i + 1).ToString()))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.Rank?.ShortName ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.Person?.FullName ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.ActiveServiceType?.Name ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.ActiveServiceAssignment?.DutyLocation ?? "-"))));
                        string times = $"{p.ActiveServiceAssignment?.StartDateTime:HH:mm} - {p.ActiveServiceAssignment?.EndDateTime:HH:mm}";
                        row.Cells.Add(new TableCell(new Paragraph(new Run(times))));
                        rowGroup.Rows.Add(row);
                    }
                    break;

                case ReportType.AbsentPersonnel:
                    table.Columns.Add(new TableColumn { Width = new GridLength(40) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(100) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(180) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(140) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(100) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(120) });

                    var headerRowAbs = new TableRow();
                    headerRowAbs.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Α/Α")))));
                    headerRowAbs.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΒΑΘΜΟΣ")))));
                    headerRowAbs.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΟΝΟΜΑΤΕΠΩΝΥΜΟ")))));
                    headerRowAbs.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΑΙΤΙΟΛΟΓΙΑ")))));
                    headerRowAbs.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΕΠΙΣΤΡΟΦΗ")))));
                    headerRowAbs.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΕΓΓΡΑΦΟ")))));
                    rowGroup.Rows.Add(headerRowAbs);

                    var absents = snapshot.AbsentPersonnel.OrderBy(p => p.Rank?.SortOrder ?? 99).ToList();
                    for (int i = 0; i < absents.Count; i++)
                    {
                        var p = absents[i];
                        var row = new TableRow();
                        row.Cells.Add(new TableCell(new Paragraph(new Run((i + 1).ToString()))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.Rank?.ShortName ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.Person?.FullName ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.ActiveStatusType?.Name ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.ExpectedReturnDate?.ToString("dd/MM/yyyy") ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.ActiveStatusEvent?.ReferenceDocument ?? "-"))));
                        rowGroup.Rows.Add(row);
                    }
                    break;

                default: // DailyDynamologio / PresentPersonnel
                    table.Columns.Add(new TableColumn { Width = new GridLength(40) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(100) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(180) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(140) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(120) });

                    var headerRow = new TableRow();
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Α/Α")))));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΒΑΘΜΟΣ")))));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΟΝΟΜΑΤΕΠΩΝΥΜΟ")))));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΥΠΟΜΟΝΑΔΑ")))));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("ΚΑΤΑΣΤΑΣΗ")))));
                    rowGroup.Rows.Add(headerRow);

                    var allList = snapshot.PresentPersonnel.OrderBy(p => p.Rank?.SortOrder ?? 99).ToList();
                    for (int i = 0; i < allList.Count; i++)
                    {
                        var p = allList[i];
                        var row = new TableRow();
                        row.Cells.Add(new TableCell(new Paragraph(new Run((i + 1).ToString()))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.Rank?.ShortName ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.Person?.FullName ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.Unit?.Name ?? "-"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(p.StatusDisplayLabel ?? "-"))));
                        rowGroup.Rows.Add(row);
                    }
                    break;
            }

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
