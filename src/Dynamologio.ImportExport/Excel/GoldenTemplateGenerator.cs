using System;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Dynamologio.ImportExport.Excel
{
    public static class GoldenTemplateGenerator
    {
        public static string GenerateDefaultGoldenTemplate(string targetFilePath)
        {
            string dir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var wb = new XSSFWorkbook();

            // Font & Styles
            var fontTitle = wb.CreateFont();
            fontTitle.FontName = "Calibri";
            fontTitle.FontHeightInPoints = 14;
            fontTitle.IsBold = true;

            var fontHeader = wb.CreateFont();
            fontHeader.FontName = "Calibri";
            fontHeader.FontHeightInPoints = 11;
            fontHeader.IsBold = true;

            var styleTitle = wb.CreateCellStyle();
            styleTitle.SetFont(fontTitle);
            styleTitle.Alignment = HorizontalAlignment.Center;
            styleTitle.VerticalAlignment = VerticalAlignment.Center;

            var styleHeader = wb.CreateCellStyle();
            styleHeader.SetFont(fontHeader);
            styleHeader.Alignment = HorizontalAlignment.Center;
            styleHeader.VerticalAlignment = VerticalAlignment.Center;
            styleHeader.BorderTop = BorderStyle.Thin;
            styleHeader.BorderBottom = BorderStyle.Medium;
            styleHeader.BorderLeft = BorderStyle.Thin;
            styleHeader.BorderRight = BorderStyle.Thin;
            styleHeader.FillForegroundColor = IndexedColors.Grey25Percent.Index;
            styleHeader.FillPattern = FillPattern.SolidForeground;

            var styleData = wb.CreateCellStyle();
            styleData.Alignment = HorizontalAlignment.Center;
            styleData.VerticalAlignment = VerticalAlignment.Center;
            styleData.BorderTop = BorderStyle.Thin;
            styleData.BorderBottom = BorderStyle.Thin;
            styleData.BorderLeft = BorderStyle.Thin;
            styleData.BorderRight = BorderStyle.Thin;

            // ==========================================
            // SHEET 1: ΔΥΝΑΜΟΛΟΓΙΟ
            // ==========================================
            var s1 = wb.CreateSheet("ΔΥΝΑΜΟΛΟΓΙΟ");
            s1.IsPrintGridlines = true;
            s1.DisplayGridlines = true;

            // Row 0: Title
            var r0 = s1.CreateRow(0);
            r0.HeightInPoints = 25;
            var c0_1 = r0.CreateCell(1);
            c0_1.SetCellValue("123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ");
            c0_1.CellStyle = styleTitle;
            s1.AddMergedRegion(new CellRangeAddress(0, 0, 1, 6));

            // Row 1: Subtitle & Date
            var r1 = s1.CreateRow(1);
            r1.HeightInPoints = 20;
            var c1_0 = r1.CreateCell(0);
            c1_0.SetCellValue("ΗΜΕΡΗΣΙΟ ΔΥΝΑΜΟΛΟΓΙΟ");
            c1_0.CellStyle = styleHeader;

            var c1_1 = r1.CreateCell(1);
            c1_1.SetCellValue(DateTime.Today.ToString("dd/MM/yyyy"));
            c1_1.CellStyle = styleHeader;

            // Row 2: Table Header
            var r2 = s1.CreateRow(2);
            r2.HeightInPoints = 22;
            string[] headers1 = { "ΚΑΤΗΓΟΡΙΑ", "ΥΠΑΡΧΟΥΣΑ", "ΠΑΡΟΝΤΕΣ", "ΑΠΟΝΤΕΣ", "Κ.Α.", "Α.Α.", "Φ.Π." };
            for (int i = 0; i < headers1.Length; i++)
            {
                var c = r2.CreateCell(i);
                c.SetCellValue(headers1[i]);
                c.CellStyle = styleHeader;
            }

            // Row 3: Στελέχη
            var r3 = s1.CreateRow(3);
            r3.CreateCell(0).SetCellValue("ΣΤΕΛΕΧΗ");
            r3.GetCell(0).CellStyle = styleData;
            for (int i = 1; i <= 6; i++) { var c = r3.CreateCell(i); c.SetCellValue(0); c.CellStyle = styleData; }

            // Row 4: Οπλίτες
            var r4 = s1.CreateRow(4);
            r4.CreateCell(0).SetCellValue("ΟΠΛΙΤΕΣ");
            r4.GetCell(0).CellStyle = styleData;
            for (int i = 1; i <= 6; i++) { var c = r4.CreateCell(i); c.SetCellValue(0); c.CellStyle = styleData; }

            // Row 5: ΣΥΝΟΛΟ
            var r5 = s1.CreateRow(5);
            r5.CreateCell(0).SetCellValue("ΓΕΝΙΚΟ ΣΥΝΟΛΟ");
            r5.GetCell(0).CellStyle = styleHeader;
            for (int i = 1; i <= 6; i++)
            {
                var c = r5.CreateCell(i);
                c.CellFormula = $"SUM({(char)('A' + i)}4:{(char)('A' + i)}5)";
                c.CellStyle = styleHeader;
            }

            for (int i = 0; i <= 6; i++) s1.SetColumnWidth(i, 18 * 256);

            // ==========================================
            // SHEET 2: ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ
            // ==========================================
            var s2 = wb.CreateSheet("ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ");
            var r2_0 = s2.CreateRow(0);
            r2_0.CreateCell(0).SetCellValue("ΟΝΟΜΑΣΤΙΚΗ ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ");
            r2_0.GetCell(0).CellStyle = styleTitle;
            s2.AddMergedRegion(new CellRangeAddress(0, 0, 0, 8));

            var r2_1 = s2.CreateRow(1);
            r2_1.CreateCell(0).SetCellValue("Ημερομηνία:");
            r2_1.CreateCell(1).SetCellValue(DateTime.Today.ToString("dd/MM/yyyy"));

            var r2_2 = s2.CreateRow(2);
            string[] headers2 = { "Α/Α", "ΒΑΘΜΟΣ", "ΕΠΩΝΥΜΟ", "ΟΝΟΜΑ", "ΑΙΤΙΟΛΟΓΙΑ", "ΑΠΟ", "ΕΩΣ", "ΕΠΙΣΤΡΟΦΗ", "ΔΙΑΤΑΓΗ" };
            for (int i = 0; i < headers2.Length; i++)
            {
                var c = r2_2.CreateCell(i);
                c.SetCellValue(headers2[i]);
                c.CellStyle = styleHeader;
            }

            for (int i = 0; i < headers2.Length; i++) s2.SetColumnWidth(i, 16 * 256);

            using (var fs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write))
            {
                wb.Write(fs);
            }

            return targetFilePath;
        }
    }
}
