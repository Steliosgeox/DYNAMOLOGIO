using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Newtonsoft.Json;
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

        public string DisplayText => $"{FieldName}: {OldValue} -> {NewValue}";
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
        public DateTime? EffectiveStartDate { get; set; }
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
        ImportPreviewReport AnalyzeAndPreviewImport(string filePath, IUnitOfWork uow, Dictionary<string, int> customColumnMapping = null, DateTime? defaultEffectiveDate = null);
        ImportBatch CommitImport(ImportPreviewReport previewReport, IUnitOfWork uow, string operatorUsername = null, DateTime? explicitEffectiveDate = null);
    }

    public class ExcelImportService : IExcelImportService
    {
        public ImportPreviewReport AnalyzeAndPreviewImport(string filePath, IUnitOfWork uow, Dictionary<string, int> customColumnMapping = null, DateTime? defaultEffectiveDate = null)
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

                if (customColumnMapping != null && customColumnMapping.Count > 0)
                {
                    foreach (var kvp in customColumnMapping) colMap[kvp.Key] = kvp.Value;
                }
                else if (headerRow != null)
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
                            else if (headerText.IndexOf("ΕΝΑΡΞ", StringComparison.OrdinalIgnoreCase) >= 0 || headerText.IndexOf("ΤΟΠΟΘΕΤ", StringComparison.OrdinalIgnoreCase) >= 0 || headerText.IndexOf("ΗΜΕΡΟΜ", StringComparison.OrdinalIgnoreCase) >= 0)
                                colMap["STARTDATE"] = c;
                        }
                    }
                }

                // Verify mandatory column detection
                bool hasMandatoryColumns = colMap.ContainsKey("LASTNAME") && colMap.ContainsKey("RANK");
                if (!hasMandatoryColumns)
                {
                    var errorRow = new ImportRowPreview
                    {
                        RowIndex = 1,
                        Action = ImportRowAction.ErrorInvalid,
                        ValidationMessages = new List<string> { "Δεν εντοπίστηκαν οι υποχρεωτικές στήλες 'ΕΠΩΝΥΜΟ' και 'ΒΑΘΜΟΣ' στις κεφαλίδες του Excel. Απαιτείται ρητή αντιστοίχιση στηλών." }
                    };
                    report.Rows.Add(errorRow);
                    report.TotalRows = 1;
                    return report;
                }

                int startRow = headerRowIndex + 1;
                for (int r = startRow; r <= sheet.LastRowNum; r++)
                {
                    var row = sheet.GetRow(r);
                    if (row == null) continue;

                    string lastName = colMap.TryGetValue("LASTNAME", out var cLn) ? GetCellString(row, cLn) : "";
                    string firstName = colMap.TryGetValue("FIRSTNAME", out var cFn) ? GetCellString(row, cFn) : "";
                    string rankStr = colMap.TryGetValue("RANK", out var cRk) ? GetCellString(row, cRk) : "";
                    string asm = colMap.TryGetValue("ASM", out var cAsm) ? GetCellString(row, cAsm) : "";
                    string unitStr = colMap.TryGetValue("UNIT", out var cUn) ? GetCellString(row, cUn) : "";
                    string specStr = colMap.TryGetValue("SPECIALTY", out var cSp) ? GetCellString(row, cSp) : "";
                    string dateStr = colMap.TryGetValue("STARTDATE", out var cDt) ? GetCellString(row, cDt) : "";

                    if (string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(firstName))
                    {
                        continue;
                    }

                    DateTime? parsedStartDate = null;
                    if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParse(dateStr, out var d))
                    {
                        parsedStartDate = d;
                    }

                    var rowPreview = new ImportRowPreview
                    {
                        RowIndex = r + 1,
                        MilitaryServiceNumber = asm,
                        LastName = lastName,
                        FirstName = firstName,
                        RankName = rankStr,
                        UnitName = unitStr,
                        Specialty = specStr,
                        EffectiveStartDate = parsedStartDate ?? defaultEffectiveDate
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
                    if (string.IsNullOrWhiteSpace(unitStr))
                    {
                        rowPreview.Action = ImportRowAction.ErrorInvalid;
                        rowPreview.ValidationMessages.Add("Κενή μονάδα/υπομονάδα. Απαιτείται ρητός ορισμός.");
                    }
                    else
                    {
                        var matchedUnit = allUnits.FirstOrDefault(u =>
                            string.Equals(u.Name, unitStr, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(u.Code, unitStr, StringComparison.OrdinalIgnoreCase));

                        if (matchedUnit == null)
                        {
                            rowPreview.Action = ImportRowAction.ErrorInvalid;
                            rowPreview.ValidationMessages.Add($"Άγνωστη μονάδα/λόχος '{unitStr}'.");
                        }
                        else
                        {
                            rowPreview.ResolvedUnit = matchedUnit;
                        }
                    }

                    // Find Existing Person by ASM or Full Name (with duplicate name ambiguity protection)
                    Personnel existingPerson = null;
                    if (!string.IsNullOrWhiteSpace(asm))
                    {
                        existingPerson = allPersonnel.FirstOrDefault(p => string.Equals(p.MilitaryServiceNumber?.Trim(), asm.Trim(), StringComparison.OrdinalIgnoreCase));
                    }

                    if (existingPerson == null && !string.IsNullOrWhiteSpace(lastName))
                    {
                        var matchingByName = allPersonnel.Where(p =>
                            string.Equals(p.LastName?.Trim(), lastName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(p.FirstName?.Trim(), firstName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

                        if (matchingByName.Count > 1)
                        {
                            // SEC-IMP-004: Flag duplicate name ambiguity
                            rowPreview.Action = ImportRowAction.ErrorInvalid;
                            rowPreview.ValidationMessages.Add($"Εντοπίστηκαν πολλαπλά πρόσωπα ({matchingByName.Count}) με το ονοματεπώνυμο '{lastName} {firstName}' χωρίς ΑΣΜ. Απαιτείται ΑΣΜ για ασφαλή ταυτοποίηση.");
                        }
                        else if (matchingByName.Count == 1)
                        {
                            existingPerson = matchingByName[0];
                        }
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

                            if (!string.IsNullOrWhiteSpace(rowPreview.Specialty) && !string.Equals(rowPreview.Specialty, existingPerson.Specialty, StringComparison.OrdinalIgnoreCase))
                            {
                                rowPreview.FieldDiffs.Add(new FieldDiff { FieldName = "Ειδικότητα", OldValue = existingPerson.Specialty ?? "-", NewValue = rowPreview.Specialty });
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

                report.TotalRows = report.Rows.Count;
            }

            return report;
        }

        public ImportBatch CommitImport(ImportPreviewReport previewReport, IUnitOfWork uow, string operatorUsername = null, DateTime? explicitEffectiveDate = null)
        {
            if (previewReport == null || !previewReport.CanCommit)
            {
                throw new InvalidOperationException("Η παρτίδα δεν είναι έτοιμη για εισαγωγή (περιέχει σφάλματα ή είναι κενή).");
            }

            string actor = !string.IsNullOrWhiteSpace(operatorUsername) ? operatorUsername : $"{Environment.UserDomainName}\\{Environment.UserName}";
            if (string.IsNullOrWhiteSpace(actor) || actor == "\\") actor = Environment.UserName;

            var batch = new ImportBatch
            {
                FileName = previewReport.FileName,
                Sha256Hash = previewReport.FileSha256,
                TotalRecordsProcessed = previewReport.TotalRows,
                InsertedCount = previewReport.NewCount,
                UpdatedCount = previewReport.UpdateCount,
                WarningCount = previewReport.WarningCount,
                SummaryNotes = $"Εισαγωγή από {actor}: {previewReport.NewCount} νέοι, {previewReport.UpdateCount} ενημερωμένοι."
            };

            DateTime effectiveStart = explicitEffectiveDate ?? DateTime.Today;

            uow.BeginTransaction();
            try
            {
                uow.ImportBatches.Insert(batch);

                foreach (var r in previewReport.Rows)
                {
                    if (r.Action == ImportRowAction.InsertNew)
                    {
                        var newPerson = new Personnel
                        {
                            MilitaryServiceNumber = r.MilitaryServiceNumber ?? string.Empty,
                            LastName = r.LastName ?? string.Empty,
                            FirstName = r.FirstName ?? string.Empty,
                            FatherName = r.FatherName ?? string.Empty,
                            RankId = r.ResolvedRank?.Id ?? Guid.Empty,
                            OrganisationUnitId = r.ResolvedUnit?.Id ?? Guid.Empty,
                            Specialty = r.Specialty ?? string.Empty,
                            StrengthStartDate = r.EffectiveStartDate ?? effectiveStart,
                            IsArchived = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        uow.Personnel.Insert(newPerson);

                        // Audit logging inside same transaction
                        uow.AuditEvents.Insert(new AuditEvent
                        {
                            Username = actor,
                            Action = AuditAction.Create,
                            EntityType = nameof(Personnel),
                            EntityId = newPerson.Id.ToString(),
                            Summary = $"Εισαγωγή νέου στελέχους: {newPerson.FullName} ({r.ResolvedRank?.ShortName})",
                            NewValueJson = JsonConvert.SerializeObject(newPerson),
                            ImportBatchId = batch.Id,
                            AppVersion = "1.0.0.0"
                        });
                    }
                    else if (r.Action == ImportRowAction.UpdateExisting && r.TargetPerson != null)
                    {
                        var person = r.TargetPerson;
                        var oldJson = JsonConvert.SerializeObject(person);

                        if (r.ResolvedRank != null) person.RankId = r.ResolvedRank.Id;
                        if (r.ResolvedUnit != null) person.OrganisationUnitId = r.ResolvedUnit.Id;
                        if (!string.IsNullOrWhiteSpace(r.Specialty)) person.Specialty = r.Specialty;
                        if (!string.IsNullOrWhiteSpace(r.MilitaryServiceNumber)) person.MilitaryServiceNumber = r.MilitaryServiceNumber;

                        person.ModifiedAt = DateTime.UtcNow;
                        person.ModifiedBy = actor;
                        uow.Personnel.Update(person);

                        // Audit logging inside same transaction
                        uow.AuditEvents.Insert(new AuditEvent
                        {
                            Username = actor,
                            Action = AuditAction.Update,
                            EntityType = nameof(Personnel),
                            EntityId = person.Id.ToString(),
                            Summary = $"Ενημέρωση στοιχείων από εισαγωγή: {person.FullName} [{r.FullDiffSummary}]",
                            OldValueJson = oldJson,
                            NewValueJson = JsonConvert.SerializeObject(person),
                            ImportBatchId = batch.Id,
                            AppVersion = "1.0.0.0"
                        });
                    }
                }

                // Batch Audit Event
                uow.AuditEvents.Insert(new AuditEvent
                {
                    Username = actor,
                    Action = AuditAction.Import,
                    EntityType = nameof(ImportBatch),
                    EntityId = batch.Id.ToString(),
                    Summary = $"Ολοκλήρωση παρτίδας εισαγωγής '{batch.FileName}' ({batch.InsertedCount} νέοι, {batch.UpdatedCount} ενημερωμένοι)",
                    NewValueJson = JsonConvert.SerializeObject(batch),
                    ImportBatchId = batch.Id,
                    AppVersion = "1.0.0.0"
                });

                uow.Commit();
                return batch;
            }
            catch
            {
                uow.Rollback();
                throw;
            }
        }

        private static string GetCellString(IRow row, int colIndex)
        {
            if (row == null || colIndex < 0) return string.Empty;
            var cell = row.GetCell(colIndex);
            if (cell == null) return string.Empty;

            switch (cell.CellType)
            {
                case CellType.String:
                    return cell.StringCellValue?.Trim() ?? string.Empty;
                case CellType.Numeric:
                    return cell.NumericCellValue.ToString();
                case CellType.Boolean:
                    return cell.BooleanCellValue.ToString();
                default:
                    return cell.ToString()?.Trim() ?? string.Empty;
            }
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
