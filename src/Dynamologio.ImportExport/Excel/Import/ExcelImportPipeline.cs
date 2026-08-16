using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    public class FieldDiff
    {
        public string FieldName { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;

        public string DisplayText => $"{FieldName}: {OldValue} ➔ {NewValue}";
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
        public List<string> ValidationMessages { get; set; } = new List<string>();
        public List<FieldDiff> FieldDiffs { get; set; } = new List<FieldDiff>();

        // Resolved entities
        public Personnel TargetPerson { get; set; }
        public Rank ResolvedRank { get; set; }
        public OrganisationUnit ResolvedUnit { get; set; }

        public string DisplayActionText
        {
            get
            {
                switch (Action)
                {
                    case ImportRowAction.InsertNew: return "Νέα Εγγραφή";
                    case ImportRowAction.UpdateExisting: return "Ενημέρωση";
                    case ImportRowAction.SkipUnchanged: return "Χωρίς Αλλαγή";
                    case ImportRowAction.ErrorInvalid: return "Σφάλμα / Μη Έγκυρο";
                    case ImportRowAction.DuplicateWarning: return "Πιθανή Διπλοεγγραφή";
                    default: return Action.ToString();
                }
            }
        }

        public string FullDiffSummary => FieldDiffs.Count > 0 ? string.Join(", ", FieldDiffs.Select(d => d.DisplayText)) : "-";
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

        public bool CanCommit => TotalRows > 0 && ErrorCount == 0;
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
                        }
                    }
                }

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
                        continue;
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

                    // Validate Rank (NO SILENT DEFAULT GUESSING)
                    var matchedRank = allRanks.FirstOrDefault(rk =>
                        string.Equals(rk.Name, rankStr, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(rk.ShortName, rankStr, StringComparison.OrdinalIgnoreCase));

                    if (matchedRank == null)
                    {
                        rowPreview.Action = ImportRowAction.ErrorInvalid;
                        rowPreview.ValidationMessages.Add($"Άγνωστος βαθμός '{rankStr}'. Απαιτείται αντιστοίχιση.");
                    }
                    else
                    {
                        rowPreview.ResolvedRank = matchedRank;
                    }

                    // Validate Unit (NO SILENT DEFAULT GUESSING)
                    var matchedUnit = allUnits.FirstOrDefault(u =>
                        string.Equals(u.Name, unitStr, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(u.Code, unitStr, StringComparison.OrdinalIgnoreCase));

                    if (matchedUnit == null && !string.IsNullOrWhiteSpace(unitStr))
                    {
                        rowPreview.Action = ImportRowAction.ErrorInvalid;
                        rowPreview.ValidationMessages.Add($"Άγνωστη μονάδα/λόχος '{unitStr}'.");
                    }
                    else
                    {
                        rowPreview.ResolvedUnit = matchedUnit ?? allUnits.FirstOrDefault();
                    }

                    // Find Existing Person by ASM or Full Name
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

                    if (rowPreview.Action != ImportRowAction.ErrorInvalid)
                    {
                        if (existingPerson != null)
                        {
                            rowPreview.TargetPerson = existingPerson;
                            rowPreview.Action = ImportRowAction.UpdateExisting;

                            // Calculate Field Diffs
                            var existingRank = allRanks.FirstOrDefault(r1 => r1.Id == existingPerson.RankId);
                            var existingUnit = allUnits.FirstOrDefault(u1 => u1.Id == existingPerson.OrganisationUnitId);

                            if (rowPreview.ResolvedRank != null && existingRank != null && existingRank.Id != rowPreview.ResolvedRank.Id)
                            {
                                rowPreview.FieldDiffs.Add(new FieldDiff { FieldName = "Βαθμός", OldValue = existingRank.ShortName, NewValue = rowPreview.ResolvedRank.ShortName });
                            }

                            if (rowPreview.ResolvedUnit != null && existingUnit != null && existingUnit.Id != rowPreview.ResolvedUnit.Id)
                            {
                                rowPreview.FieldDiffs.Add(new FieldDiff { FieldName = "Μονάδα", OldValue = existingUnit.Name, NewValue = rowPreview.ResolvedUnit.Name });
                            }

                            if (!string.IsNullOrWhiteSpace(specStr) && !string.Equals(existingPerson.Specialty, specStr, StringComparison.OrdinalIgnoreCase))
                            {
                                rowPreview.FieldDiffs.Add(new FieldDiff { FieldName = "Ειδικότητα", OldValue = existingPerson.Specialty ?? "-", NewValue = specStr });
                            }

                            if (rowPreview.FieldDiffs.Count == 0)
                            {
                                rowPreview.Action = ImportRowAction.SkipUnchanged;
                            }
                        }
                        else
                        {
                            rowPreview.Action = ImportRowAction.InsertNew;
                        }
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

            if (!previewReport.CanCommit)
            {
                throw new InvalidOperationException($"Αδυναμία εκτέλεσης εισαγωγής: Εντοπίστηκαν {previewReport.ErrorCount} σφάλματα επικύρωσης.");
            }

            uow.BeginTransaction();
            try
            {
                var batch = new ImportBatch
                {
                    FileName = previewReport.FileName,
                    Sha256Hash = previewReport.FileSha256,
                    TotalRecordsProcessed = previewReport.TotalRows,
                    InsertedCount = previewReport.NewCount,
                    UpdatedCount = previewReport.UpdateCount,
                    WarningCount = previewReport.WarningCount,
                    SummaryNotes = $"Εισαγωγή από αρχείο Excel '{previewReport.FileName}' ({previewReport.NewCount} νέες, {previewReport.UpdateCount} ενημερώσεις)"
                };
                uow.ImportBatches.Insert(batch);

                foreach (var row in previewReport.Rows)
                {
                    if (row.Action == ImportRowAction.InsertNew)
                    {
                        var newPerson = new Personnel
                        {
                            MilitaryServiceNumber = row.MilitaryServiceNumber,
                            LastName = row.LastName.Trim().ToUpperInvariant(),
                            FirstName = row.FirstName.Trim().ToUpperInvariant(),
                            FatherName = row.FatherName.Trim().ToUpperInvariant(),
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
                        if (!string.IsNullOrWhiteSpace(row.MilitaryServiceNumber))
                            row.TargetPerson.MilitaryServiceNumber = row.MilitaryServiceNumber;

                        if (row.ResolvedRank != null)
                        {
                            row.TargetPerson.RankId = row.ResolvedRank.Id;
                            row.TargetPerson.Category = row.ResolvedRank.Category;
                        }

                        if (row.ResolvedUnit != null)
                            row.TargetPerson.OrganisationUnitId = row.ResolvedUnit.Id;

                        if (!string.IsNullOrWhiteSpace(row.Specialty))
                            row.TargetPerson.Specialty = row.Specialty;

                        row.TargetPerson.ModifiedBy = operatorUsername;
                        row.TargetPerson.ModifiedAt = DateTime.Now;

                        uow.Personnel.Update(row.TargetPerson);
                    }
                }

                uow.Commit();
                return batch;
            }
            catch
            {
                uow.Rollback();
                throw;
            }
        }

        private static string GetCellString(IRow row, int cellIndex)
        {
            var cell = row.GetCell(cellIndex);
            if (cell == null) return string.Empty;
            return cell.ToString().Trim();
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
