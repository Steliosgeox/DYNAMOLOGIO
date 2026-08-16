using System;
using System.IO;
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

        public LiteDbContext(string customDbPath = null)
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

            _connectionString = $"Filename={_dbPath};Connection=shared";
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

        public string DatabasePath => _dbPath;

        public ILiteCollection<T> GetCollection<T>(string name = null)
        {
            return _database.GetCollection<T>(name);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    _database?.Dispose();
                    _database = null;
                    _disposed = true;
                }
            }
        }
    }
}
