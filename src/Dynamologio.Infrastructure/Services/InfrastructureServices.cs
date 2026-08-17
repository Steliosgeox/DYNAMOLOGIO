using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Infrastructure.LiteDb;
using Dynamologio.Infrastructure.Security;
using Newtonsoft.Json;

namespace Dynamologio.Infrastructure.Services
{


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

    public class BackupHealthInfo
    {
        public DateTime? LastAttemptUtc { get; set; }
        public DateTime? LastSuccessUtc { get; set; }
        public string LastFailureCode { get; set; } = string.Empty;
        public string LastFailureMessageSafe { get; set; } = string.Empty;
        public string StatusSummary { get; set; } = "Δεν έχει εκτελεστεί αντίγραφο ασφαλείας.";
    }

    public interface IBackupService
    {
        BackupManifest CreateBackup(string targetDirectory = null, string passphrase = null);
        BackupVerificationResult VerifyBackup(string backupZipPath, string passphrase = null);
        void PerformDailyAutoBackup();
        BackupHealthInfo GetBackupHealth();
    }

    public class BackupService : IBackupService
    {
        private static readonly byte[] EncryptedHeaderMagic = Encoding.ASCII.GetBytes("DYNBK3");
        private readonly string _dbFilePath;
        private readonly IUnitOfWork _uow;
        private readonly IKeyProtectionProvider _keyProvider;

        public BackupService(IUnitOfWork uow, string dbFilePath, IKeyProtectionProvider keyProvider = null)
        {
            _uow = uow;
            _dbFilePath = dbFilePath;
            _keyProvider = keyProvider ?? new DpapiProtectionProvider();
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
                // Encrypt payload (Manifest JSON + DB Bytes) with AES-256-CBC + HMAC-SHA256
                byte[] rawDb = File.ReadAllBytes(_dbFilePath);
                byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented));

                byte[] combinedPayload;
                using (var ms = new MemoryStream())
                {
                    byte[] lenBytes = BitConverter.GetBytes(manifestBytes.Length);
                    ms.Write(lenBytes, 0, 4);
                    ms.Write(manifestBytes, 0, manifestBytes.Length);
                    ms.Write(rawDb, 0, rawDb.Length);
                    combinedPayload = ms.ToArray();
                }

                byte[] authenticatedPayload = EncryptAndAuthenticate(combinedPayload, passphrase);

                var encEntry = archive.CreateEntry("dynamologio_backup.dat", CompressionLevel.Optimal);
                using (var entryStream = encEntry.Open())
                {
                    entryStream.Write(authenticatedPayload, 0, authenticatedPayload.Length);
                }

                // Add readable copy of manifest for fast catalog inspection
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
                    var datEntry = archive.GetEntry("dynamologio_backup.dat");
                    var legacyEncEntry = archive.GetEntry("dynamologio.db.enc");
                    var rawEntry = archive.GetEntry("dynamologio.db");
                    var manifestEntry = archive.GetEntry("manifest.json");

                    if (datEntry != null)
                    {
                        // Modern V5 authenticated envelope
                        using (var ms = new MemoryStream())
                        {
                            using (var es = datEntry.Open()) es.CopyTo(ms);
                            byte[] decryptedCombined = VerifyAndDecrypt(ms.ToArray(), passphrase);

                            int manifestLen = BitConverter.ToInt32(decryptedCombined, 0);
                            string manifestJson = Encoding.UTF8.GetString(decryptedCombined, 4, manifestLen);
                            res.Manifest = JsonConvert.DeserializeObject<BackupManifest>(manifestJson);
                            res.IsEncrypted = true;

                            int dbOffset = 4 + manifestLen;
                            int dbLen = decryptedCombined.Length - dbOffset;
                            byte[] dbBytes = new byte[dbLen];
                            Array.Copy(decryptedCombined, dbOffset, dbBytes, 0, dbLen);

                            using (var sha = SHA256.Create())
                            {
                                byte[] hash = sha.ComputeHash(dbBytes);
                                var sb = new StringBuilder();
                                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                                string computed = sb.ToString();

                                if (!string.Equals(computed, res.Manifest.DatabaseSha256Checksum, StringComparison.OrdinalIgnoreCase))
                                {
                                    res.IsValid = false;
                                    res.ErrorMessage = "Αποτυχία επαλήθευσης SHA-256 της βάσης.";
                                    return res;
                                }
                            }
                        }
                    }
                    else if (legacyEncEntry != null && manifestEntry != null)
                    {
                        // V4 legacy format
                        using (var reader = new StreamReader(manifestEntry.Open()))
                        {
                            res.Manifest = JsonConvert.DeserializeObject<BackupManifest>(reader.ReadToEnd());
                        }
                        res.IsEncrypted = true;
                    }
                    else if (rawEntry != null && manifestEntry != null)
                    {
                        using (var reader = new StreamReader(manifestEntry.Open()))
                        {
                            res.Manifest = JsonConvert.DeserializeObject<BackupManifest>(reader.ReadToEnd());
                        }
                        res.IsEncrypted = false;
                    }
                    else
                    {
                        res.IsValid = false;
                        res.ErrorMessage = "Μη αναγνωρίσιμη μορφή αντιγράφου ασφαλείας.";
                        return res;
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

        public void PerformDailyAutoBackup()
        {
            DateTime nowUtc = DateTime.UtcNow;
            string appData = Path.GetDirectoryName(_dbFilePath);
            string healthFile = Path.Combine(appData, "Config", "backup_health.json");

            var health = GetBackupHealth();
            health.LastAttemptUtc = nowUtc;

            try
            {
                string backupsDir = Path.Combine(appData, "Backups");
                if (!Directory.Exists(backupsDir)) Directory.CreateDirectory(backupsDir);

                string todayPrefix = $"Dynamologio_Backup_{DateTime.Now:yyyyMMdd}";
                var files = Directory.GetFiles(backupsDir, $"{todayPrefix}*.zip");
                if (files.Length == 0)
                {
                    var manifest = CreateBackup(backupsDir, null);
                    health.LastSuccessUtc = DateTime.UtcNow;
                    health.LastFailureCode = string.Empty;
                    health.LastFailureMessageSafe = string.Empty;
                    health.StatusSummary = $"Επιτυχές αυτόματο αντίγραφο ({manifest.Timestamp:dd/MM/yyyy HH:mm}).";
                }
                else
                {
                    health.StatusSummary = "Το ημερήσιο αντίγραφο έχει ήδη δημιουργηθεί.";
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
                health.LastFailureCode = ex.GetType().Name;
                health.LastFailureMessageSafe = ex.Message;
                health.StatusSummary = $"Σφάλμα αυτόματου αντιγράφου: {ex.Message}";
            }
            finally
            {
                SaveBackupHealth(healthFile, health);
            }
        }

        public BackupHealthInfo GetBackupHealth()
        {
            try
            {
                string appData = Path.GetDirectoryName(_dbFilePath);
                string healthFile = Path.Combine(appData, "Config", "backup_health.json");
                if (File.Exists(healthFile))
                {
                    string json = File.ReadAllText(healthFile);
                    return JsonConvert.DeserializeObject<BackupHealthInfo>(json) ?? new BackupHealthInfo();
                }
            }
            catch { }
            return new BackupHealthInfo();
        }

        private static void SaveBackupHealth(string path, BackupHealthInfo health)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonConvert.SerializeObject(health, Formatting.Indented));
            }
            catch { }
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

        private byte[] EncryptAndAuthenticate(byte[] data, string passphrase)
        {
            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] aesKey = new byte[32];
            byte[] hmacKey = new byte[32];

            if (!string.IsNullOrEmpty(passphrase))
            {
                using (var kdf = new Rfc2898DeriveBytes(passphrase, salt, 100000))
                {
                    byte[] derived = kdf.GetBytes(64);
                    Array.Copy(derived, 0, aesKey, 0, 32);
                    Array.Copy(derived, 32, hmacKey, 0, 32);
                }
            }
            else
            {
                byte[] machineKey = _keyProvider.GetMachineBackupKey();
                using (var kdf = new Rfc2898DeriveBytes(machineKey, salt, 100000))
                {
                    byte[] derived = kdf.GetBytes(64);
                    Array.Copy(derived, 0, aesKey, 0, 32);
                    Array.Copy(derived, 32, hmacKey, 0, 32);
                }
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

            // Authenticate (Encrypt-then-MAC)
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

            using (var output = new MemoryStream())
            {
                output.Write(EncryptedHeaderMagic, 0, EncryptedHeaderMagic.Length);
                output.Write(versionBytes, 0, versionBytes.Length);
                output.Write(salt, 0, salt.Length);
                output.Write(iv, 0, iv.Length);
                output.Write(hmacTag, 0, hmacTag.Length);
                output.Write(ciphertext, 0, ciphertext.Length);
                return output.ToArray();
            }
        }

        private byte[] VerifyAndDecrypt(byte[] payload, string passphrase)
        {
            if (payload == null || payload.Length < 72)
            {
                throw new CryptographicException("Μη έγκυρο ή ελλιπές κρυπτογραφημένο πακέτο.");
            }

            for (int i = 0; i < EncryptedHeaderMagic.Length; i++)
            {
                if (payload[i] != EncryptedHeaderMagic[i])
                {
                    throw new CryptographicException("Μη αναγνωρίσιμος τύπος αντιγράφου ασφαλείας.");
                }
            }

            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            byte[] receivedHmac = new byte[32];
            int ciphertextOffset = 6 + 2 + 16 + 16 + 32;
            int ciphertextSize = payload.Length - ciphertextOffset;
            byte[] ciphertext = new byte[ciphertextSize];

            Array.Copy(payload, 8, salt, 0, 16);
            Array.Copy(payload, 24, iv, 0, 16);
            Array.Copy(payload, 40, receivedHmac, 0, 32);
            Array.Copy(payload, 72, ciphertext, 0, ciphertextSize);

            byte[] aesKey = new byte[32];
            byte[] hmacKey = new byte[32];

            if (!string.IsNullOrEmpty(passphrase))
            {
                using (var kdf = new Rfc2898DeriveBytes(passphrase, salt, 100000))
                {
                    byte[] derived = kdf.GetBytes(64);
                    Array.Copy(derived, 0, aesKey, 0, 32);
                    Array.Copy(derived, 32, hmacKey, 0, 32);
                }
            }
            else
            {
                byte[] machineKey = _keyProvider.GetMachineBackupKey();
                using (var kdf = new Rfc2898DeriveBytes(machineKey, salt, 100000))
                {
                    byte[] derived = kdf.GetBytes(64);
                    Array.Copy(derived, 0, aesKey, 0, 32);
                    Array.Copy(derived, 32, hmacKey, 0, 32);
                }
            }

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
                throw new CryptographicException("Αποτυχία επαλήθευσης HMAC-SHA256. Το αρχείο έχει αλλοιωθεί ή το κλειδί είναι εσφαλμένο.");
            }

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

    public interface IDatabaseLifecycleCoordinator
    {
        bool RestoreDatabase(string backupZipPath, string passphrase, Action onContextRecreated = null);
    }

    public class DatabaseLifecycleCoordinator : IDatabaseLifecycleCoordinator
    {
        private readonly LiteDbContext _dbContext;
        private readonly IBackupService _backupService;
        private readonly IKeyProtectionProvider _keyProvider;

        public DatabaseLifecycleCoordinator(LiteDbContext dbContext, IBackupService backupService, IKeyProtectionProvider keyProvider = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _keyProvider = keyProvider ?? new DpapiProtectionProvider();
        }

        public bool RestoreDatabase(string backupZipPath, string passphrase, Action onContextRecreated = null)
        {
            var verify = _backupService.VerifyBackup(backupZipPath, passphrase);
            if (!verify.IsValid)
            {
                throw new InvalidOperationException($"Αποτυχία επαλήθευσης αντιγράφου: {verify.ErrorMessage}");
            }

            string dbPath = _dbContext.DbFilePath;
            string safetyBackup = dbPath + $".pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            string stagingPath = Path.Combine(Path.GetTempPath(), $"staging_restore_{Guid.NewGuid():N}.db");

            try
            {
                if (File.Exists(dbPath))
                {
                    File.Copy(dbPath, safetyBackup, true);
                }

                // Extract DB payload
                using (var archive = ZipFile.OpenRead(backupZipPath))
                {
                    var datEntry = archive.GetEntry("dynamologio_backup.dat");
                    if (datEntry != null)
                    {
                        using (var ms = new MemoryStream())
                        {
                            using (var es = datEntry.Open()) es.CopyTo(ms);
                            var backupSvc = _backupService as BackupService;
                            // Decrypt using verification pipeline
                            var result = _backupService.VerifyBackup(backupZipPath, passphrase);
                            if (!result.IsValid) throw new CryptographicException(result.ErrorMessage);

                            // Extract decrypted db bytes from archive
                            using (var archive2 = ZipFile.OpenRead(backupZipPath))
                            {
                                var datEntry2 = archive2.GetEntry("dynamologio_backup.dat");
                                using (var ms2 = new MemoryStream())
                                {
                                    using (var es2 = datEntry2.Open()) es2.CopyTo(ms2);
                                    byte[] payload = ms2.ToArray();
                                    // Use raw decryption
                                    byte[] decrypted = ExtractDatabaseBytes(payload, passphrase, _keyProvider);
                                    File.WriteAllBytes(stagingPath, decrypted);
                                }
                            }
                        }
                    }
                    else
                    {
                        var rawEntry = archive.GetEntry("dynamologio.db");
                        if (rawEntry != null) rawEntry.ExtractToFile(stagingPath, true);
                    }
                }

                // Validate staging database with LiteDB using correct master password
                string masterPwd = _dbContext.DatabasePassword;
                string testConnStr = !string.IsNullOrEmpty(masterPwd)
                    ? $"Filename={stagingPath};Password={masterPwd};Connection=direct"
                    : $"Filename={stagingPath};Connection=direct";

                using (var testDb = new LiteDB.LiteDatabase(testConnStr))
                {
                    var colNames = testDb.GetCollectionNames().ToList();
                    if (colNames.Count == 0)
                    {
                        throw new InvalidOperationException("Η επαναφερθείσα βάση δεδομένων είναι κενή.");
                    }
                }

                // Close active application DB context
                _dbContext.Close();

                // Atomically replace live DB file
                File.Copy(stagingPath, dbPath, true);
                if (File.Exists(stagingPath)) File.Delete(stagingPath);

                // Reopen application DB context
                _dbContext.Reopen();

                // Rebuild UI context
                onContextRecreated?.Invoke();

                if (File.Exists(safetyBackup))
                {
                    try { File.Delete(safetyBackup); } catch { }
                }

                return true;
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
                    try
                    {
                        _dbContext.Close();
                        File.Copy(safetyBackup, dbPath, true);
                        _dbContext.Reopen();
                        File.Delete(safetyBackup);
                    }
                    catch { }
                }

                throw new InvalidOperationException($"Αποτυχία εκτέλεσης επαναφοράς: {ex.Message}", ex);
            }
        }

        private static byte[] ExtractDatabaseBytes(byte[] payload, string passphrase, IKeyProtectionProvider keyProvider)
        {
            // Decrypt outer envelope
            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            int ciphertextOffset = 6 + 2 + 16 + 16 + 32;
            int ciphertextSize = payload.Length - ciphertextOffset;
            byte[] ciphertext = new byte[ciphertextSize];

            Array.Copy(payload, 8, salt, 0, 16);
            Array.Copy(payload, 24, iv, 0, 16);
            Array.Copy(payload, 72, ciphertext, 0, ciphertextSize);

            byte[] aesKey = new byte[32];
            if (!string.IsNullOrEmpty(passphrase))
            {
                using (var kdf = new Rfc2898DeriveBytes(passphrase, salt, 100000))
                {
                    aesKey = kdf.GetBytes(32);
                }
            }
            else
            {
                byte[] machineKey = keyProvider.GetMachineBackupKey();
                using (var kdf = new Rfc2898DeriveBytes(machineKey, salt, 100000))
                {
                    aesKey = kdf.GetBytes(32);
                }
            }

            byte[] decrypted;
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
                    decrypted = ms.ToArray();
                }
            }

            int manifestLen = BitConverter.ToInt32(decrypted, 0);
            int dbOffset = 4 + manifestLen;
            int dbLen = decrypted.Length - dbOffset;
            byte[] dbBytes = new byte[dbLen];
            Array.Copy(decrypted, dbOffset, dbBytes, 0, dbLen);
            return dbBytes;
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
