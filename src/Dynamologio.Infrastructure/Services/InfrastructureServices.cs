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
        void LogAction(AuditAction action, string entityType, string entityId, string summary, object oldValue = null, object newValue = null, Guid? importBatchId = null, string username = null);
    }

    public class AuditService : IAuditService
    {
        private readonly IUnitOfWork _uow;

        public AuditService(IUnitOfWork uow)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        public void LogAction(AuditAction action, string entityType, string entityId, string summary, object oldValue = null, object newValue = null, Guid? importBatchId = null, string username = null)
        {
            string actor = !string.IsNullOrWhiteSpace(username) ? username : Environment.UserName;
            if (string.IsNullOrWhiteSpace(actor)) actor = "LOCAL_USER";

            var audit = new AuditEvent
            {
                Username = actor,
                Action = action,
                EntityType = entityType ?? string.Empty,
                EntityId = entityId ?? string.Empty,
                Summary = summary ?? string.Empty,
                OldValueJson = oldValue != null ? JsonConvert.SerializeObject(oldValue) : string.Empty,
                NewValueJson = newValue != null ? JsonConvert.SerializeObject(newValue) : string.Empty,
                ImportBatchId = importBatchId,
                AppVersion = "1.0.0.0"
            };

            _uow.AuditEvents.Insert(audit);
        }
    }

    public class BackupManifest
    {
        public string AppVersion { get; set; } = "1.0.0.0";
        public int SchemaVersion { get; set; } = 1;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string DatabaseFileName { get; set; } = "dynamologio.db";
        public string DatabaseSha256Checksum { get; set; } = string.Empty;
        public bool IsEncrypted { get; set; } = false;
        public int PersonnelCount { get; set; }
        public int StatusEventsCount { get; set; }
        public string GeneratedBy { get; set; } = Environment.UserName;
    }

    public interface IBackupService
    {
        BackupManifest CreateBackup(string targetDirectory = null, string passphrase = null);
        bool VerifyBackup(string backupZipPath, out BackupManifest manifest, out string errorMessage);
        void RestoreBackup(string backupZipPath, string passphrase = null);
        void PerformDailyAutoBackup();
    }

    public class BackupService : IBackupService
    {
        private readonly string _dbFilePath;
        private readonly IUnitOfWork _uow;

        public BackupService(IUnitOfWork uow, string dbFilePath)
        {
            _uow = uow;
            _dbFilePath = dbFilePath;
        }

        public BackupManifest CreateBackup(string targetDirectory = null, string passphrase = null)
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

            // Compute exact SHA-256 checksum of live DB
            string checksum = ComputeSha256(_dbFilePath);
            var manifest = new BackupManifest
            {
                Timestamp = DateTime.Now,
                DatabaseSha256Checksum = checksum,
                IsEncrypted = !string.IsNullOrEmpty(passphrase),
                PersonnelCount = _uow.Personnel.Count(),
                StatusEventsCount = _uow.StatusEvents.Count(),
                GeneratedBy = Environment.UserName
            };

            using (var zipStream = new FileStream(backupZipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                if (string.IsNullOrEmpty(passphrase))
                {
                    archive.CreateEntryFromFile(_dbFilePath, "dynamologio.db", CompressionLevel.Optimal);
                }
                else
                {
                    // Encrypt DB payload with AES-256 & PBKDF2
                    byte[] rawDb = File.ReadAllBytes(_dbFilePath);
                    byte[] encryptedDb = EncryptBytesAes256(rawDb, passphrase);
                    var dbEntry = archive.CreateEntry("dynamologio.db.enc", CompressionLevel.Optimal);
                    using (var entryStream = dbEntry.Open())
                    {
                        entryStream.Write(encryptedDb, 0, encryptedDb.Length);
                    }
                }

                // Add manifest.json
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var entryStream = manifestEntry.Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                {
                    writer.Write(JsonConvert.SerializeObject(manifest, Formatting.Indented));
                }
            }

            return manifest;
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
                    var dbEntry = archive.GetEntry("dynamologio.db") ?? archive.GetEntry("dynamologio.db.enc");

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

                    if (!manifest.IsEncrypted)
                    {
                        string tempDb = Path.Combine(Path.GetTempPath(), $"verify_{Guid.NewGuid():N}.db");
                        try
                        {
                            dbEntry.ExtractToFile(tempDb, true);
                            string extractedHash = ComputeSha256(tempDb);

                            if (!string.Equals(extractedHash, manifest.DatabaseSha256Checksum, StringComparison.OrdinalIgnoreCase))
                            {
                                errorMessage = "Αποτυχία επαλήθευσης ακεραιότητας SHA-256. Το αρχείο έχει τροποποιηθεί ή αλλοιωθεί.";
                                return false;
                            }
                        }
                        finally
                        {
                            if (File.Exists(tempDb)) File.Delete(tempDb);
                        }
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

        public void RestoreBackup(string backupZipPath, string passphrase = null)
        {
            if (!VerifyBackup(backupZipPath, out var manifest, out var errorMessage))
            {
                throw new InvalidOperationException($"Αποτυχία επαλήθευσης αντιγράφου: {errorMessage}");
            }

            // 1. Create Pre-restore safety snapshot
            string dbDir = Path.GetDirectoryName(_dbFilePath);
            string safetyBackup = Path.Combine(dbDir, $"pre_restore_safety_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            if (File.Exists(_dbFilePath))
            {
                File.Copy(_dbFilePath, safetyBackup, true);
            }

            try
            {
                // 2. Extract to temp location first
                string tempExtracted = Path.Combine(Path.GetTempPath(), $"restore_staging_{Guid.NewGuid():N}.db");

                using (var archive = ZipFile.OpenRead(backupZipPath))
                {
                    if (manifest.IsEncrypted)
                    {
                        var encEntry = archive.GetEntry("dynamologio.db.enc");
                        if (encEntry == null) throw new InvalidOperationException("Λείπει το κρυπτογραφημένο αρχείο βάσης.");

                        using (var ms = new MemoryStream())
                        {
                            using (var es = encEntry.Open()) es.CopyTo(ms);
                            byte[] decrypted = DecryptBytesAes256(ms.ToArray(), passphrase);
                            File.WriteAllBytes(tempExtracted, decrypted);
                        }
                    }
                    else
                    {
                        var dbEntry = archive.GetEntry("dynamologio.db");
                        dbEntry.ExtractToFile(tempExtracted, true);
                    }
                }

                // 3. Verify SHA-256 of extracted database
                string extractedChecksum = ComputeSha256(tempExtracted);
                if (!string.Equals(extractedChecksum, manifest.DatabaseSha256Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Αποτυχία επαλήθευσης ακεραιότητας SHA-256 κατά την επαναφορά.");
                }

                // 4. Overwrite live DB
                File.Copy(tempExtracted, _dbFilePath, true);
                if (File.Exists(tempExtracted)) File.Delete(tempExtracted);
            }
            catch
            {
                // Rollback to safety copy if restore failed
                if (File.Exists(safetyBackup))
                {
                    try { File.Copy(safetyBackup, _dbFilePath, true); } catch { }
                }
                throw;
            }
        }

        public void PerformDailyAutoBackup()
        {
            try
            {
                string backupsDir = Path.Combine(Path.GetDirectoryName(_dbFilePath), "Backups");
                if (!Directory.Exists(backupsDir)) Directory.CreateDirectory(backupsDir);

                string todayPrefix = $"Dynamologio_Backup_{DateTime.Now:yyyyMMdd}";
                var files = Directory.GetFiles(backupsDir, $"{todayPrefix}*.zip");
                if (files.Length == 0)
                {
                    CreateBackup(backupsDir);
                }

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

        private static byte[] EncryptBytesAes256(byte[] data, string passphrase)
        {
            byte[] salt = new byte[16];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(salt);

            using (var keyDerivation = new Rfc2898DeriveBytes(passphrase, salt, 50000))
            {
                byte[] key = keyDerivation.GetBytes(32);
                byte[] iv = keyDerivation.GetBytes(16);

                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    using (var ms = new MemoryStream())
                    {
                        ms.Write(salt, 0, salt.Length); // Write salt header
                        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(data, 0, data.Length);
                            cs.FlushFinalBlock();
                        }
                        return ms.ToArray();
                    }
                }
            }
        }

        private static byte[] DecryptBytesAes256(byte[] encryptedData, string passphrase)
        {
            if (string.IsNullOrEmpty(passphrase)) throw new ArgumentException("Απαιτείται συνθηματικό αποκρυπτογράφησης.");

            byte[] salt = new byte[16];
            Array.Copy(encryptedData, 0, salt, 0, 16);

            using (var keyDerivation = new Rfc2898DeriveBytes(passphrase, salt, 50000))
            {
                byte[] key = keyDerivation.GetBytes(32);
                byte[] iv = keyDerivation.GetBytes(16);

                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(encryptedData, 16, encryptedData.Length - 16);
                            cs.FlushFinalBlock();
                        }
                        return ms.ToArray();
                    }
                }
            }
        }
    }

    public interface IDiagnosticPackageService
    {
        string ExportDiagnosticPackage(string outputZipPath = null);
    }

    public class DiagnosticPackageService : IDiagnosticPackageService
    {
        private readonly IUnitOfWork _uow;
        private readonly string _dbPath;

        public DiagnosticPackageService(IUnitOfWork uow, string dbPath)
        {
            _uow = uow;
            _dbPath = dbPath;
        }

        public string ExportDiagnosticPackage(string outputZipPath = null)
        {
            if (string.IsNullOrWhiteSpace(outputZipPath))
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                outputZipPath = Path.Combine(desktop, $"Dynamologio_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            }

            var diagnosticInfo = new
            {
                GeneratedAt = DateTime.Now,
                GeneratedBy = Environment.UserName,
                OSVersion = Environment.OSVersion.ToString(),
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess,
                CLRVersion = Environment.Version.ToString(),
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                TotalPersonnelCount = _uow.Personnel.Count(),
                ActivePersonnelCount = _uow.Personnel.Find(p => !p.IsArchived).Count(),
                StatusEventsCount = _uow.StatusEvents.Count(),
                AuditEventsCount = _uow.AuditEvents.Count(),
                DatabaseSize = File.Exists(_dbPath) ? new FileInfo(_dbPath).Length : 0
            };

            using (var zipStream = new FileStream(outputZipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var infoEntry = archive.CreateEntry("system_diagnostics.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(infoEntry.Open(), Encoding.UTF8))
                {
                    writer.Write(JsonConvert.SerializeObject(diagnosticInfo, Formatting.Indented));
                }
            }

            return outputZipPath;
        }
    }
}
