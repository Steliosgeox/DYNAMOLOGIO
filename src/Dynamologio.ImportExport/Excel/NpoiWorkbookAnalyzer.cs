using System;
using System.Collections.Generic;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Dynamologio.ImportExport.Excel
{
    public class SheetAnalysisResult
    {
        public string SheetName { get; set; } = string.Empty;
        public int FirstRowNum { get; set; }
        public int LastRowNum { get; set; }
        public int TotalRows => LastRowNum >= FirstRowNum ? (LastRowNum - FirstRowNum + 1) : 0;
        public int MergedRegionsCount { get; set; }
        public List<string> HeaderCandidateColumns { get; set; } = new List<string>();
        public List<string> SampleRowPreviews { get; set; } = new List<string>();
    }

    public class WorkbookAnalysisReport
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty; // XLS or XLSX
        public int TotalSheets { get; set; }
        public List<SheetAnalysisResult> Sheets { get; set; } = new List<SheetAnalysisResult>();
    }

    public interface IWorkbookAnalyzer
    {
        WorkbookAnalysisReport AnalyzeWorkbook(string filePath);
    }

    public class NpoiWorkbookAnalyzer : IWorkbookAnalyzer
    {
        public WorkbookAnalysisReport AnalyzeWorkbook(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Το αρχείο Excel δεν βρέθηκε.", filePath);
            }

            var report = new WorkbookAnalysisReport
            {
                FilePath = filePath,
                FileType = Path.GetExtension(filePath).ToUpperInvariant()
            };

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                IWorkbook workbook = null;
                if (filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook(fs);
                }
                else
                {
                    workbook = new XSSFWorkbook(fs);
                }

                report.TotalSheets = workbook.NumberOfSheets;

                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    var sheet = workbook.GetSheetAt(i);
                    var sReport = new SheetAnalysisResult
                    {
                        SheetName = sheet.SheetName,
                        FirstRowNum = sheet.FirstRowNum,
                        LastRowNum = sheet.LastRowNum,
                        MergedRegionsCount = sheet.NumMergedRegions
                    };

                    if (sheet.LastRowNum >= sheet.FirstRowNum)
                    {
                        var firstRow = sheet.GetRow(sheet.FirstRowNum);
                        if (firstRow != null)
                        {
                            for (int c = firstRow.FirstCellNum; c < firstRow.LastCellNum; c++)
                            {
                                var cell = firstRow.GetCell(c);
                                sReport.HeaderCandidateColumns.Add(cell?.ToString()?.Trim() ?? string.Empty);
                            }
                        }
                    }

                    report.Sheets.Add(sReport);
                }
            }

            return report;
        }
    }
}
