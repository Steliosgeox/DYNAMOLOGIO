using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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

        public string DbFilePath => _dbPath;

        public LiteDbContext(string customDbPath = null, string explicitPassword = null)
        {
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

            // Resolve DPAPI-protected master key
            if (explicitPassword != null)
            {
                _password = explicitPassword;
            }
            else if (string.IsNullOrWhiteSpace(customDbPath))
            {
                _password = GetOrGenerateDpapiMasterKey();
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

        private static string GetOrGenerateDpapiMasterKey()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            string configFolder = Path.Combine(appData, "Dynamologio", "Config");
            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
            }

            string keyFile = Path.Combine(configFolder, "master.key");
            byte[] entropy = Encoding.UTF8.GetBytes("DynamologioDPAPIMasterKeyEntropyV4");

            if (File.Exists(keyFile))
            {
                byte[] protectedBytes = File.ReadAllBytes(keyFile);
                byte[] rawBytes = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(rawBytes);
            }
            else
            {
                byte[] rawKey = new byte[32];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(rawKey);
                }

                byte[] protectedBytes = ProtectedData.Protect(rawKey, entropy, DataProtectionScope.LocalMachine);
                File.WriteAllBytes(keyFile, protectedBytes);
                return Convert.ToBase64String(rawKey);
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
            string backupPath = dbFilePath + ".plaintext.bak";
            string stagingEncryptedPath = dbFilePath + ".encrypted.staging";

            try
            {
                File.Copy(dbFilePath, backupPath, true);

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
            }
            catch (Exception ex)
            {
                if (File.Exists(stagingEncryptedPath))
                {
                    try { File.Delete(stagingEncryptedPath); } catch { }
                }

                // Restore original plaintext backup if live DB was touched
                if (File.Exists(backupPath))
                {
                    try { File.Copy(backupPath, dbFilePath, true); } catch { }
                }

                throw new InvalidOperationException($"Αποτυχία κρυπτογράφησης υπάρχουσας βάσης δεδομένων: {ex.Message}", ex);
            }
        }
    }
}
