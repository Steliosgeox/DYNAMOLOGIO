using System;
using System.IO;
using System.Linq;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Dynamologio.ImportExport.Excel
{
    public interface IExcelTemplateWriter
    {
        void GenerateDynamologioWorkbook(
            string templateFilePath,
            string destinationFilePath,
            UnitStrengthSnapshot strengthSnapshot,
            string unitTitle = "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ");
    }

    public class NpoiTemplateWriter : IExcelTemplateWriter
    {
        public void GenerateDynamologioWorkbook(
            string templateFilePath,
            string destinationFilePath,
            UnitStrengthSnapshot strengthSnapshot,
            string unitTitle = "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ")
        {
            if (strengthSnapshot == null) throw new ArgumentNullException(nameof(strengthSnapshot));
            if (!File.Exists(templateFilePath))
            {
                throw new FileNotFoundException("Το πρότυπο αρχείο Excel δεν βρέθηκε.", templateFilePath);
            }

            // 1. Copy template to destination
            string destDir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(templateFilePath, destinationFilePath, true);

            // 2. Open destination and populate
            using (var fs = new FileStream(destinationFilePath, FileMode.Open, FileAccess.ReadWrite))
            {
                IWorkbook workbook = null;
                if (destinationFilePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook(fs);
                }
                else
                {
                    workbook = new XSSFWorkbook(fs);
                }

                // Populate Sheet 1: ΔΥΝΑΜΟΛΟΓΙΟ
                var sheet1 = workbook.GetSheetAt(0);
                if (sheet1 != null)
                {
                    SetCellValue(sheet1, 0, 1, unitTitle); // Row 1, Col B
                    SetCellValue(sheet1, 1, 1, strengthSnapshot.AsOfTimestamp.ToString("dd/MM/yyyy")); // Row 2, Col B

                    // Summary Rows (Στελέχη: Row 4, Οπλίτες: Row 5, Σύνολο: Row 6)
                    // Col B: Υπάρχουσα, Col C: Παρόντες, Col D: Απόντες
                    SetCellValue(sheet1, 3, 1, strengthSnapshot.OfficersAndNcosActive);
                    SetCellValue(sheet1, 3, 2, strengthSnapshot.OfficersAndNcosPresent);
                    SetCellValue(sheet1, 3, 3, strengthSnapshot.OfficersAndNcosAbsent);

                    SetCellValue(sheet1, 4, 1, strengthSnapshot.ConscriptsActive);
                    SetCellValue(sheet1, 4, 2, strengthSnapshot.ConscriptsPresent);
                    SetCellValue(sheet1, 4, 3, strengthSnapshot.ConscriptsAbsent);

                    SetCellValue(sheet1, 5, 1, strengthSnapshot.TotalActiveStrength);
                    SetCellValue(sheet1, 5, 2, strengthSnapshot.TotalPresent);
                    SetCellValue(sheet1, 5, 3, strengthSnapshot.TotalAbsent);
                }

                // Populate Sheet 2: ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ
                if (workbook.NumberOfSheets > 1)
                {
                    var sheet2 = workbook.GetSheetAt(1);
                    if (sheet2 != null)
                    {
                        SetCellValue(sheet2, 1, 1, strengthSnapshot.AsOfTimestamp.ToString("dd/MM/yyyy"));

                        int startRow = 3;
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

                // Re-evaluate formulas if possible
                try
                {
                    if (workbook is HSSFWorkbook hssf) HSSFFormulaEvaluator.EvaluateAllFormulaCells(hssf);
                    else if (workbook is XSSFWorkbook xssf) XSSFFormulaEvaluator.EvaluateAllFormulaCells(xssf);
                }
                catch
                {
                    // Ignore formula evaluation errors on templates with complex user defined functions
                }

                using (var outFs = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write))
                {
                    workbook.Write(outFs);
                }
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
