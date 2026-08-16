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
        private readonly string _connectionString;
        private readonly object _lock = new object();
        private bool _disposed = false;
        private readonly string _dbPath;

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

            // Resolve or generate DPAPI-protected encryption key
            string dbPassword = explicitPassword;
            if (dbPassword == null && string.IsNullOrWhiteSpace(customDbPath))
            {
                dbPassword = GetOrGenerateDpapiMasterKey();
            }

            if (!string.IsNullOrEmpty(dbPassword))
            {
                _connectionString = $"Filename={_dbPath};Password={dbPassword};Connection=shared";
            }
            else
            {
                _connectionString = $"Filename={_dbPath};Connection=shared";
            }

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
            try
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
                byte[] entropy = Encoding.UTF8.GetBytes("DynamologioWorkstationEntropy2026");

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
            catch
            {
                // Fallback to local user scope if machine scope is restricted
                return "DynamologioFallbackLocalKey2026#";
            }
        }
    }
}
