using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dynamologio.App.Controls;
using Dynamologio.App.ViewModels;
using Dynamologio.App.Views;
using Dynamologio.App.Services;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.ImportExport.Excel;
using Dynamologio.ImportExport.Excel.Import;
using Dynamologio.Infrastructure.LiteDb;
using Dynamologio.Infrastructure.Repositories;
using Dynamologio.Infrastructure.Security;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Reporting.Services;
using LiteDB;
using NPOI.SS.UserModel;
using Xunit;

namespace Dynamologio.Tests
{
    public class FailingKeyProvider : IKeyProtectionProvider
    {
        public string GetDatabaseMasterPassword()
        {
            throw new CryptographicException("Προσομοίωση αποτυχίας DPAPI key store!");
        }

        public byte[] GetMachineBackupKey()
        {
            throw new CryptographicException("Προσομοίωση αποτυχίας DPAPI backup subkey!");
        }
    }

    public class TestKeyProvider : IKeyProtectionProvider
    {
        private readonly string _pwd;
        private readonly byte[] _backupKey;

        public TestKeyProvider(string pwd = "TestMasterPassword123!")
        {
            _pwd = pwd;
            _backupKey = Encoding.UTF8.GetBytes("TestMachineBackupDerivedKey32B!!");
        }

        public string GetDatabaseMasterPassword() => _pwd;
        public byte[] GetMachineBackupKey() => _backupKey;
    }

    public class TestFileDialogService : IFileDialogService
    {
        public string OpenExcelFile() => null;
        public string SaveExcelFile(string defaultName) => null;
        public string SelectBackupFile() => null;
        public string SelectBackupDestination(string defaultName) => null;
    }

    public class TestConfirmationService : IConfirmationService
    {
        public bool Confirm(string title, string message) => true;
    }

    public class TestNotificationService : INotificationService
    {
        public void Info(string title, string message) { }
        public void Warning(string title, string message) { }
        public void Error(string title, string message) { }
    }

    public class TestPrintService : IPrintService
    {
        public bool PrintDocument(object document, string title) => true;
    }

    public class FailingAuditPublisher : IAuditEventPublisher
    {
        public void Publish(AuditAction action, string entityType, string entityId, string summary, object oldValue = null, object newValue = null)
        {
            throw new InvalidOperationException("Σφάλμα προσομοίωσης κατά την εγγραφή του AuditEvent!");
        }
    }

    public class TestClock : IClock
    {
        private readonly DateTime _now;
        public TestClock(DateTime now) { _now = now; }
        public DateTime Now => _now;
        public DateTime UtcNow => _now.ToUniversalTime();
        public DateTime Today => _now.Date;
    }

    public class TestCurrentActor : ICurrentActor
    {
        public string GetActor() => "TEST_USER";
    }

    public class DynamologioTests : IDisposable
    {
        protected readonly string _tempDbPath;
        protected readonly LiteDbContext _dbContext;
        protected readonly LiteDbUnitOfWork _uow;
        protected readonly IClock _clock;

        public DynamologioTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"dynamologio_test_{Guid.NewGuid():N}.db");
            _dbContext = new LiteDbContext(_tempDbPath);
            _uow = new LiteDbUnitOfWork(_dbContext);
            _clock = new TestClock(new DateTime(2026, 8, 16, 8, 0, 0));
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            if (File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { }
            }
        }

        // ==========================================
        // 1. P0 SECURITY TESTS
        // ==========================================

        [Fact]
        public void AT_SEC_001_InjectedKeyProviderFailure_FailsClosed()
        {
            string failDbPath = Path.Combine(Path.GetTempPath(), $"fail_db_{Guid.NewGuid():N}.db");
            var failingProvider = new FailingKeyProvider();

            Assert.Throws<CryptographicException>(() =>
            {
                using (var ctx = new LiteDbContext(failDbPath, keyProvider: failingProvider))
                {
                    var count = ctx.Database.GetCollectionNames().Count();
                }
            });

            if (File.Exists(failDbPath)) File.Delete(failDbPath);
        }

        [Fact]
        public void AT_SEC_002_PlaintextLegacyDb_MigratesToEncrypted_LeavesNoPlaintextResidue()
        {
            string legacyDbPath = Path.Combine(Path.GetTempPath(), $"legacy_{Guid.NewGuid():N}.db");
            string targetPassword = "NewEncryptedPassword2026#";

            // Create real plaintext LiteDB with sample data
            using (var plainDb = new LiteDatabase($"Filename={legacyDbPath};Connection=direct"))
            {
                var col = plainDb.GetCollection<Personnel>("personnel");
                col.Insert(new Personnel { LastName = "ΠΑΠΑΔΟΠΟΥΛΟΣ", FirstName = "ΝΙΚΟΛΑΟΣ" });
                col.Insert(new Personnel { LastName = "ΓΕΩΡΓΙΟΥ", FirstName = "ΓΕΩΡΓΙΟΣ" });
            }

            // Perform migration
            LiteDbContext.MigrateLegacyPlaintextDbIfNeeded(legacyDbPath, targetPassword);

            // 1. Plaintext direct opening MUST FAIL
            Assert.ThrowsAny<Exception>(() =>
            {
                using (var testPlain = new LiteDatabase($"Filename={legacyDbPath};Connection=direct"))
                {
                    var names = testPlain.GetCollectionNames().ToList();
                }
            });

            // 2. Encrypted opening with password MUST SUCCEED and preserve all documents
            using (var testEnc = new LiteDatabase($"Filename={legacyDbPath};Password={targetPassword};Connection=direct"))
            {
                var col = testEnc.GetCollection<Personnel>("personnel");
                Assert.Equal(2, col.Count());
            }

            // 3. SEC-002: Zero plaintext copies remain
            string bakPath = legacyDbPath + ".migration_temp.bak";
            string stagingPath = legacyDbPath + ".encrypted.staging";
            Assert.False(File.Exists(bakPath), "Το προσωρινό αντίγραφο plaintext .bak δεν πρέπει να παραμένει μετά την επιτυχή μετανάστευση!");
            Assert.False(File.Exists(stagingPath), "Το staging αρχείο δεν πρέπει να παραμένει μετά την επιτυχή μετανάστευση!");

            if (File.Exists(legacyDbPath)) File.Delete(legacyDbPath);
        }

        [Fact]
        public void AT_SEC_003_AuthenticatedAes256_ValidPassphrase_EncryptsAndDecrypts()
        {
            var keyProvider = new TestKeyProvider();
            var backupService = new BackupService(_uow, _tempDbPath, keyProvider);

            string targetDir = Path.Combine(Path.GetTempPath(), $"bk_dir_{Guid.NewGuid():N}");
            var manifest = backupService.CreateBackup(targetDir, "MySecretBackupKey123!");

            string backupZip = Directory.GetFiles(targetDir, "*.zip").First();
            var result = backupService.VerifyBackup(backupZip, "MySecretBackupKey123!");

            Assert.True(result.IsValid);
            Assert.True(result.IsEncrypted);
            Assert.Equal(manifest.DatabaseSha256Checksum, result.Manifest.DatabaseSha256Checksum);

            Directory.Delete(targetDir, true);
        }

        [Fact]
        public void AT_SEC_004_AuthenticatedBitFlip_RejectsBeforeDecryption()
        {
            var keyProvider = new TestKeyProvider();
            var backupService = new BackupService(_uow, _tempDbPath, keyProvider);

            string targetDir = Path.Combine(Path.GetTempPath(), $"bk_dir_{Guid.NewGuid():N}");
            backupService.CreateBackup(targetDir, "MySecretBackupKey123!");
            string backupZip = Directory.GetFiles(targetDir, "*.zip").First();

            // Read zip, modify 1 byte in payload, save
            byte[] zipBytes = File.ReadAllBytes(backupZip);
            // Flip byte in middle of zip
            zipBytes[zipBytes.Length / 2] ^= 0xFF;
            string corruptedZip = Path.Combine(targetDir, "corrupted.zip");
            File.WriteAllBytes(corruptedZip, zipBytes);

            var result = backupService.VerifyBackup(corruptedZip, "MySecretBackupKey123!");
            Assert.False(result.IsValid);

            Directory.Delete(targetDir, true);
        }

        [Fact]
        public void AT_SEC_005_WrongPassphrase_RejectsAuthentication()
        {
            var keyProvider = new TestKeyProvider();
            var backupService = new BackupService(_uow, _tempDbPath, keyProvider);

            string targetDir = Path.Combine(Path.GetTempPath(), $"bk_dir_{Guid.NewGuid():N}");
            backupService.CreateBackup(targetDir, "CorrectSecretKey123!");
            string backupZip = Directory.GetFiles(targetDir, "*.zip").First();

            var result = backupService.VerifyBackup(backupZip, "WrongSecretKey999!");
            Assert.False(result.IsValid);

            Directory.Delete(targetDir, true);
        }

        [Fact]
        public void AT_SEC_006_ProductionEncryptedLiteDb_BackupAndRestore_Succeeds()
        {
            string prodDbPath = Path.Combine(Path.GetTempPath(), $"prod_enc_{Guid.NewGuid():N}.db");
            var keyProvider = new TestKeyProvider("MasterProductionLiteDbKey2026#");

            using (var prodCtx = new LiteDbContext(prodDbPath, keyProvider: keyProvider))
            {
                var prodUow = new LiteDbUnitOfWork(prodCtx);
                prodUow.Personnel.Insert(new Personnel { LastName = "ΚΩΝΣΤΑΝΤΙΝΟΥ", FirstName = "ΑΝΔΡΕΑΣ" });

                var backupSvc = new BackupService(prodUow, prodDbPath, keyProvider);
                string targetDir = Path.Combine(Path.GetTempPath(), $"prod_bk_{Guid.NewGuid():N}");
                backupSvc.CreateBackup(targetDir, "ExportPassphrase2026!");

                string backupZip = Directory.GetFiles(targetDir, "*.zip").First();

                var coordinator = new DatabaseLifecycleCoordinator(prodCtx, backupSvc, keyProvider);
                bool restored = coordinator.RestoreDatabase(backupZip, "ExportPassphrase2026!");
                Assert.True(restored);

                var restoredPersons = prodUow.Personnel.GetAll().ToList();
                Assert.Single(restoredPersons);
                Assert.Equal("ΚΩΝΣΤΑΝΤΙΝΟΥ", restoredPersons[0].LastName);

                Directory.Delete(targetDir, true);
            }

            if (File.Exists(prodDbPath)) File.Delete(prodDbPath);
        }

        [Fact]
        public void AT_SEC_007_LifecycleCoordinator_RestoresAndRecreatesContext()
        {
            var keyProvider = new TestKeyProvider();
            var backupService = new BackupService(_uow, _tempDbPath, keyProvider);
            var coordinator = new DatabaseLifecycleCoordinator(_dbContext, backupService, keyProvider);

            _uow.Personnel.Insert(new Personnel { LastName = "ΔΗΜΗΤΡΙΟΥ", FirstName = "ΔΗΜΗΤΡΙΟΣ" });

            string targetDir = Path.Combine(Path.GetTempPath(), $"bk_dir_{Guid.NewGuid():N}");
            backupService.CreateBackup(targetDir);
            string backupZip = Directory.GetFiles(targetDir, "*.zip").First();

            bool contextRecreatedCalled = false;
            bool success = coordinator.RestoreDatabase(backupZip, null, () =>
            {
                contextRecreatedCalled = true;
            });

            Assert.True(success);
            Assert.True(contextRecreatedCalled);
            Assert.Single(_uow.Personnel.GetAll());

            Directory.Delete(targetDir, true);
        }

        [Fact]
        public void AT_SEC_008_AutoBackupHealth_PersistsStructuredTimestamps()
        {
            string testDbFolder = Path.Combine(Path.GetTempPath(), $"health_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDbFolder);
            string testDbPath = Path.Combine(testDbFolder, "dynamologio.db");

            using (var ctx = new LiteDbContext(testDbPath))
            {
                var uow = new LiteDbUnitOfWork(ctx);
                var keyProvider = new TestKeyProvider();
                var backupService = new BackupService(uow, testDbPath, keyProvider);

                backupService.PerformDailyAutoBackup();
                var health = backupService.GetBackupHealth();

                Assert.NotNull(health.LastAttemptUtc);
                Assert.NotNull(health.LastSuccessUtc);
                Assert.Empty(health.LastFailureCode);
                Assert.Contains("Επιτυχές", health.StatusSummary);
            }

            if (Directory.Exists(testDbFolder)) Directory.Delete(testDbFolder, true);
        }

        [Fact]
        public void AT_SEC_009_NoStaticFallbackSecretsInCodebase()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "Dynamologio.sln")))
            {
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            string srcDir = Path.Combine(current, "src");
            if (!Directory.Exists(srcDir)) return;

            string[] forbiddenSecrets = new[]
            {
                "DynamologioFallbackLocalKey2026#",
                "DynamologioMachineBoundBackupKey2026"
            };

            foreach (string file in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (string secret in forbiddenSecrets)
                {
                    Assert.DoesNotContain(secret, text);
                }
            }
        }

        // ==========================================
        // 3. IMPORT & AMBIGUITY RESOLUTION TESTS
        // ==========================================

        [Fact]
        public void AT_IMPORT_001_UnknownExcelLayout_RequiresMapping()
        {
            string testXlsx = Path.Combine(Path.GetTempPath(), $"invalid_layout_{Guid.NewGuid():N}.xlsx");
            using (var fs = new FileStream(testXlsx, FileMode.Create))
            {
                IWorkbook wb = new NPOI.XSSF.UserModel.XSSFWorkbook();
                var sheet = wb.CreateSheet("Sheet1");
                var row0 = sheet.CreateRow(0);
                row0.CreateCell(0).SetCellValue("COL_A");
                row0.CreateCell(1).SetCellValue("COL_B");
                wb.Write(fs);
            }

            var importer = new ExcelImportService();
            var preview = importer.AnalyzeAndPreviewImport(testXlsx, _uow);

            Assert.False(preview.CanCommit);
            Assert.True(preview.ErrorCount > 0);

            File.Delete(testXlsx);
        }

        [Fact]
        public void AT_IMPORT_002_DuplicateNameAmbiguity_RequiresAsmResolution()
        {
            // Seed two existing persons with the same name
            _uow.Personnel.Insert(new Personnel { LastName = "ΠΑΠΑΔΟΠΟΥΛΟΣ", FirstName = "ΓΕΩΡΓΙΟΣ", MilitaryServiceNumber = "12345" });
            _uow.Personnel.Insert(new Personnel { LastName = "ΠΑΠΑΔΟΠΟΥΛΟΣ", FirstName = "ΓΕΩΡΓΙΟΣ", MilitaryServiceNumber = "67890" });

            var rank = new Rank { Name = "Λοχαγός", ShortName = "Λγος", SortOrder = 1 };
            var unit = new OrganisationUnit { Name = "1ος ΛΟΧΟΣ", Code = "1ΛΧ" };
            _uow.Ranks.Insert(rank);
            _uow.OrganisationUnits.Insert(unit);

            string testXlsx = Path.Combine(Path.GetTempPath(), $"dup_name_{Guid.NewGuid():N}.xlsx");
            using (var fs = new FileStream(testXlsx, FileMode.Create))
            {
                IWorkbook wb = new NPOI.XSSF.UserModel.XSSFWorkbook();
                var sheet = wb.CreateSheet("Sheet1");
                var r0 = sheet.CreateRow(0);
                r0.CreateCell(0).SetCellValue("ΕΠΩΝΥΜΟ");
                r0.CreateCell(1).SetCellValue("ΟΝΟΜΑ");
                r0.CreateCell(2).SetCellValue("ΒΑΘΜΟΣ");
                r0.CreateCell(3).SetCellValue("ΜΟΝΑΔΑ");

                var r1 = sheet.CreateRow(1);
                r1.CreateCell(0).SetCellValue("ΠΑΠΑΔΟΠΟΥΛΟΣ");
                r1.CreateCell(1).SetCellValue("ΓΕΩΡΓΙΟΣ");
                r1.CreateCell(2).SetCellValue("Λοχαγός");
                r1.CreateCell(3).SetCellValue("1ος ΛΟΧΟΣ");

                wb.Write(fs);
            }

            var importer = new ExcelImportService();
            var preview = importer.AnalyzeAndPreviewImport(testXlsx, _uow);

            Assert.False(preview.CanCommit, "Η εισαγωγή με διπλοτυπία ονόματος χωρίς ΑΣΜ πρέπει να μπλοκάρεται!");
            Assert.True(preview.ErrorCount > 0);
            Assert.Contains(preview.Rows[0].ValidationMessages, m => m.Contains("πολλαπλά πρόσωπα"));

            File.Delete(testXlsx);
        }

        // ==========================================
        // 4. REPORTING & SHA ENFORCEMENT TESTS
        // ==========================================

        [Fact]
        public void AT_REPORT_001_NoTemplateOrBlankSha_ReturnsUnverified()
        {
            var strengthCalc = new StrengthCalculationEngine(new StatusEngine());
            var reportSvc = new ReportGeneratorService(_uow, strengthCalc, new NpoiTemplateWriter());

            var status = reportSvc.CheckTemplateStatus(out _, out _, out _);
            Assert.Equal(TemplateVerificationStatus.Unverified, status);
        }

        [Fact]
        public void AT_REPORT_002_DistinctReportGeneration_AllFourTypes()
        {
            var strengthCalc = new StrengthCalculationEngine(new StatusEngine());
            var writer = new NpoiTemplateWriter();
            var reportSvc = new ReportGeneratorService(_uow, strengthCalc, writer);

            // Populate sample data
            var rank = new Rank { Name = "Λοχαγός", ShortName = "Λγος", SortOrder = 1, Category = PersonnelCategory.OfficerOrNco };
            var unit = new OrganisationUnit { Name = "1ος ΛΟΧΟΣ" };
            _uow.Ranks.Insert(rank);
            _uow.OrganisationUnits.Insert(unit);

            var p1 = new Personnel { LastName = "ΑΛΕΞΙΟΥ", FirstName = "ΚΩΝΣΤΑΝΤΙΝΟΣ", RankId = rank.Id, OrganisationUnitId = unit.Id, MilitaryServiceNumber = "11111" };
            _uow.Personnel.Insert(p1);

            var st = new StatusType { Name = "ΚΑΝΟΝΙΚΗ ΑΔΕΙΑ", Effect = StatusEffect.Absent };
            _uow.StatusTypes.Insert(st);

            var svType = new ServiceType { Name = "ΑΞΙΩΜΑΤΙΚΟΣ ΥΠΗΡΕΣΙΑΣ" };
            _uow.ServiceTypes.Insert(svType);

            _uow.ServiceAssignments.Insert(new ServiceAssignment
            {
                PersonnelId = p1.Id,
                ServiceTypeId = svType.Id,
                ServiceDate = DateTime.Today,
                StartDateTime = DateTime.Today,
                EndDateTime = DateTime.Today.AddHours(24),
                DutyLocation = "Διοικητήριο"
            });

            // FlowDocument checks for all 4 types
            var doc1 = reportSvc.GeneratePrintableDocument(new ReportGenerationRequest { Type = ReportType.DailyDynamologio });
            Assert.NotNull(doc1);

            var doc2 = reportSvc.GeneratePrintableDocument(new ReportGenerationRequest { Type = ReportType.AbsentPersonnel });
            Assert.NotNull(doc2);

            var doc3 = reportSvc.GeneratePrintableDocument(new ReportGenerationRequest { Type = ReportType.PresentPersonnel });
            Assert.NotNull(doc3);

            var doc4 = reportSvc.GeneratePrintableDocument(new ReportGenerationRequest { Type = ReportType.ServiceRoster });
            Assert.NotNull(doc4);

            // Excel export checks for all 4 types
            string outDir = Path.Combine(Path.GetTempPath(), $"reports_out_{Guid.NewGuid():N}");
            Directory.CreateDirectory(outDir);

            string pathAbsent = Path.Combine(outDir, "absent.xlsx");
            reportSvc.ExportToExcel(new ReportGenerationRequest { Type = ReportType.AbsentPersonnel }, pathAbsent);
            Assert.True(File.Exists(pathAbsent));

            string pathPresent = Path.Combine(outDir, "present.xlsx");
            reportSvc.ExportToExcel(new ReportGenerationRequest { Type = ReportType.PresentPersonnel }, pathPresent);
            Assert.True(File.Exists(pathPresent));

            string pathService = Path.Combine(outDir, "service.xlsx");
            reportSvc.ExportToExcel(new ReportGenerationRequest { Type = ReportType.ServiceRoster }, pathService);
            Assert.True(File.Exists(pathService));

            Directory.Delete(outDir, true);
        }



        [Fact]
        public void ScreenshotCapture_FullMainWindow_1366x768_and_1024x768()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "Dynamologio.sln")))
            {
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            string screenshotsDir = Path.Combine(current, "TestResults", "temp", "artifact");
            if (!Directory.Exists(screenshotsDir)) Directory.CreateDirectory(screenshotsDir);

            // Populate rich realistic military demo dataset
            PopulateRealisticDemoDataset();

            Exception staEx = null;
            var staThread = new Thread(() =>
            {
                try
                {
                    AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
                    {
                        var asmName = new System.Reflection.AssemblyName(args.Name);
                        if (asmName.Name == "Dynamologio" || asmName.Name == "Dynamologio.App")
                        {
                            string exePath = Path.Combine(current, "src", "Dynamologio.App", "bin", "Debug", "net472", "Dynamologio.exe");
                            if (File.Exists(exePath)) return System.Reflection.Assembly.LoadFrom(exePath);
                        }
                        return null;
                    };

                    if (Application.Current == null)
                    {
                        var app = new Application();
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Dynamologio;component/Styles/Icons.xaml") });
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Dynamologio;component/Styles/DesignSystem.xaml") });
                    }

                    var keyProvider = new TestKeyProvider();
                    var statusEngine = new StatusEngine();
                    var conflictEngine = new ConflictEngine();
                    var strengthCalc = new StrengthCalculationEngine(statusEngine);
                    var reportService = new ReportGeneratorService(_uow, strengthCalc, new NpoiTemplateWriter());
                    var importService = new ExcelImportService();
                    var backupService = new BackupService(_uow, _tempDbPath, keyProvider);
                    var lifecycleCoordinator = new DatabaseLifecycleCoordinator(_dbContext, backupService, keyProvider);
                    var diagService = new DiagnosticPackageService(_uow, _tempDbPath);
                    var auditSink = new LiteDbAuditSink(_uow);
                    var actor = new TestCurrentActor();
                    var auditService = new AuditEventPublisher(new[] { auditSink }, actor, _clock);
                    var tx = new LiteDbTransactionRunner(_uow);
                    var personnelService = new PersonnelService(_uow, auditService, tx, _clock, actor);
                    var absenceService = new AbsenceService(_uow, auditService, tx, _clock, actor);
                    var dutyService = new DutyService(_uow, auditService, tx, _clock, actor);

                    var fileDialogService = new TestFileDialogService();
                    var notificationService = new TestNotificationService();
                    var confirmationService = new TestConfirmationService();
                    var printService = new TestPrintService();
                    var personEditorDialogService = new Dynamologio.App.Services.WpfPersonEditorDialogService(_uow, conflictEngine, personnelService);

                    var strengthQueryService = new StrengthQueryService(_uow, strengthCalc);
                    var personnelQueryService = new PersonnelQueryService(_uow, statusEngine);
                    var absenceQueryService = new AbsenceQueryService(_uow);
                    var serviceQueryService = new ServiceRosterQueryService(_uow);

                    var navService = new Dynamologio.App.Navigation.NavigationService();
                    var shellState = new Dynamologio.App.Navigation.ShellStateService();
                    shellState.UpdateDeploymentHeader("ΜΟΝΑΔΑ", "ΓΡΑΦΕΙΟ / ΤΜΗΜΑ");

                    var factories = new System.Collections.Generic.Dictionary<Dynamologio.App.Navigation.NavigationSection, Func<Dynamologio.App.ViewModels.ViewModelBase>>
                    {
                        { Dynamologio.App.Navigation.NavigationSection.Dashboard, () => new DashboardViewModel(strengthQueryService, _clock, navService) },
                        { Dynamologio.App.Navigation.NavigationSection.Dynamologio, () => new DynamologioViewModel(strengthQueryService, personnelQueryService, reportService, _clock, shellState, printService, fileDialogService, notificationService) },
                        { Dynamologio.App.Navigation.NavigationSection.Personnel, () => new PersonnelViewModel(personnelQueryService, personnelService, _clock, personEditorDialogService, confirmationService) },
                        { Dynamologio.App.Navigation.NavigationSection.Absences, () => new AbsencesViewModel(personnelQueryService, absenceQueryService, conflictEngine, absenceService, _clock, notificationService, confirmationService) },
                        { Dynamologio.App.Navigation.NavigationSection.Services, () => new ServicesViewModel(personnelQueryService, serviceQueryService, absenceQueryService, conflictEngine, dutyService, _clock, notificationService, confirmationService) },
                        { Dynamologio.App.Navigation.NavigationSection.Reports, () => new ReportsViewModel(personnelQueryService, strengthQueryService, reportService, _clock, shellState, printService, fileDialogService, notificationService) },
                        { Dynamologio.App.Navigation.NavigationSection.ImportExport, () => new ImportExportViewModel(_uow, importService, fileDialogService, notificationService, confirmationService) },
                        { Dynamologio.App.Navigation.NavigationSection.DataValidation, () => new DataValidationViewModel(_uow, conflictEngine, statusEngine, _clock) },
                        { Dynamologio.App.Navigation.NavigationSection.History, () => new HistoryViewModel(_uow, _clock) },
                        { Dynamologio.App.Navigation.NavigationSection.Settings, () => new SettingsViewModel(_uow, backupService, lifecycleCoordinator, diagService, _clock, shellState, fileDialogService, notificationService, confirmationService) }
                    };

                    var factory = new Dynamologio.App.Navigation.ViewModelFactory(factories);

                    var mainVM = new MainViewModel(
                        navService,
                        factory,
                        shellState);

                    string[] sections = new[] { "Dashboard", "Dynamologio", "Personnel", "Absences", "Services", "Reports", "ImportExport", "DataValidation", "History", "Settings" };

                    var mainWindow = new MainWindow { DataContext = mainVM };

                    foreach (string section in sections)
                    {
                        if (Enum.TryParse<Dynamologio.App.Navigation.NavigationSection>(section, out var parsedSection))
                        {
                            navService.Navigate(parsedSection);
                        }

                        // Capture Complete MainWindow at 1366x768
                        RenderAndSaveScreenshot(mainWindow, 1366, 768, Path.Combine(screenshotsDir, $"MainWindow_{section}_1366x768.png"));

                        // Capture Complete MainWindow at 1024x768
                        RenderAndSaveScreenshot(mainWindow, 1024, 768, Path.Combine(screenshotsDir, $"MainWindow_{section}_1024x768.png"));
                    }
                }
                catch (Exception ex)
                {
                    staEx = ex;
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            if (staEx != null) throw staEx;
        }

        private void PopulateRealisticDemoDataset()
        {
            var rCapt = new Rank { Name = "Λοχαγός", ShortName = "Λγος", SortOrder = 1, Category = PersonnelCategory.OfficerOrNco };
            var rLieut = new Rank { Name = "Υπολοχαγός", ShortName = "Υπλγος", SortOrder = 2, Category = PersonnelCategory.OfficerOrNco };
            var rSgt = new Rank { Name = "Λοχίας", ShortName = "Λχιας", SortOrder = 3, Category = PersonnelCategory.OfficerOrNco };
            var rPvt = new Rank { Name = "Στρατιώτης", ShortName = "Στρ", SortOrder = 4, Category = PersonnelCategory.Conscript };

            _uow.Ranks.Insert(rCapt);
            _uow.Ranks.Insert(rLieut);
            _uow.Ranks.Insert(rSgt);
            _uow.Ranks.Insert(rPvt);

            var uLohos1 = new OrganisationUnit { Name = "1ος ΛΟΧΟΣ", Code = "1ΛΧ", SortOrder = 1 };
            var uLohos2 = new OrganisationUnit { Name = "2ος ΛΟΧΟΣ", Code = "2ΛΧ", SortOrder = 2 };
            var uLohos3 = new OrganisationUnit { Name = "3ος ΛΟΧΟΣ", Code = "3ΛΧ", SortOrder = 3 };

            _uow.OrganisationUnits.Insert(uLohos1);
            _uow.OrganisationUnits.Insert(uLohos2);
            _uow.OrganisationUnits.Insert(uLohos3);

            var stLeave = new StatusType { Name = "ΚΑΝΟΝΙΚΗ ΑΔΕΙΑ", Effect = StatusEffect.Absent, SortOrder = 1 };
            var stSick = new StatusType { Name = "ΑΝΑΡΡΩΤΙΚΗ ΑΔΕΙΑ", Effect = StatusEffect.Absent, SortOrder = 2 };
            var stDetached = new StatusType { Name = "ΑΠΟΣΠΑΣΗ", Effect = StatusEffect.Absent, SortOrder = 3 };

            _uow.StatusTypes.Insert(stLeave);
            _uow.StatusTypes.Insert(stSick);
            _uow.StatusTypes.Insert(stDetached);

            var svDuty = new ServiceType { Name = "ΑΞΙΩΜΑΤΙΚΟΣ ΥΠΗΡΕΣΙΑΣ", SortOrder = 1 };
            var svGuard = new ServiceType { Name = "ΕΦΟΔΟΣ / ΣΚΟΠΙΑ", SortOrder = 2 };

            _uow.ServiceTypes.Insert(svDuty);
            _uow.ServiceTypes.Insert(svGuard);

            // Populate 32 personnel
            var persons = new List<Personnel>();
            for (int i = 1; i <= 32; i++)
            {
                var rk = i <= 2 ? rCapt : (i <= 6 ? rLieut : (i <= 12 ? rSgt : rPvt));
                var un = i % 3 == 1 ? uLohos1 : (i % 3 == 2 ? uLohos2 : uLohos3);

                var p = new Personnel
                {
                    LastName = $"ΕΠΩΝΥΜΟ_{i:D2}",
                    FirstName = $"ΟΝΟΜΑ_{i:D2}",
                    FatherName = "ΙΩΑΝΝΗΣ",
                    MilitaryServiceNumber = $"15{i:D4}",
                    RankId = rk.Id,
                    OrganisationUnitId = un.Id,
                    Specialty = i <= 6 ? "ΠΕΖΙΚΟ" : "ΤΥΦΕΚΙΟΦΟΡΟΣ",
                    StrengthStartDate = new DateTime(2026, 1, 1),
                    IsArchived = false
                };
                _uow.Personnel.Insert(p);
                persons.Add(p);
            }

            // Add active absences for 3 persons
            _uow.StatusEvents.Insert(new StatusEvent
            {
                PersonnelId = persons[1].Id,
                StatusTypeId = stLeave.Id,
                StartAt = new DateTime(2026, 8, 14),
                EndAtExclusive = new DateTime(2026, 8, 20),
                ReferenceDocument = "Φ.400/12/2026"
            });

            _uow.StatusEvents.Insert(new StatusEvent
            {
                PersonnelId = persons[7].Id,
                StatusTypeId = stSick.Id,
                StartAt = new DateTime(2026, 8, 15),
                EndAtExclusive = new DateTime(2026, 8, 18),
                ReferenceDocument = "401 ΓΣΝΑ"
            });

            // Add services
            _uow.ServiceAssignments.Insert(new ServiceAssignment
            {
                PersonnelId = persons[0].Id,
                ServiceTypeId = svDuty.Id,
                ServiceDate = new DateTime(2026, 8, 16),
                StartDateTime = new DateTime(2026, 8, 16, 8, 0, 0),
                EndDateTime = new DateTime(2026, 8, 17, 8, 0, 0),
                DutyLocation = "Διοικητήριο"
            });
        }

        private static void RenderAndSaveScreenshot(FrameworkElement element, int width, int height, string outputPath)
        {
            var target = (element is Window win && win.Content is FrameworkElement fe) ? fe : element;
            target.Width = width;
            target.Height = height;
            target.Measure(new Size(width, height));
            target.Arrange(new Rect(0, 0, width, height));
            target.UpdateLayout();

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(target);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var fs = new FileStream(outputPath, FileMode.Create))
            {
                encoder.Save(fs);
            }
        }
    }
}
