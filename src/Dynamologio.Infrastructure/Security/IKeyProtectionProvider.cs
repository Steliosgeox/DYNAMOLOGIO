using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LiteDB;

namespace Dynamologio.Infrastructure.Security
{
    public interface IKeyProtectionProvider
    {
        string GetDatabaseMasterPassword();
        byte[] GetMachineBackupKey();
    }

    public class DpapiProtectionProvider : IKeyProtectionProvider
    {
        private static readonly byte[] CanonicalEntropy = Encoding.UTF8.GetBytes("DynamologioDPAPIMasterKeyEntropyV4");
        private static readonly byte[][] CandidateEntropies = new[]
        {
            Encoding.UTF8.GetBytes("DynamologioDPAPIMasterKeyEntropyV4"),
            Encoding.UTF8.GetBytes("DynamologioDPAPIMasterKeyEntropyV5"),
            Encoding.UTF8.GetBytes("DynamologioDPAPIMasterKeyEntropy"),
            null
        };

        private readonly string _keyFilePath;
        private readonly string _dbFilePath;

        public DpapiProtectionProvider() : this(null, null) { }

        public DpapiProtectionProvider(string customKeyPath, string dbFilePath)
        {
            if (!string.IsNullOrWhiteSpace(customKeyPath))
            {
                _keyFilePath = customKeyPath;
            }
            else
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
                _keyFilePath = Path.Combine(configFolder, "master.key");
            }

            if (!string.IsNullOrWhiteSpace(dbFilePath))
            {
                _dbFilePath = dbFilePath;
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
                _dbFilePath = Path.Combine(dataFolder, "dynamologio.db");
            }
        }

        public string GetDatabaseMasterPassword()
        {
            byte[] rawKey = GetOrGenerateRawMasterKey();
            return Convert.ToBase64String(rawKey);
        }

        public byte[] GetMachineBackupKey()
        {
            byte[] masterKey = GetOrGenerateRawMasterKey();
            using (var hmac = new HMACSHA256(masterKey))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes("DynamologioMachineBackupDerivedKeyV5"));
            }
        }

        private byte[] GetOrGenerateRawMasterKey()
        {
            if (File.Exists(_keyFilePath))
            {
                byte[] protectedBytes = File.ReadAllBytes(_keyFilePath);
                byte[] rawKey = null;

                // Try all candidate entropies with LocalMachine scope
                foreach (var entropy in CandidateEntropies)
                {
                    try
                    {
                        rawKey = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.LocalMachine);
                        if (rawKey != null && rawKey.Length == 32)
                        {
                            break;
                        }
                    }
                    catch { }
                }

                // Try all candidate entropies with CurrentUser scope
                if (rawKey == null)
                {
                    foreach (var entropy in CandidateEntropies)
                    {
                        try
                        {
                            rawKey = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
                            if (rawKey != null && rawKey.Length == 32)
                            {
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (rawKey != null)
                {
                    return rawKey;
                }

                // If DB file exists and has data, check if it's plaintext or encrypted
                if (File.Exists(_dbFilePath) && new FileInfo(_dbFilePath).Length > 0)
                {
                    bool isPlaintext = false;
                    try
                    {
                        using (var plainCheck = new LiteDatabase($"Filename={_dbFilePath};Connection=direct"))
                        {
                            var cols = plainCheck.GetCollectionNames();
                            isPlaintext = true;
                        }
                    }
                    catch
                    {
                        isPlaintext = false;
                    }

                    if (!isPlaintext)
                    {
                        // DB is already encrypted with an inaccessible key -> fail closed to protect data
                        throw new CryptographicException("Αποτυχία αποκρυπτογράφησης του κλειδιού master.key μέσω DPAPI. Για λόγους ασφαλείας, η εκκίνηση διεκόπη.");
                    }

                    // DB is plaintext; backup unusable key and generate fresh valid key
                    try
                    {
                        string orphanBak = _keyFilePath + $".unusable_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
                        File.Move(_keyFilePath, orphanBak);
                    }
                    catch { }

                    return GenerateAndSaveNewKey();
                }

                // If no DB exists yet or it is 0 bytes, recover by generating a fresh master key
                return GenerateAndSaveNewKey();
            }
            else
            {
                return GenerateAndSaveNewKey();
            }
        }

        private byte[] GenerateAndSaveNewKey()
        {
            byte[] rawKey = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(rawKey);
            }

            byte[] protectedBytes = ProtectedData.Protect(rawKey, CanonicalEntropy, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(_keyFilePath, protectedBytes);
            return rawKey;
        }
    }
}
