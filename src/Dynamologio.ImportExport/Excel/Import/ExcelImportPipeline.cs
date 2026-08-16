using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Dynamologio.ImportExport.Excel.Import
{
    public enum ImportRowAction
    {
        InsertNew = 1,
        UpdateExisting = 2,
        SkipUnchanged = 3,
        ErrorInvalid = 4,
        DuplicateWarning = 5
    }

    public class ImportRowPreview
    {
        public int RowIndex { get; set; }
        public ImportRowAction Action { get; set; }
        public string MilitaryServiceNumber { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public string RankName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string StatusStartDate { get; set; } = string.Empty;
        public string StatusReturnDate { get; set; } = string.Empty;
        public List<string> ValidationMessages { get; set; } = new List<string>();

        // Resolved entities
        public Personnel TargetPerson { get; set; }
        public Rank ResolvedRank { get; set; }
        public OrganisationUnit ResolvedUnit { get; set; }
    }

    public class ImportPreviewReport
    {
        public string FileName { get; set; } = string.Empty;
        public string FileSha256 { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int NewCount => Rows.Count(r => r.Action == ImportRowAction.InsertNew);
        public int UpdateCount => Rows.Count(r => r.Action == ImportRowAction.UpdateExisting);
        public int SkipCount => Rows.Count(r => r.Action == ImportRowAction.SkipUnchanged);
        public int ErrorCount => Rows.Count(r => r.Action == ImportRowAction.ErrorInvalid);
        public int WarningCount => Rows.Count(r => r.Action == ImportRowAction.DuplicateWarning);
        public List<ImportRowPreview> Rows { get; set; } = new List<ImportRowPreview>();
    }

    public interface IExcelImportService
    {
        ImportPreviewReport AnalyzeAndPreviewImport(string filePath, IUnitOfWork uow);
        ImportBatch CommitImport(ImportPreviewReport previewReport, IUnitOfWork uow, string operatorUsername = "OPERATOR");
    }

    public class ExcelImportService : IExcelImportService
    {
        public ImportPreviewReport AnalyzeAndPreviewImport(string filePath, IUnitOfWork uow)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Το αρχείο εισαγωγής δεν βρέθηκε.", filePath);
            }

            var report = new ImportPreviewReport
            {
                FileName = Path.GetFileName(filePath),
                FileSha256 = ComputeSha256(filePath)
            };

            var allPersonnel = uow.Personnel.GetAll().ToList();
            var allRanks = uow.Ranks.GetAll().ToList();
            var allUnits = uow.OrganisationUnits.GetAll().ToList();
            var allStatusTypes = uow.StatusTypes.GetAll().ToList();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                IWorkbook workbook = filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)
                    ? (IWorkbook)new HSSFWorkbook(fs)
                    : new XSSFWorkbook(fs);

                var sheet = workbook.GetSheetAt(0);
                if (sheet == null || sheet.LastRowNum < sheet.FirstRowNum)
                {
                    return report;
                }

                // Detect headers from row 0 or 1
                int headerRowIndex = sheet.FirstRowNum;
                var headerRow = sheet.GetRow(headerRowIndex);
                var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                if (headerRow != null)
                {
                    for (int c = headerRow.FirstCellNum; c < headerRow.LastCellNum; c++)
                    {
                        string headerText = headerRow.GetCell(c)?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(headerText))
                        {
                            if (headerText.IndexOf("ΑΣΜ", StringComparison.OrdinalIgnoreCase) >= 0 || headerText.IndexOf("ΣΠΑ", StringComparison.OrdinalIgnoreCase) >= 0 || headerText.IndexOf("ΜΗΤΡΩ", StringComparison.OrdinalIgnoreCase) >= 0)
                                colMap["ASM"] = c;
                            else if (headerText.IndexOf("ΕΠΩΝΥΜ", StringComparison.OrdinalIgnoreCase) >= 0)
                                colMap["LASTNAME"] = c;
                            else if (headerText.IndexOf("ΟΝΟΜ", StringComparison.OrdinalIgnoreCase) >= 0 && headerText.IndexOf("ΕΠΩΝΥΜ", StringComparison.OrdinalIgnoreCase) < 0)
                                colMap["FIRSTNAME"] = c;
                            else if (headerText.IndexOf("ΠΑΤΡ", StringComparison.OrdinalIgnoreCase) >= 0)
                                colMap["FATHER"] = c;
                            else if (headerText.IndexOf("ΒΑΘΜ", StringComparison.OrdinalIgnoreCase) >= 0)
                                colMap["RANK"] = c;
                            else if (headerText.IndexOf("ΛΟΧ", StringComparison.OrdinalIgnoreCase) >= 0 || headerText.IndexOf("ΤΜΗΜ", StringComparison.OrdinalIgnoreCase) >= 0 || headerText.IndexOf("ΜΟΝΑΔ", StringComparison.OrdinalIgnoreCase) >= 0)
                                colMap["UNIT"] = c;
                            else if (headerText.IndexOf("ΕΙΔΙΚ", StringComparison.OrdinalIgnoreCase) >= 0)
                                colMap["SPECIALTY"] = c;
                            else if (headerText.IndexOf("ΚΑΤΑΣΤΑΣ", StringComparison.OrdinalIgnoreCase) >= 0 || headerText.IndexOf("ΑΔΕΙΑ", StringComparison.OrdinalIgnoreCase) >= 0)
                                colMap["STATUS"] = c;
                        }
                    }
                }

                // Fallback default column indexes if header row detection was partial
                if (!colMap.ContainsKey("LASTNAME")) colMap["LASTNAME"] = 2;
                if (!colMap.ContainsKey("FIRSTNAME")) colMap["FIRSTNAME"] = 3;
                if (!colMap.ContainsKey("RANK")) colMap["RANK"] = 1;

                int startRow = headerRowIndex + 1;
                for (int r = startRow; r <= sheet.LastRowNum; r++)
                {
                    var row = sheet.GetRow(r);
                    if (row == null) continue;

                    string lastName = GetCellString(row, colMap.TryGetValue("LASTNAME", out var cLn) ? cLn : 2);
                    string firstName = GetCellString(row, colMap.TryGetValue("FIRSTNAME", out var cFn) ? cFn : 3);
                    string rankStr = GetCellString(row, colMap.TryGetValue("RANK", out var cRk) ? cRk : 1);
                    string asm = GetCellString(row, colMap.TryGetValue("ASM", out var cAsm) ? cAsm : 0);
                    string unitStr = GetCellString(row, colMap.TryGetValue("UNIT", out var cUn) ? cUn : 4);
                    string specStr = GetCellString(row, colMap.TryGetValue("SPECIALTY", out var cSp) ? cSp : 5);

                    if (string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(firstName))
                    {
                        continue; // Skip blank line
                    }

                    var rowPreview = new ImportRowPreview
                    {
                        RowIndex = r + 1,
                        MilitaryServiceNumber = asm,
                        LastName = lastName,
                        FirstName = firstName,
                        RankName = rankStr,
                        UnitName = unitStr,
                        Specialty = specStr
                    };

                    // Match Rank
                    var matchedRank = allRanks.FirstOrDefault(rk =>
                        string.Equals(rk.Name, rankStr, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(rk.ShortName, rankStr, StringComparison.OrdinalIgnoreCase));

                    if (matchedRank == null)
                    {
                        matchedRank = allRanks.FirstOrDefault(rk => rk.ShortName.StartsWith("Στρ", StringComparison.OrdinalIgnoreCase)) ?? allRanks.FirstOrDefault();
                        rowPreview.ValidationMessages.Add($"Άγνωστος βαθμός '{rankStr}'. Αντιστοιχίστηκε προεπιλεγμένα.");
                    }
                    rowPreview.ResolvedRank = matchedRank;

                    // Match Unit
                    var matchedUnit = allUnits.FirstOrDefault(u =>
                        string.Equals(u.Name, unitStr, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(u.Code, unitStr, StringComparison.OrdinalIgnoreCase)) ?? allUnits.FirstOrDefault();
                    rowPreview.ResolvedUnit = matchedUnit;

                    // Check Existing Person by ASM or Full Name + Rank
                    Personnel existingPerson = null;
                    if (!string.IsNullOrWhiteSpace(asm))
                    {
                        existingPerson = allPersonnel.FirstOrDefault(p => string.Equals(p.MilitaryServiceNumber?.Trim(), asm.Trim(), StringComparison.OrdinalIgnoreCase));
                    }

                    if (existingPerson == null && !string.IsNullOrWhiteSpace(lastName))
                    {
                        existingPerson = allPersonnel.FirstOrDefault(p =>
                            string.Equals(p.LastName?.Trim(), lastName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(p.FirstName?.Trim(), firstName.Trim(), StringComparison.OrdinalIgnoreCase));
                    }

                    if (existingPerson != null)
                    {
                        rowPreview.TargetPerson = existingPerson;
                        rowPreview.Action = ImportRowAction.UpdateExisting;
                        rowPreview.ValidationMessages.Add($"Ενημέρωση υπάρχοντος: {existingPerson.FullName} (ΑΣΜ: {existingPerson.MilitaryServiceNumber})");
                    }
                    else
                    {
                        rowPreview.Action = ImportRowAction.InsertNew;
                    }

                    report.Rows.Add(rowPreview);
                }
            }

            report.TotalRows = report.Rows.Count;
            return report;
        }

        public ImportBatch CommitImport(ImportPreviewReport previewReport, IUnitOfWork uow, string operatorUsername = "OPERATOR")
        {
            if (previewReport == null || previewReport.Rows.Count == 0)
            {
                throw new InvalidOperationException("Δεν υπάρχουν εγγραφές προς εισαγωγή.");
            }

            var batch = new ImportBatch
            {
                FileName = previewReport.FileName,
                Sha256Hash = previewReport.FileSha256,
                TotalRecordsProcessed = previewReport.TotalRows,
                InsertedCount = previewReport.NewCount,
                UpdatedCount = previewReport.UpdateCount,
                WarningCount = previewReport.WarningCount,
                SummaryNotes = $"Εισαγωγή από αρχείο Excel '{previewReport.FileName}' στις {DateTime.Now:dd/MM/yyyy HH:mm}"
            };
            uow.ImportBatches.Insert(batch);

            foreach (var row in previewReport.Rows)
            {
                if (row.Action == ImportRowAction.InsertNew)
                {
                    var newPerson = new Personnel
                    {
                        MilitaryServiceNumber = row.MilitaryServiceNumber,
                        LastName = row.LastName.ToUpperInvariant(),
                        FirstName = row.FirstName.ToUpperInvariant(),
                        FatherName = row.FatherName.ToUpperInvariant(),
                        RankId = row.ResolvedRank?.Id ?? Guid.Empty,
                        Category = row.ResolvedRank?.Category ?? PersonnelCategory.Conscript,
                        OrganisationUnitId = row.ResolvedUnit?.Id ?? Guid.Empty,
                        Specialty = row.Specialty,
                        StrengthStartDate = DateTime.Today,
                        CreatedBy = operatorUsername,
                        ModifiedBy = operatorUsername
                    };
                    uow.Personnel.Insert(newPerson);
                }
                else if (row.Action == ImportRowAction.UpdateExisting && row.TargetPerson != null)
                {
                    row.TargetPerson.MilitaryServiceNumber = !string.IsNullOrWhiteSpace(row.MilitaryServiceNumber) ? row.MilitaryServiceNumber : row.TargetPerson.MilitaryServiceNumber;
                    row.TargetPerson.LastName = !string.IsNullOrWhiteSpace(row.LastName) ? row.LastName.ToUpperInvariant() : row.TargetPerson.LastName;
                    row.TargetPerson.FirstName = !string.IsNullOrWhiteSpace(row.FirstName) ? row.FirstName.ToUpperInvariant() : row.TargetPerson.FirstName;
                    if (row.ResolvedRank != null) row.TargetPerson.RankId = row.ResolvedRank.Id;
                    if (row.ResolvedUnit != null) row.TargetPerson.OrganisationUnitId = row.ResolvedUnit.Id;
                    if (!string.IsNullOrWhiteSpace(row.Specialty)) row.TargetPerson.Specialty = row.Specialty;
                    row.TargetPerson.ModifiedBy = operatorUsername;
                    row.TargetPerson.ModifiedAt = DateTime.Now;

                    uow.Personnel.Update(row.TargetPerson);
                }
            }

            uow.Commit();
            return batch;
        }

        private static string GetCellString(IRow row, int cellIndex)
        {
            var cell = row.GetCell(cellIndex);
            if (cell == null) return string.Empty;
            return cell.ToString().Trim();
        }

        private static string ComputeSha256(string filePath)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new System.Text.StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
