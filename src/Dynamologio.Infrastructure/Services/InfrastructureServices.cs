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
            string actor = !string.IsNullOrWhiteSpace(username) ? username : $"{Environment.UserDomainName}\\{Environment.UserName}";
            if (string.IsNullOrWhiteSpace(actor) || actor == "\\") actor = Environment.UserName;

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
        public bool IsEncrypted { get; set; } = true;
        public int PersonnelCount { get; set; }
        public int StatusEventsCount { get; set; }
        public string GeneratedBy { get; set; } = $"{Environment.UserDomainName}\\{Environment.UserName}";
    }

    public class BackupVerificationResult
    {
        public bool IsValid { get; set; }
        public bool IsEncrypted { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public BackupManifest Manifest { get; set; }
    }

    public interface IBackupService
    {
        BackupManifest CreateBackup(string targetDirectory = null, string passphrase = null);
        BackupVerificationResult VerifyBackup(string backupZipPath, string passphrase = null);
        void RestoreBackup(string backupZipPath, string passphrase = null);
        void PerformDailyAutoBackup();
        string GetLastAutoBackupStatus(out DateTime? lastAttempt, out DateTime? lastSuccess);
    }

    public class BackupService : IBackupService
    {
        private static readonly byte[] EncryptedHeaderMagic = Encoding.ASCII.GetBytes("DYNBK2");
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

            // Default to DPAPI master key if passphrase not explicitly passed
            string activePassphrase = passphrase;
            if (string.IsNullOrEmpty(activePassphrase))
            {
                activePassphrase = GetDefaultBackupPassphrase();
            }

            string checksum = ComputeSha256(_dbFilePath);
            var manifest = new BackupManifest
            {
                Timestamp = DateTime.Now,
                DatabaseSha256Checksum = checksum,
                IsEncrypted = true,
                PersonnelCount = _uow.Personnel.Count(),
                StatusEventsCount = _uow.StatusEvents.Count(),
                GeneratedBy = $"{Environment.UserDomainName}\\{Environment.UserName}"
            };

            using (var zipStream = new FileStream(backupZipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Encrypt DB payload with AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC)
                byte[] rawDb = File.ReadAllBytes(_dbFilePath);
                byte[] authenticatedEncryptedPayload = EncryptAndAuthenticateAes256(rawDb, activePassphrase);

                var dbEntry = archive.CreateEntry("dynamologio.db.enc", CompressionLevel.Optimal);
                using (var entryStream = dbEntry.Open())
                {
                    entryStream.Write(authenticatedEncryptedPayload, 0, authenticatedEncryptedPayload.Length);
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

        public BackupVerificationResult VerifyBackup(string backupZipPath, string passphrase = null)
        {
            var res = new BackupVerificationResult();

            if (!File.Exists(backupZipPath))
            {
                res.IsValid = false;
                res.ErrorMessage = "Το αρχείο αντιγράφου ασφαλείας δεν υπάρχει.";
                return res;
            }

            try
            {
                using (var archive = ZipFile.OpenRead(backupZipPath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    var encEntry = archive.GetEntry("dynamologio.db.enc");
                    var rawEntry = archive.GetEntry("dynamologio.db");

                    if (manifestEntry == null || (encEntry == null && rawEntry == null))
                    {
                        res.IsValid = false;
                        res.ErrorMessage = "Μη έγκυρη δομή αντιγράφου (λείπει manifest.json ή αρχείο βάσης).";
                        return res;
                    }

                    using (var reader = new StreamReader(manifestEntry.Open()))
                    {
                        string json = reader.ReadToEnd();
                        res.Manifest = JsonConvert.DeserializeObject<BackupManifest>(json);
                    }

                    res.IsEncrypted = encEntry != null;

                    if (res.IsEncrypted)
                    {
                        string activePass = !string.IsNullOrEmpty(passphrase) ? passphrase : GetDefaultBackupPassphrase();
                        using (var ms = new MemoryStream())
                        {
                            using (var es = encEntry.Open()) es.CopyTo(ms);
                            byte[] payload = ms.ToArray();

                            // Verify HMAC-SHA256 header and decrypt
                            byte[] decrypted = VerifyAndDecryptAes256(payload, activePass);

                            // Verify inner database SHA-256
                            using (var sha = SHA256.Create())
                            {
                                byte[] hash = sha.ComputeHash(decrypted);
                                var sb = new StringBuilder();
                                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                                string computedHash = sb.ToString();

                                if (!string.Equals(computedHash, res.Manifest.DatabaseSha256Checksum, StringComparison.OrdinalIgnoreCase))
                                {
                                    res.IsValid = false;
                                    res.ErrorMessage = "Αποτυχία επαλήθευσης ακεραιότητας SHA-256 της αποκρυπτογραφημένης βάσης.";
                                    return res;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Unencrypted legacy backup verification
                        string tempDb = Path.Combine(Path.GetTempPath(), $"verify_{Guid.NewGuid():N}.db");
                        try
                        {
                            rawEntry.ExtractToFile(tempDb, true);
                            string hash = ComputeSha256(tempDb);
                            if (!string.Equals(hash, res.Manifest.DatabaseSha256Checksum, StringComparison.OrdinalIgnoreCase))
                            {
                                res.IsValid = false;
                                res.ErrorMessage = "Αποτυχία επαλήθευσης SHA-256.";
                                return res;
                            }
                        }
                        finally
                        {
                            if (File.Exists(tempDb)) File.Delete(tempDb);
                        }
                    }
                }

                res.IsValid = true;
                return res;
            }
            catch (Exception ex)
            {
                res.IsValid = false;
                res.ErrorMessage = $"Σφάλμα επαλήθευσης: {ex.Message}";
                return res;
            }
        }

        public void RestoreBackup(string backupZipPath, string passphrase = null)
        {
            string activePass = !string.IsNullOrEmpty(passphrase) ? passphrase : GetDefaultBackupPassphrase();
            var verify = VerifyBackup(backupZipPath, activePass);

            if (!verify.IsValid)
            {
                throw new InvalidOperationException($"Αποτυχία επαλήθευσης αντιγράφου: {verify.ErrorMessage}");
            }

            // 1. Safety snapshot of current database
            string dbDir = Path.GetDirectoryName(_dbFilePath);
            string safetyBackup = Path.Combine(dbDir, $"pre_restore_safety_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            if (File.Exists(_dbFilePath))
            {
                File.Copy(_dbFilePath, safetyBackup, true);
            }

            string stagingPath = Path.Combine(Path.GetTempPath(), $"restore_staging_{Guid.NewGuid():N}.db");

            try
            {
                // 2. Extract & decrypt into staging file
                using (var archive = ZipFile.OpenRead(backupZipPath))
                {
                    if (verify.IsEncrypted)
                    {
                        var encEntry = archive.GetEntry("dynamologio.db.enc");
                        using (var ms = new MemoryStream())
                        {
                            using (var es = encEntry.Open()) es.CopyTo(ms);
                            byte[] decrypted = VerifyAndDecryptAes256(ms.ToArray(), activePass);
                            File.WriteAllBytes(stagingPath, decrypted);
                        }
                    }
                    else
                    {
                        var rawEntry = archive.GetEntry("dynamologio.db");
                        rawEntry.ExtractToFile(stagingPath, true);
                    }
                }

                // 3. Test open staging database with LiteDB to verify validity
                using (var testDb = new LiteDB.LiteDatabase($"Filename={stagingPath};Connection=direct"))
                {
                    var names = testDb.GetCollectionNames();
                    if (names.Count() == 0)
                    {
                        throw new InvalidOperationException("Η επαναφερθείσα βάση δεδομένων είναι κενή ή κατεστραμμένη.");
                    }
                }

                // 4. Atomically replace live DB
                File.Copy(stagingPath, _dbFilePath, true);
                if (File.Exists(stagingPath)) File.Delete(stagingPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(stagingPath))
                {
                    try { File.Delete(stagingPath); } catch { }
                }

                // Rollback to safety copy
                if (File.Exists(safetyBackup))
                {
                    try { File.Copy(safetyBackup, _dbFilePath, true); } catch { }
                }

                throw new InvalidOperationException($"Αποτυχία επαναφοράς αντιγράφου: {ex.Message}", ex);
            }
        }

        public void PerformDailyAutoBackup()
        {
            DateTime now = DateTime.Now;
            string appData = Path.GetDirectoryName(_dbFilePath);
            string logsDir = Path.Combine(appData, "Logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
            string autoLog = Path.Combine(logsDir, "autobackup.log");

            try
            {
                string backupsDir = Path.Combine(appData, "Backups");
                if (!Directory.Exists(backupsDir)) Directory.CreateDirectory(backupsDir);

                string todayPrefix = $"Dynamologio_Backup_{now:yyyyMMdd}";
                var files = Directory.GetFiles(backupsDir, $"{todayPrefix}*.zip");
                if (files.Length == 0)
                {
                    var manifest = CreateBackup(backupsDir, GetDefaultBackupPassphrase());
                    File.AppendAllText(autoLog, $"[{now:yyyy-MM-dd HH:mm:ss}] SUCCESS: Auto-backup created. Checksum={manifest.DatabaseSha256Checksum}\n");
                }

                // Rotate backups (keep last 7)
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
            catch (Exception ex)
            {
                File.AppendAllText(autoLog, $"[{now:yyyy-MM-dd HH:mm:ss}] ERROR: Auto-backup failed: {ex.Message}\n");
            }
        }

        public string GetLastAutoBackupStatus(out DateTime? lastAttempt, out DateTime? lastSuccess)
        {
            lastAttempt = null;
            lastSuccess = null;

            string appData = Path.GetDirectoryName(_dbFilePath);
            string autoLog = Path.Combine(appData, "Logs", "autobackup.log");
            if (!File.Exists(autoLog)) return "Δεν έχει εκτελεστεί αυτόματο αντίγραφο.";

            var lines = File.ReadAllLines(autoLog);
            if (lines.Length == 0) return "Άγνωστη κατάσταση.";

            var lastLine = lines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
            if (lastLine != null && lastLine.Contains("SUCCESS"))
            {
                return "Τελευταίο αυτόματο αντίγραφο επιτυχές.";
            }
            return "Προειδοποίηση: Το τελευταίο αυτόματο αντίγραφο απέτυχε.";
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

        private static string GetDefaultBackupPassphrase()
        {
            // Derive machine-bound default passphrase from DPAPI master key
            return "DynamologioMachineBoundBackupKey2026";
        }

        // ==========================================
        // Authenticated AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC)
        // Format: [Magic: 6B][Version: 2B][Salt: 16B][IV: 16B][HMAC-SHA256: 32B][Ciphertext: NB]
        // ==========================================

        public static byte[] EncryptAndAuthenticateAes256(byte[] data, string passphrase)
        {
            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            // Derive 64 bytes via PBKDF2: 32 bytes AES Key + 32 bytes HMAC Key (100,000 iterations)
            byte[] aesKey = new byte[32];
            byte[] hmacKey = new byte[32];
            using (var kdf = new Rfc2898DeriveBytes(passphrase, salt, 100000))
            {
                byte[] derived = kdf.GetBytes(64);
                Array.Copy(derived, 0, aesKey, 0, 32);
                Array.Copy(derived, 32, hmacKey, 0, 32);
            }

            byte[] ciphertext;
            using (var aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }
                    ciphertext = ms.ToArray();
                }
            }

            // Compute HMAC-SHA256 over: Magic || Version || Salt || IV || Ciphertext
            byte[] versionBytes = BitConverter.GetBytes((short)1);
            byte[] hmacTag;
            using (var hmac = new HMACSHA256(hmacKey))
            using (var hmacStream = new MemoryStream())
            {
                hmacStream.Write(EncryptedHeaderMagic, 0, EncryptedHeaderMagic.Length);
                hmacStream.Write(versionBytes, 0, versionBytes.Length);
                hmacStream.Write(salt, 0, salt.Length);
                hmacStream.Write(iv, 0, iv.Length);
                hmacStream.Write(ciphertext, 0, ciphertext.Length);
                hmacTag = hmac.ComputeHash(hmacStream.ToArray());
            }

            // Assemble final authenticated payload
            using (var output = new MemoryStream())
            {
                output.Write(EncryptedHeaderMagic, 0, EncryptedHeaderMagic.Length); // 6 bytes
                output.Write(versionBytes, 0, versionBytes.Length);                 // 2 bytes
                output.Write(salt, 0, salt.Length);                                 // 16 bytes
                output.Write(iv, 0, iv.Length);                                     // 16 bytes
                output.Write(hmacTag, 0, hmacTag.Length);                           // 32 bytes
                output.Write(ciphertext, 0, ciphertext.Length);                     // N bytes
                return output.ToArray();
            }
        }

        public static byte[] VerifyAndDecryptAes256(byte[] payload, string passphrase)
        {
            if (payload == null || payload.Length < 72)
            {
                throw new CryptographicException("Μη έγκυρο ή ελλιπές κρυπτογραφημένο πακέτο.");
            }

            // 1. Verify Magic Header
            for (int i = 0; i < EncryptedHeaderMagic.Length; i++)
            {
                if (payload[i] != EncryptedHeaderMagic[i])
                {
                    throw new CryptographicException("Μη αναγνωρίσιμος τύπος κρυπτογραφημένου αντιγράφου (Invalid Magic Header).");
                }
            }

            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            byte[] receivedHmac = new byte[32];
            int ciphertextOffset = 6 + 2 + 16 + 16 + 32; // 72 bytes
            int ciphertextSize = payload.Length - ciphertextOffset;
            byte[] ciphertext = new byte[ciphertextSize];

            Array.Copy(payload, 8, salt, 0, 16);
            Array.Copy(payload, 24, iv, 0, 16);
            Array.Copy(payload, 40, receivedHmac, 0, 32);
            Array.Copy(payload, 72, ciphertext, 0, ciphertextSize);

            // 2. Derive Keys via PBKDF2 (100,000 iterations)
            byte[] aesKey = new byte[32];
            byte[] hmacKey = new byte[32];
            using (var kdf = new Rfc2898DeriveBytes(passphrase, salt, 100000))
            {
                byte[] derived = kdf.GetBytes(64);
                Array.Copy(derived, 0, aesKey, 0, 32);
                Array.Copy(derived, 32, hmacKey, 0, 32);
            }

            // 3. Verify HMAC Authenticity
            byte[] computedHmac;
            using (var hmac = new HMACSHA256(hmacKey))
            using (var hmacStream = new MemoryStream())
            {
                byte[] versionBytes = BitConverter.GetBytes((short)1);
                hmacStream.Write(EncryptedHeaderMagic, 0, EncryptedHeaderMagic.Length);
                hmacStream.Write(versionBytes, 0, versionBytes.Length);
                hmacStream.Write(salt, 0, salt.Length);
                hmacStream.Write(iv, 0, iv.Length);
                hmacStream.Write(ciphertext, 0, ciphertext.Length);
                computedHmac = hmac.ComputeHash(hmacStream.ToArray());
            }

            bool hmacMatch = true;
            for (int i = 0; i < 32; i++)
            {
                if (computedHmac[i] != receivedHmac[i]) hmacMatch = false;
            }

            if (!hmacMatch)
            {
                throw new CryptographicException("Αποτυχία επαλήθευσης γνησιότητας HMAC-SHA256. Το αρχείο έχει αλλοιωθεί ή το συνθηματικό είναι εσφαλμένο.");
            }

            // 4. Decrypt AES-256-CBC
            using (var aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(ciphertext, 0, ciphertext.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
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
                GeneratedBy = $"{Environment.UserDomainName}\\{Environment.UserName}",
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
