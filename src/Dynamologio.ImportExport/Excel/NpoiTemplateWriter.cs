using System;
using System.IO;
using System.Linq;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Newtonsoft.Json;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Dynamologio.ImportExport.Excel
{
    public class TemplateCellMapping
    {
        public string TitleCell { get; set; } = "B1";
        public string DateCell { get; set; } = "B2";
        public int SummaryStartRow { get; set; } = 3;
        public int AbsentStartRow { get; set; } = 3;
        public int PresentStartRow { get; set; } = 3;
        public int ServiceStartRow { get; set; } = 3;
    }

    public interface IExcelTemplateWriter
    {
        void GenerateDynamologioWorkbook(string templateFilePath, string destinationFilePath, UnitStrengthSnapshot snapshot, string unitTitle, TemplateCellMapping mapping = null);
        void GenerateAbsentWorkbook(string destinationFilePath, UnitStrengthSnapshot snapshot, string unitTitle);
        void GeneratePresentWorkbook(string destinationFilePath, UnitStrengthSnapshot snapshot, string unitTitle);
        void GenerateServiceWorkbook(string destinationFilePath, UnitStrengthSnapshot snapshot, string unitTitle);
    }

    public class NpoiTemplateWriter : IExcelTemplateWriter
    {
        public void GenerateDynamologioWorkbook(
            string templateFilePath,
            string destinationFilePath,
            UnitStrengthSnapshot strengthSnapshot,
            string unitTitle,
            TemplateCellMapping mapping = null)
        {
            if (strengthSnapshot == null) throw new ArgumentNullException(nameof(strengthSnapshot));
            mapping = mapping ?? new TemplateCellMapping();

            string destDir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            if (!string.IsNullOrEmpty(templateFilePath) && File.Exists(templateFilePath))
            {
                File.Copy(templateFilePath, destinationFilePath, true);
            }

            IWorkbook workbook = null;
            if (File.Exists(destinationFilePath))
            {
                using (var fs = new FileStream(destinationFilePath, FileMode.Open, FileAccess.Read))
                {
                    workbook = destinationFilePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ? (IWorkbook)new HSSFWorkbook(fs) : new XSSFWorkbook(fs);
                }
            }
            else
            {
                workbook = new XSSFWorkbook();
                workbook.CreateSheet("ΔΥΝΑΜΟΛΟΓΙΟ");
                workbook.CreateSheet("ΑΠΟΝΤΕΣ");
            }

            // Sheet 1: Dynamologio Summary
            var sheet1 = workbook.GetSheetAt(0);
            if (sheet1 != null)
            {
                SetCellValue(sheet1, 0, 1, string.IsNullOrWhiteSpace(unitTitle) ? "ΕΛΛΗΝΙΚΟΣ ΣΤΡΑΤΟΣ" : unitTitle);
                SetCellValue(sheet1, 1, 1, strengthSnapshot.AsOfTimestamp.ToString("dd/MM/yyyy"));

                int r = mapping.SummaryStartRow;
                SetCellValue(sheet1, r, 1, strengthSnapshot.OfficersAndNcosActive);
                SetCellValue(sheet1, r, 2, strengthSnapshot.OfficersAndNcosPresent);
                SetCellValue(sheet1, r, 3, strengthSnapshot.OfficersAndNcosAbsent);

                SetCellValue(sheet1, r + 1, 1, strengthSnapshot.ConscriptsActive);
                SetCellValue(sheet1, r + 1, 2, strengthSnapshot.ConscriptsPresent);
                SetCellValue(sheet1, r + 1, 3, strengthSnapshot.ConscriptsAbsent);

                SetCellValue(sheet1, r + 2, 1, strengthSnapshot.TotalActiveStrength);
                SetCellValue(sheet1, r + 2, 2, strengthSnapshot.TotalPresent);
                SetCellValue(sheet1, r + 2, 3, strengthSnapshot.TotalAbsent);
            }

            // Sheet 2: Absent List
            if (workbook.NumberOfSheets > 1)
            {
                var sheet2 = workbook.GetSheetAt(1);
                if (sheet2 != null)
                {
                    SetCellValue(sheet2, 1, 1, strengthSnapshot.AsOfTimestamp.ToString("dd/MM/yyyy"));
                    int startRow = mapping.AbsentStartRow;
                    var absents = strengthSnapshot.AbsentPersonnel.OrderBy(p => p.Rank?.SortOrder ?? 99).ToList();

                    for (int i = 0; i < absents.Count; i++)
                    {
                        var p = absents[i];
                        var row = sheet2.GetRow(startRow + i) ?? sheet2.CreateRow(startRow + i);
                        SetCellValue(row, 0, (i + 1).ToString());
                        SetCellValue(row, 1, p.Rank?.ShortName ?? "");
                        SetCellValue(row, 2, p.Person?.LastName ?? "");
                        SetCellValue(row, 3, p.Person?.FirstName ?? "");
                        SetCellValue(row, 4, p.ActiveStatusType?.Name ?? "Απουσία");
                        SetCellValue(row, 5, p.ActiveStatusEvent?.StartAt.ToString("dd/MM/yyyy") ?? "-");
                        SetCellValue(row, 6, p.ActiveStatusEvent?.EndAtExclusive.AddDays(-1).ToString("dd/MM/yyyy") ?? "-");
                        SetCellValue(row, 7, p.ExpectedReturnDate.HasValue ? p.ExpectedReturnDate.Value.ToString("dd/MM/yyyy") : "-");
                        SetCellValue(row, 8, p.ActiveStatusEvent?.ReferenceDocument ?? "");
                    }
                }
            }

            using (var outFs = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(outFs);
            }
        }

        public void GenerateAbsentWorkbook(string destinationFilePath, UnitStrengthSnapshot snapshot, string unitTitle)
        {
            var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ");

            SetCellValue(sheet, 0, 0, string.IsNullOrWhiteSpace(unitTitle) ? "ΕΛΛΗΝΙΚΟΣ ΣΤΡΑΤΟΣ" : unitTitle);
            SetCellValue(sheet, 1, 0, $"Ημερομηνία: {snapshot.AsOfTimestamp:dd/MM/yyyy}");
            SetCellValue(sheet, 2, 0, "Α/Α");
            SetCellValue(sheet, 2, 1, "ΒΑΘΜΟΣ");
            SetCellValue(sheet, 2, 2, "ΕΠΩΝΥΜΟ");
            SetCellValue(sheet, 2, 3, "ΟΝΟΜΑ");
            SetCellValue(sheet, 2, 4, "ΜΟΝΑΔΑ");
            SetCellValue(sheet, 2, 5, "ΕΙΔΟΣ ΑΠΟΥΣΙΑΣ");
            SetCellValue(sheet, 2, 6, "ΕΝΑΡΞΗ");
            SetCellValue(sheet, 2, 7, "ΕΠΙΣΤΡΟΦΗ");
            SetCellValue(sheet, 2, 8, "ΕΓΓΡΑΦΟ");

            var list = snapshot.AbsentPersonnel.OrderBy(p => p.Rank?.SortOrder ?? 99).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                var row = sheet.CreateRow(3 + i);
                SetCellValue(row, 0, (i + 1).ToString());
                SetCellValue(row, 1, p.Rank?.ShortName ?? "-");
                SetCellValue(row, 2, p.Person?.LastName ?? "-");
                SetCellValue(row, 3, p.Person?.FirstName ?? "-");
                SetCellValue(row, 4, p.Unit?.Name ?? "-");
                SetCellValue(row, 5, p.ActiveStatusType?.Name ?? "Απουσία");
                SetCellValue(row, 6, p.ActiveStatusEvent?.StartAt.ToString("dd/MM/yyyy") ?? "-");
                SetCellValue(row, 7, p.ExpectedReturnDate?.ToString("dd/MM/yyyy") ?? "-");
                SetCellValue(row, 8, p.ActiveStatusEvent?.ReferenceDocument ?? "-");
            }

            using (var fs = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write))
            {
                wb.Write(fs);
            }
        }

        public void GeneratePresentWorkbook(string destinationFilePath, UnitStrengthSnapshot snapshot, string unitTitle)
        {
            var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("ΚΑΤΑΣΤΑΣΗ ΠΑΡΟΝΤΩΝ");

            SetCellValue(sheet, 0, 0, string.IsNullOrWhiteSpace(unitTitle) ? "ΕΛΛΗΝΙΚΟΣ ΣΤΡΑΤΟΣ" : unitTitle);
            SetCellValue(sheet, 1, 0, $"Ημερομηνία: {snapshot.AsOfTimestamp:dd/MM/yyyy}");
            SetCellValue(sheet, 2, 0, "Α/Α");
            SetCellValue(sheet, 2, 1, "ΒΑΘΜΟΣ");
            SetCellValue(sheet, 2, 2, "ΕΠΩΝΥΜΟ");
            SetCellValue(sheet, 2, 3, "ΟΝΟΜΑ");
            SetCellValue(sheet, 2, 4, "ΥΠΟΜΟΝΑΔΑ");
            SetCellValue(sheet, 2, 5, "ΕΙΔΙΚΟΤΗΤΑ");
            SetCellValue(sheet, 2, 6, "ΑΣΜ");

            var list = snapshot.PresentPersonnel.OrderBy(p => p.Rank?.SortOrder ?? 99).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                var row = sheet.CreateRow(3 + i);
                SetCellValue(row, 0, (i + 1).ToString());
                SetCellValue(row, 1, p.Rank?.ShortName ?? "-");
                SetCellValue(row, 2, p.Person?.LastName ?? "-");
                SetCellValue(row, 3, p.Person?.FirstName ?? "-");
                SetCellValue(row, 4, p.Unit?.Name ?? "-");
                SetCellValue(row, 5, p.Person?.Specialty ?? "-");
                SetCellValue(row, 6, p.Person?.MilitaryServiceNumber ?? "-");
            }

            using (var fs = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write))
            {
                wb.Write(fs);
            }
        }

        public void GenerateServiceWorkbook(string destinationFilePath, UnitStrengthSnapshot snapshot, string unitTitle)
        {
            var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("ΠΙΝΑΚΑΣ ΥΠΗΡΕΣΙΩΝ");

            SetCellValue(sheet, 0, 0, string.IsNullOrWhiteSpace(unitTitle) ? "ΕΛΛΗΝΙΚΟΣ ΣΤΡΑΤΟΣ" : unitTitle);
            SetCellValue(sheet, 1, 0, $"Ημερομηνία Υπηρεσιών: {snapshot.AsOfTimestamp:dd/MM/yyyy}");
            SetCellValue(sheet, 2, 0, "Α/Α");
            SetCellValue(sheet, 2, 1, "ΒΑΘΜΟΣ");
            SetCellValue(sheet, 2, 2, "ΕΠΩΝΥΜΟ");
            SetCellValue(sheet, 2, 3, "ΟΝΟΜΑ");
            SetCellValue(sheet, 2, 4, "ΥΠΗΡΕΣΙΑ / ΚΑΘΗΚΟΝ");
            SetCellValue(sheet, 2, 5, "ΤΟΠΟΘΕΣΙΑ");
            SetCellValue(sheet, 2, 6, "ΩΡΑΡΙΟ");
            SetCellValue(sheet, 2, 7, "ΠΑΡΑΤΗΡΗΣΕΙΣ");

            var list = snapshot.PresentPersonnel.Where(p => p.ActiveServiceAssignment != null).OrderBy(p => p.Rank?.SortOrder ?? 99).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                var row = sheet.CreateRow(3 + i);
                SetCellValue(row, 0, (i + 1).ToString());
                SetCellValue(row, 1, p.Rank?.ShortName ?? "-");
                SetCellValue(row, 2, p.Person?.LastName ?? "-");
                SetCellValue(row, 3, p.Person?.FirstName ?? "-");
                SetCellValue(row, 4, p.ActiveServiceType?.Name ?? "-");
                SetCellValue(row, 5, p.ActiveServiceAssignment?.DutyLocation ?? "-");
                string times = $"{p.ActiveServiceAssignment?.StartDateTime:HH:mm} - {p.ActiveServiceAssignment?.EndDateTime:HH:mm}";
                SetCellValue(row, 6, times);
                SetCellValue(row, 7, p.ActiveServiceAssignment?.Notes ?? "-");
            }

            using (var fs = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write))
            {
                wb.Write(fs);
            }
        }

        private static void SetCellValue(ISheet sheet, int rowIndex, int colIndex, string value)
        {
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            var cell = row.GetCell(colIndex) ?? row.CreateCell(colIndex);
            cell.SetCellValue(value);
        }

        private static void SetCellValue(ISheet sheet, int rowIndex, int colIndex, int value)
        {
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            var cell = row.GetCell(colIndex) ?? row.CreateCell(colIndex);
            cell.SetCellValue(value);
        }

        private static void SetCellValue(IRow row, int colIndex, string value)
        {
            var cell = row.GetCell(colIndex) ?? row.CreateCell(colIndex);
            cell.SetCellValue(value);
        }
    }
}
