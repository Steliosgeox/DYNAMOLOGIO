using System;
using System.IO;
using System.Security.Cryptography;
using Dynamologio.Infrastructure.Security;
using LiteDB;

namespace Dynamologio.Infrastructure.LiteDb
{
    public class LiteDbContext : IDisposable
    {
        private LiteDatabase _database;
        private string _connectionString;
        private readonly object _lock = new object();
        private bool _disposed = false;
        private readonly string _dbPath;
        private readonly string _password;
        private readonly IKeyProtectionProvider _keyProvider;

        public string DbFilePath => _dbPath;
        public string DatabasePassword => _password;
        public LiteDbContext() : this(null, null, null) { }
        public LiteDbContext(string customDbPath) : this(customDbPath, null, null) { }
        public LiteDbContext(IKeyProtectionProvider keyProvider) : this(null, null, keyProvider) { }

        public LiteDbContext(
            string customDbPath = null,
            string explicitPassword = null,
            IKeyProtectionProvider keyProvider = null)
        {
            _keyProvider = keyProvider;

            if (!string.IsNullOrWhiteSpace(customDbPath))
            {
                _dbPath = customDbPath;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (string.IsNullOrEmpty(appData))
                {
                    appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }
                string dataFolder = Path.Combine(appData, "Dynamologio", "Data");
                if (!Directory.Exists(dataFolder))
                {
                    Directory.CreateDirectory(dataFolder);
                }
                _dbPath = Path.Combine(dataFolder, "dynamologio.db");
            }

            // Resolve master encryption password
            if (explicitPassword != null)
            {
                _password = explicitPassword;
            }
            else if (_keyProvider != null)
            {
                _password = _keyProvider.GetDatabaseMasterPassword();
            }
            else if (string.IsNullOrWhiteSpace(customDbPath))
            {
                var defaultProvider = new DpapiProtectionProvider();
                _password = defaultProvider.GetDatabaseMasterPassword();
            }
            else
            {
                _password = string.Empty; // Test/custom DB without explicit password
            }

            // Migrate legacy unencrypted database if needed
            MigrateLegacyPlaintextDbIfNeeded(_dbPath, _password);

            _connectionString = !string.IsNullOrEmpty(_password)
                ? $"Filename={_dbPath};Password={_password};Connection=shared"
                : $"Filename={_dbPath};Connection=shared";

            _database = new LiteDatabase(_connectionString);
        }

        public LiteDatabase Database
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(LiteDbContext));
                }
                return _database;
            }
        }

        public ILiteCollection<T> GetCollection<T>(string name = null)
        {
            return string.IsNullOrEmpty(name) ? Database.GetCollection<T>() : Database.GetCollection<T>(name);
        }

        public void Close()
        {
            lock (_lock)
            {
                if (_database != null)
                {
                    try { _database.Dispose(); } catch { }
                    _database = null;
                }
            }
        }

        public void Reopen()
        {
            lock (_lock)
            {
                if (_database == null)
                {
                    _database = new LiteDatabase(_connectionString);
                    _disposed = false;
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    Close();
                    _disposed = true;
                }
            }
        }

        public static void MigrateLegacyPlaintextDbIfNeeded(string dbFilePath, string targetPassword)
        {
            if (!File.Exists(dbFilePath) || string.IsNullOrEmpty(targetPassword)) return;

            bool isPlaintext = false;

            // Test if database is openable without password
            try
            {
                using (var testPlainDb = new LiteDatabase($"Filename={dbFilePath};Connection=direct"))
                {
                    var collections = testPlainDb.GetCollectionNames();
                    isPlaintext = true;
                }
            }
            catch
            {
                isPlaintext = false;
            }

            if (!isPlaintext) return; // Already encrypted or new file

            // Perform atomic rebuild/migration to encrypted format
            string stagingEncryptedPath = dbFilePath + ".encrypted.staging";
            string tempLegacyBackupPath = dbFilePath + ".migration_temp.bak";

            try
            {
                File.Copy(dbFilePath, tempLegacyBackupPath, true);

                if (File.Exists(stagingEncryptedPath)) File.Delete(stagingEncryptedPath);

                using (var plainDb = new LiteDatabase($"Filename={dbFilePath};Connection=direct"))
                using (var encDb = new LiteDatabase($"Filename={stagingEncryptedPath};Password={targetPassword};Connection=direct"))
                {
                    foreach (string colName in plainDb.GetCollectionNames())
                    {
                        var plainCol = plainDb.GetCollection(colName);
                        var encCol = encDb.GetCollection(colName);
                        foreach (var doc in plainCol.FindAll())
                        {
                            encCol.Insert(doc);
                        }

                        // Validate collection counts match
                        if (plainCol.Count() != encCol.Count())
                        {
                            throw new InvalidOperationException($"Ασυμφωνία πλήθους εγγραφών κατά τη μετανάστευση στη συλλογή '{colName}'.");
                        }
                    }
                }

                // Verify staging encrypted DB opens cleanly with password
                using (var verifyDb = new LiteDatabase($"Filename={stagingEncryptedPath};Password={targetPassword};Connection=direct"))
                {
                    var count = verifyDb.GetCollectionNames();
                }

                // Atomically replace live DB with encrypted DB
                File.Copy(stagingEncryptedPath, dbFilePath, true);
                if (File.Exists(stagingEncryptedPath)) File.Delete(stagingEncryptedPath);

                // SEC-002: Securely purge temporary plaintext copy so ZERO plaintext database files remain
                if (File.Exists(tempLegacyBackupPath))
                {
                    try
                    {
                        // Overwrite with zeroes before deletion
                        byte[] zeroes = new byte[new FileInfo(tempLegacyBackupPath).Length];
                        File.WriteAllBytes(tempLegacyBackupPath, zeroes);
                        File.Delete(tempLegacyBackupPath);
                    }
                    catch
                    {
                        File.Delete(tempLegacyBackupPath);
                    }
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(stagingEncryptedPath))
                {
                    try { File.Delete(stagingEncryptedPath); } catch { }
                }

                // Restore original plaintext backup if live DB was touched
                if (File.Exists(tempLegacyBackupPath))
                {
                    try { File.Copy(tempLegacyBackupPath, dbFilePath, true); } catch { }
                    try { File.Delete(tempLegacyBackupPath); } catch { }
                }

                throw new InvalidOperationException($"Αποτυχία κρυπτογράφησης υπάρχουσας βάσης δεδομένων: {ex.Message}", ex);
            }
        }
    }
}
