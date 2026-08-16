using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Newtonsoft.Json;

namespace Dynamologio.Infrastructure.Services
{
    public interface IAuditService
    {
        void LogAction(AuditAction action, string entityType, string entityId, string summary, object oldValue = null, object newValue = null, Guid? importBatchId = null, string username = "OPERATOR");
    }

    public class AuditService : IAuditService
    {
        private readonly IUnitOfWork _uow;

        public AuditService(IUnitOfWork uow)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        public void LogAction(AuditAction action, string entityType, string entityId, string summary, object oldValue = null, object newValue = null, Guid? importBatchId = null, string username = "OPERATOR")
        {
            try
            {
                var audit = new AuditEvent
                {
                    Username = username,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Summary = summary,
                    OldValueJson = oldValue != null ? JsonConvert.SerializeObject(oldValue) : string.Empty,
                    NewValueJson = newValue != null ? JsonConvert.SerializeObject(newValue) : string.Empty,
                    ImportBatchId = importBatchId,
                    AppVersion = "1.0.0.0"
                };

                _uow.AuditEvents.Insert(audit);
            }
            catch
            {
                // Fallback: Do not crash business operations if audit logging fails
            }
        }
    }

    public class BackupManifest
    {
        public string AppVersion { get; set; } = "1.0.0.0";
        public int SchemaVersion { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string DatabaseFileName { get; set; } = "dynamologio.db";
        public string DatabaseSha256 { get; set; } = string.Empty;
        public int PersonnelCount { get; set; }
        public int StatusEventsCount { get; set; }
    }

    public interface IBackupService
    {
        string CreateBackup(string targetDirectory = null);
        bool VerifyBackup(string backupZipPath, out BackupManifest manifest, out string errorMessage);
        bool RestoreBackup(string backupZipPath, out string errorMessage);
        void PerformDailyAutoBackup();
    }

    public class BackupService : IBackupService
    {
        private readonly string _dbFilePath;
        private readonly IUnitOfWork _uow;

        public BackupService(string dbFilePath, IUnitOfWork uow)
        {
            _dbFilePath = dbFilePath;
            _uow = uow;
        }

        public string CreateBackup(string targetDirectory = null)
        {
            if (!File.Exists(_dbFilePath))
            {
                throw new FileNotFoundException("Το αρχείο της βάσης δεδομένων δεν βρέθηκε.", _dbFilePath);
            }

            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                string appData = Path.GetDirectoryName(_dbFilePath);
                targetDirectory = Path.Combine(appData, "Backups");
            }

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupZipPath = Path.Combine(targetDirectory, $"Dynamologio_Backup_{timestamp}.zip");

            string sha256 = ComputeSha256(_dbFilePath);
            var manifest = new BackupManifest
            {
                CreatedAt = DateTime.Now,
                DatabaseSha256 = sha256,
                PersonnelCount = _uow.Personnel.Count(),
                StatusEventsCount = _uow.StatusEvents.Count()
            };

            using (var zipStream = new FileStream(backupZipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Add DB
                archive.CreateEntryFromFile(_dbFilePath, "dynamologio.db", CompressionLevel.Optimal);

                // Add Manifest
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var entryStream = manifestEntry.Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                {
                    writer.Write(JsonConvert.SerializeObject(manifest, Formatting.Indented));
                }
            }

            return backupZipPath;
        }

        public bool VerifyBackup(string backupZipPath, out BackupManifest manifest, out string errorMessage)
        {
            manifest = null;
            errorMessage = string.Empty;

            if (!File.Exists(backupZipPath))
            {
                errorMessage = "Το αρχείο αντιγράφου ασφαλείας δεν υπάρχει.";
                return false;
            }

            try
            {
                using (var archive = ZipFile.OpenRead(backupZipPath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    var dbEntry = archive.GetEntry("dynamologio.db");

                    if (manifestEntry == null || dbEntry == null)
                    {
                        errorMessage = "Μη έγκυρο αρχείο αντιγράφου ασφαλείας (λείπει το manifest.json ή η βάση).";
                        return false;
                    }

                    using (var reader = new StreamReader(manifestEntry.Open()))
                    {
                        string json = reader.ReadToEnd();
                        manifest = JsonConvert.DeserializeObject<BackupManifest>(json);
                    }

                    // Extract DB to temp and verify checksum
                    string tempDb = Path.Combine(Path.GetTempPath(), $"verify_{Guid.NewGuid():N}.db");
                    try
                    {
                        dbEntry.ExtractToFile(tempDb, true);
                        string extractedHash = ComputeSha256(tempDb);

                        if (!string.Equals(extractedHash, manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            errorMessage = "Αποτυχία επαλήθευσης ακεραιότητας SHA-256.";
                            return false;
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempDb)) File.Delete(tempDb);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Σφάλμα ανάγνωσης αντιγράφου: {ex.Message}";
                return false;
            }
        }

        public bool RestoreBackup(string backupZipPath, out string errorMessage)
        {
            if (!VerifyBackup(backupZipPath, out var manifest, out errorMessage))
            {
                return false;
            }

            try
            {
                // 1. Create Pre-Restore safety backup of current live DB
                if (File.Exists(_dbFilePath))
                {
                    string safetyBackup = Path.Combine(Path.GetDirectoryName(_dbFilePath), $"pre_restore_safety_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                    File.Copy(_dbFilePath, safetyBackup, true);
                }

                // 2. Extract restored DB
                using (var archive = ZipFile.OpenRead(backupZipPath))
                {
                    var dbEntry = archive.GetEntry("dynamologio.db");
                    dbEntry.ExtractToFile(_dbFilePath, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Σφάλμα κατά την επαναφορά: {ex.Message}";
                return false;
            }
        }

        public void PerformDailyAutoBackup()
        {
            try
            {
                string backupsDir = Path.Combine(Path.GetDirectoryName(_dbFilePath), "Backups");
                if (!Directory.Exists(backupsDir)) Directory.CreateDirectory(backupsDir);

                // Check if backup already exists for today
                string todayPrefix = $"Dynamologio_Backup_{DateTime.Now:yyyyMMdd}";
                var files = Directory.GetFiles(backupsDir, $"{todayPrefix}*.zip");
                if (files.Length == 0)
                {
                    CreateBackup(backupsDir);
                }

                // Rotate: Keep latest 7 backups
                var allBackups = new DirectoryInfo(backupsDir).GetFiles("Dynamologio_Backup_*.zip");
                if (allBackups.Length > 7)
                {
                    var toDelete = allBackups.OrderByDescending(f => f.CreationTime).Skip(7);
                    foreach (var f in toDelete)
                    {
                        try { f.Delete(); } catch { }
                    }
                }
            }
            catch
            {
                // Silent fail on background auto-backup
            }
        }

        public static string ComputeSha256(string filePath)
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

    public class DiagnosticPackageService
    {
        public static string GenerateDiagnosticPackage(IUnitOfWork uow, string dbPath, string outputDir = null)
        {
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            }

            string zipPath = Path.Combine(outputDir, $"Dynamologio_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

            var diagnosticInfo = new
            {
                GeneratedAt = DateTime.Now,
                OSVersion = Environment.OSVersion.ToString(),
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess,
                CLRVersion = Environment.Version.ToString(),
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                TotalPersonnelCount = uow.Personnel.Count(),
                ActivePersonnelCount = uow.Personnel.Find(p => !p.IsArchived).Count(),
                StatusEventsCount = uow.StatusEvents.Count(),
                AuditEventsCount = uow.AuditEvents.Count(),
                DatabaseSize = File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0
            };

            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var infoEntry = archive.CreateEntry("system_diagnostics.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(infoEntry.Open(), Encoding.UTF8))
                {
                    writer.Write(JsonConvert.SerializeObject(diagnosticInfo, Formatting.Indented));
                }
            }

            return zipPath;
        }
    }
}
