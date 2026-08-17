using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dynamologio.App.ViewModels;
using Dynamologio.App.Views;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Dynamologio.ImportExport.Excel;
using Dynamologio.ImportExport.Excel.Import;
using Dynamologio.Infrastructure.LiteDb;
using Dynamologio.Infrastructure.Migrations;
using Dynamologio.Infrastructure.Repositories;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Reporting.Services;
using LiteDB;
using NPOI.SS.UserModel;
using Xunit;

namespace Dynamologio.Tests
{
    public class DynamologioTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly LiteDbContext _dbContext;
        private readonly LiteDbUnitOfWork _uow;
        private readonly FixedClock _clock;

        public DynamologioTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"dynamologio_test_{Guid.NewGuid():N}.db");
            _dbContext = new LiteDbContext(_tempDbPath);
            _uow = new LiteDbUnitOfWork(_dbContext);
            _clock = new FixedClock(new DateTime(2026, 8, 16, 10, 0, 0));

            var migrationRunner = new SchemaMigrationRunner(_uow);
            migrationRunner.RunMigrations();
        }

        public void Dispose()
        {
            _uow?.Dispose();
            _dbContext?.Dispose();
            if (File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { }
            }
        }

        // ==========================================
        // 1. LIFECYCLE & INTERVAL MATH TESTS
        // ==========================================

        [Fact]
        public void AT_LIFECYCLE_001_ActivePersonnelWithinDates_ShouldBeIncludedInStrength()
        {
            var engine = new StatusEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΠΑΠΑΔΟΠΟΥΛΟΣ",
                FirstName = "ΓΕΩΡΓΙΟΣ",
                StrengthStartDate = new DateTime(2026, 1, 1)
            };

            var snapshot = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 5, 10));
            Assert.True(snapshot.IsInActiveStrength);
            Assert.Equal(StatusEffect.Present, snapshot.EffectiveStatus);
        }

        [Fact]
        public void AT_LIFECYCLE_002_BeforeStrengthStartDate_ShouldBeExcluded()
        {
            var engine = new StatusEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΜΕΛΛΟΝΤΙΚΟΣ",
                FirstName = "ΑΝΔΡΕΑΣ",
                StrengthStartDate = new DateTime(2026, 9, 1)
            };

            var snapshot = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 8, 16));
            Assert.False(snapshot.IsInActiveStrength);
            Assert.Equal(StatusEffect.ExcludedFromStrength, snapshot.EffectiveStatus);
        }

        [Fact]
        public void AT_LIFECYCLE_003_OnOrAfterStrengthEndDate_ShouldBeExcluded()
        {
            var engine = new StatusEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΑΠΟΛΥΘΕΙΣ",
                FirstName = "ΔΗΜΗΤΡΙΟΣ",
                StrengthStartDate = new DateTime(2025, 1, 1),
                StrengthEndDate = new DateTime(2026, 8, 1)
            };

            var snapshot = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 8, 16));
            Assert.False(snapshot.IsInActiveStrength);
            Assert.Equal(StatusEffect.ExcludedFromStrength, snapshot.EffectiveStatus);
        }

        [Fact]
        public void AT_LIFECYCLE_004_HistoricalQuery_PreservesPastArchivedStatus()
        {
            var engine = new StatusEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΠΑΛΑΙΟΣ",
                FirstName = "ΝΙΚΟΛΑΟΣ",
                IsArchived = true,
                StrengthStartDate = new DateTime(2025, 1, 1),
                StrengthEndDate = new DateTime(2026, 6, 30)
            };

            var historicalSnapshot = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 3, 15));
            Assert.True(historicalSnapshot.IsInActiveStrength);
            Assert.Equal(StatusEffect.Present, historicalSnapshot.EffectiveStatus);
        }

        [Fact]
        public void AT_ABS_001_StatusIntervalMath_IsActiveAt_HalfOpen()
        {
            StatusIntervalMath.CreateDayInterval(new DateTime(2026, 8, 10), new DateTime(2026, 8, 15), out var startAt, out var endExclusive);

            Assert.True(StatusIntervalMath.IsActiveAt(startAt, endExclusive, new DateTime(2026, 8, 10, 8, 0, 0)));
            Assert.True(StatusIntervalMath.IsActiveAt(startAt, endExclusive, new DateTime(2026, 8, 14, 23, 59, 59)));
            Assert.False(StatusIntervalMath.IsActiveAt(startAt, endExclusive, new DateTime(2026, 8, 15, 0, 0, 0)));
        }

        [Fact]
        public void AT_ABS_002_AutomaticReturnUponExpiration_AtMidnight()
        {
            var engine = new StatusEngine();
            var pId = Guid.NewGuid();
            var person = new Personnel { Id = pId, StrengthStartDate = new DateTime(2026, 1, 1) };
            var stId = Guid.NewGuid();
            var statusType = new StatusType { Id = stId, Name = "ΚΑΝΟΝΙΚΗ ΑΔΕΙΑ", Effect = StatusEffect.Absent };

            StatusIntervalMath.CreateDayInterval(new DateTime(2026, 8, 10), new DateTime(2026, 8, 15), out var startAt, out var endExclusive);
            var ev = new StatusEvent { Id = Guid.NewGuid(), PersonnelId = pId, StatusTypeId = stId, StartAt = startAt, EndAtExclusive = endExclusive };

            var snapshotDuring = engine.CalculatePersonStatus(person, new[] { ev }, new[] { statusType }, null, null, null, null, new DateTime(2026, 8, 14, 23, 59, 0));
            Assert.Equal(StatusEffect.Absent, snapshotDuring.EffectiveStatus);

            var snapshotAfter = engine.CalculatePersonStatus(person, new[] { ev }, new[] { statusType }, null, null, null, null, new DateTime(2026, 8, 15, 0, 0, 0));
            Assert.Equal(StatusEffect.Present, snapshotAfter.EffectiveStatus);
        }

        // ==========================================
        // 2. CONFLICT MATRIX & VALIDATION TESTS
        // ==========================================

        [Fact]
        public void AT_CONFLICT_001_InvalidDateRange_Rejected()
        {
            var engine = new ConflictEngine();
            var person = new Personnel { Id = Guid.NewGuid(), StrengthStartDate = new DateTime(2026, 1, 1) };
            var ev = new StatusEvent
            {
                PersonnelId = person.Id,
                StartAt = new DateTime(2026, 8, 15),
                EndAtExclusive = new DateTime(2026, 8, 10)
            };

            var results = engine.ValidateStatusEvent(ev, person, null, null);
            Assert.Contains(results, r => r.Severity == ConflictSeverity.Error);
        }

        [Fact]
        public void AT_CONFLICT_002_DuplicateAsm_Rejected()
        {
            var engine = new ConflictEngine();
            var p1 = new Personnel { Id = Guid.NewGuid(), MilitaryServiceNumber = "123/45678/20" };
            var p2 = new Personnel { Id = Guid.NewGuid(), MilitaryServiceNumber = " 123/45678/20 " };

            var results = engine.ValidatePersonnel(p2, new[] { p1 });
            Assert.Contains(results, r => r.Severity == ConflictSeverity.Error);
        }

        [Fact]
        public void AT_CONFLICT_003_ServiceOutsideStrength_Rejected()
        {
            var engine = new ConflictEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                StrengthStartDate = new DateTime(2026, 8, 1),
                StrengthEndDate = new DateTime(2026, 8, 10)
            };

            var assignment = new ServiceAssignment
            {
                PersonnelId = person.Id,
                ServiceDate = new DateTime(2026, 8, 15),
                StartDateTime = new DateTime(2026, 8, 15, 8, 0, 0),
                EndDateTime = new DateTime(2026, 8, 15, 14, 0, 0)
            };

            var results = engine.ValidateServiceAssignment(assignment, person, null, null);
            Assert.Contains(results, r => r.Severity == ConflictSeverity.Error);
        }

        [Fact]
        public void AT_CONFLICT_004_OverlappingServices_Rejected()
        {
            var engine = new ConflictEngine();
            var person = new Personnel { Id = Guid.NewGuid(), StrengthStartDate = new DateTime(2026, 1, 1) };
            var s1 = new ServiceAssignment
            {
                PersonnelId = person.Id,
                ServiceDate = new DateTime(2026, 8, 16),
                StartDateTime = new DateTime(2026, 8, 16, 8, 0, 0),
                EndDateTime = new DateTime(2026, 8, 16, 14, 0, 0)
            };
            var s2 = new ServiceAssignment
            {
                PersonnelId = person.Id,
                ServiceDate = new DateTime(2026, 8, 16),
                StartDateTime = new DateTime(2026, 8, 16, 12, 0, 0),
                EndDateTime = new DateTime(2026, 8, 16, 18, 0, 0)
            };

            var results = engine.ValidateServiceAssignment(s2, person, new[] { s1 }, null);
            Assert.Contains(results, r => r.Severity == ConflictSeverity.Error);
        }

        // ==========================================
        // 3. P0 SECURITY & CRYPTOGRAPHY TESTS
        // ==========================================

        [Fact]
        public void AT_SEC_001_NoStaticFallbackSecret_KeyFailureFailsClosed()
        {
            string isolatedDb = Path.Combine(Path.GetTempPath(), $"isolated_{Guid.NewGuid():N}.db");
            try
            {
                // Creating context with explicit password succeeds
                using (var ctx = new LiteDbContext(isolatedDb, "ValidTestPassphrase123#"))
                {
                    var col = ctx.GetCollection<Personnel>("personnel");
                    col.Insert(new Personnel { LastName = "TEST", FirstName = "PASS" });
                }

                // Attempting to reopen with incorrect password must throw Cryptographic/LiteException fail closed
                Assert.ThrowsAny<Exception>(() =>
                {
                    using (var badCtx = new LiteDbContext(isolatedDb, "WrongPassword!!!"))
                    {
                        var col = badCtx.GetCollection<Personnel>("personnel");
                        col.FindAll().ToList();
                    }
                });
            }
            finally
            {
                if (File.Exists(isolatedDb)) File.Delete(isolatedDb);
            }
        }

        [Fact]
        public void AT_SEC_002_PlaintextLegacyDb_MigratesToEncrypted()
        {
            string legacyDb = Path.Combine(Path.GetTempPath(), $"legacy_{Guid.NewGuid():N}.db");
            try
            {
                // 1. Create legacy unencrypted database
                using (var plain = new LiteDatabase($"Filename={legacyDb};Connection=direct"))
                {
                    var col = plain.GetCollection<Personnel>("personnel");
                    col.Insert(new Personnel { LastName = "LEGACY_PERSON", FirstName = "IOANNIS" });
                }

                // 2. Run migration to encrypted database
                string encPassword = "TargetSecurePassword2026#";
                LiteDbContext.MigrateLegacyPlaintextDbIfNeeded(legacyDb, encPassword);

                // 3. Verify it cannot be opened without password anymore
                Assert.ThrowsAny<Exception>(() =>
                {
                    using (var testPlain = new LiteDatabase($"Filename={legacyDb};Connection=direct"))
                    {
                        testPlain.GetCollection<Personnel>("personnel").FindAll().ToList();
                    }
                });

                // 4. Verify it opens cleanly with password and preserves legacy records
                using (var testEnc = new LiteDatabase($"Filename={legacyDb};Password={encPassword};Connection=direct"))
                {
                    var list = testEnc.GetCollection<Personnel>("personnel").FindAll().ToList();
                    Assert.Single(list);
                    Assert.Equal("LEGACY_PERSON", list[0].LastName);
                }
            }
            finally
            {
                if (File.Exists(legacyDb)) File.Delete(legacyDb);
                if (File.Exists(legacyDb + ".plaintext.bak")) File.Delete(legacyDb + ".plaintext.bak");
            }
        }

        [Fact]
        public void AT_SEC_003_AuthenticatedAes256_ValidPassphrase_EncryptsAndDecrypts()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("CONFIDENTIAL MILITARY STRENGTH DATA 2026");
            string passphrase = "StrongUnitPassphrase2026#";

            byte[] authenticatedPayload = BackupService.EncryptAndAuthenticateAes256(plaintext, passphrase);
            Assert.NotNull(authenticatedPayload);
            Assert.True(authenticatedPayload.Length > 72);

            byte[] decrypted = BackupService.VerifyAndDecryptAes256(authenticatedPayload, passphrase);
            Assert.Equal(Encoding.UTF8.GetString(plaintext), Encoding.UTF8.GetString(decrypted));
        }

        [Fact]
        public void AT_SEC_004_AuthenticatedBitFlip_RejectsBeforeDecryption()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("SENSITIVE STRENGTH RECORDS");
            string passphrase = "SecurityPassword123#";

            byte[] authenticatedPayload = BackupService.EncryptAndAuthenticateAes256(plaintext, passphrase);

            // Flip 1 bit in ciphertext (payload index 80)
            authenticatedPayload[80] ^= 0x01;

            // Must throw CryptographicException due to HMAC mismatch
            var ex = Assert.Throws<CryptographicException>(() =>
            {
                BackupService.VerifyAndDecryptAes256(authenticatedPayload, passphrase);
            });
            Assert.Contains("HMAC", ex.Message);
        }

        [Fact]
        public void AT_SEC_005_WrongPassphrase_RejectsAuthentication()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("TEST DATA");
            byte[] authenticatedPayload = BackupService.EncryptAndAuthenticateAes256(plaintext, "CorrectPassword");

            Assert.Throws<CryptographicException>(() =>
            {
                BackupService.VerifyAndDecryptAes256(authenticatedPayload, "WrongPassword");
            });
        }

        [Fact]
        public void AT_SEC_006_AuditFailureRollback_ForPersonnel()
        {
            // Unit of Work starts with 0 personnel
            int initialCount = _uow.Personnel.Count();

            var failingAudit = new FailingAuditService();
            var personnelService = new PersonnelService(_uow, failingAudit);

            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΑΠΟΤΥΧΙΑ_AUDIT",
                FirstName = "ΓΕΩΡΓΙΟΣ"
            };

            // When audit throws, mutation must rollback
            Assert.Throws<InvalidOperationException>(() =>
            {
                personnelService.CreatePerson(person, "Test reason");
            });

            // Person must NOT exist in repository
            Assert.Equal(initialCount, _uow.Personnel.Count());
        }

        [Fact]
        public void AT_SEC_007_AuditFailureRollback_ForAbsence()
        {
            int initialCount = _uow.StatusEvents.Count();

            var failingAudit = new FailingAuditService();
            var absenceService = new AbsenceService(_uow, failingAudit);

            var ev = new StatusEvent
            {
                Id = Guid.NewGuid(),
                PersonnelId = Guid.NewGuid(),
                StatusTypeId = Guid.NewGuid(),
                StartAt = DateTime.Today,
                EndAtExclusive = DateTime.Today.AddDays(2)
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                absenceService.CreateAbsence(ev, "Test reason");
            });

            Assert.Equal(initialCount, _uow.StatusEvents.Count());
        }

        [Fact]
        public void AT_SEC_008_AuditFailureRollback_ForService()
        {
            int initialCount = _uow.ServiceAssignments.Count();

            var failingAudit = new FailingAuditService();
            var dutyService = new DutyService(_uow, failingAudit);

            var assignment = new ServiceAssignment
            {
                Id = Guid.NewGuid(),
                PersonnelId = Guid.NewGuid(),
                ServiceTypeId = Guid.NewGuid(),
                ServiceDate = DateTime.Today,
                StartDateTime = DateTime.Today.AddHours(8),
                EndDateTime = DateTime.Today.AddHours(14)
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                dutyService.AssignDuty(assignment, "Test duty");
            });

            Assert.Equal(initialCount, _uow.ServiceAssignments.Count());
        }

        // ==========================================
        // 4. REPORTING & IMPORT VERIFICATION TESTS
        // ==========================================

        [Fact]
        public void AT_REPORT_001_MissingTemplate_BlocksExport()
        {
            var strengthCalc = new StrengthCalculationEngine(new StatusEngine());
            var reportService = new ReportGeneratorService(_uow, strengthCalc, new NpoiTemplateWriter());

            var req = new ReportGenerationRequest
            {
                Type = ReportType.DailyDynamologio,
                AsOfTimestamp = DateTime.Today,
                CustomTemplatePath = "C:\\NonExistentPath\\FakeTemplate.xlsx"
            };

            // When template is missing, export must fail with clear exception
            Assert.Throws<InvalidOperationException>(() =>
            {
                reportService.ExportToExcel(req, Path.Combine(Path.GetTempPath(), "test_out.xlsx"));
            });
        }

        [Fact]
        public void AT_REPORT_002_PrintableDocument_ProducesFlowDocument()
        {
            var strengthCalc = new StrengthCalculationEngine(new StatusEngine());
            var reportService = new ReportGeneratorService(_uow, strengthCalc, new NpoiTemplateWriter());

            var doc = reportService.GeneratePrintableDocument(new ReportGenerationRequest
            {
                Type = ReportType.AbsentPersonnel,
                AsOfTimestamp = DateTime.Today,
                UnitTitle = "ΔΟΚΙΜΗ ΜΟΝΑΔΑΣ"
            });

            Assert.NotNull(doc);
            Assert.NotEmpty(doc.Blocks);
        }

        [Fact]
        public void AT_IMPORT_001_UnknownExcelLayout_RequiresMapping()
        {
            // Create dummy excel with invalid columns
            string tempExcel = Path.Combine(Path.GetTempPath(), $"invalid_headers_{Guid.NewGuid():N}.xlsx");
            try
            {
                using (var fs = new FileStream(tempExcel, FileMode.Create))
                {
                    IWorkbook wb = new NPOI.XSSF.UserModel.XSSFWorkbook();
                    var sheet = wb.CreateSheet("Sheet1");
                    var row0 = sheet.CreateRow(0);
                    row0.CreateCell(0).SetCellValue("COL_A");
                    row0.CreateCell(1).SetCellValue("COL_B");
                    row0.CreateCell(2).SetCellValue("COL_C");

                    var row1 = sheet.CreateRow(1);
                    row1.CreateCell(0).SetCellValue("Val1");
                    row1.CreateCell(1).SetCellValue("Val2");
                    row1.CreateCell(2).SetCellValue("Val3");

                    wb.Write(fs);
                }

                var importService = new ExcelImportService();
                var report = importService.AnalyzeAndPreviewImport(tempExcel, _uow);

                // Must detect error and refuse commit
                Assert.False(report.CanCommit);
                Assert.True(report.ErrorCount > 0);
            }
            finally
            {
                if (File.Exists(tempExcel)) File.Delete(tempExcel);
            }
        }

        // ==========================================
        // 5. REPOSITORY & UI VERIFICATION TESTS
        // ==========================================

        [Fact]
        public void AT_REPO_001_ZeroEmojiInProductionSources()
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

            var emojiPattern = new Regex(@"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u26FF\u2700-\u27BF]", RegexOptions.Compiled);
            var violations = new List<string>();

            foreach (var file in Directory.EnumerateFiles(srcDir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".cs" || ext == ".xaml")
                {
                    string content = File.ReadAllText(file);
                    var matches = emojiPattern.Matches(content);
                    if (matches.Count > 0)
                    {
                        violations.Add($"{Path.GetFileName(file)} ({matches.Count} emoji found)");
                    }
                }
            }

            Assert.True(violations.Count == 0, $"Emoji detected in production sources:\n{string.Join("\n", violations)}");
        }

        [Fact]
        public void AT_UI_001_AllPrimaryViewsInstantiateOnSTA()
        {
            var staThread = new Thread(() =>
            {
                var strengthCalc = new StrengthCalculationEngine(new StatusEngine());
                var reportService = new ReportGeneratorService(_uow, strengthCalc, new NpoiTemplateWriter());
                var importService = new ExcelImportService();
                var backupService = new BackupService(_uow, _tempDbPath);
                var diagService = new DiagnosticPackageService(_uow, _tempDbPath);
                var auditService = new AuditService(_uow);

                var mainVM = new MainViewModel(
                    _uow,
                    new StatusEngine(),
                    strengthCalc,
                    new ConflictEngine(),
                    reportService,
                    importService,
                    backupService,
                    diagService,
                    auditService,
                    _clock);

                Assert.NotNull(new DashboardView { DataContext = mainVM.DashboardVM });
                Assert.NotNull(new DynamologioView { DataContext = mainVM.DynamologioVM });
                Assert.NotNull(new PersonnelView { DataContext = mainVM.PersonnelVM });
                Assert.NotNull(new AbsencesView { DataContext = mainVM.AbsencesVM });
                Assert.NotNull(new ServicesView { DataContext = mainVM.ServicesVM });
                Assert.NotNull(new ReportsView { DataContext = mainVM.ReportsVM });
                Assert.NotNull(new ImportExportView { DataContext = mainVM.ImportExportVM });
                Assert.NotNull(new DataValidationView { DataContext = mainVM.DataValidationVM });
                Assert.NotNull(new HistoryView { DataContext = mainVM.HistoryVM });
                Assert.NotNull(new SettingsView { DataContext = mainVM.SettingsVM });
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
        }

        [Fact]
        public void AT_UI_002_GenerateScreenshots_1366x768_and_1024x768()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "Dynamologio.sln")))
            {
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            string screenshotsDir = Path.Combine(current, "docs", "screenshots", "v4");
            if (!Directory.Exists(screenshotsDir)) Directory.CreateDirectory(screenshotsDir);

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

                    var strengthCalc = new StrengthCalculationEngine(new StatusEngine());
                    var reportService = new ReportGeneratorService(_uow, strengthCalc, new NpoiTemplateWriter());
                    var importService = new ExcelImportService();
                    var backupService = new BackupService(_uow, _tempDbPath);
                    var diagService = new DiagnosticPackageService(_uow, _tempDbPath);
                    var auditService = new AuditService(_uow);

                    var mainVM = new MainViewModel(
                        _uow,
                        new StatusEngine(),
                        strengthCalc,
                        new ConflictEngine(),
                        reportService,
                        importService,
                        backupService,
                        diagService,
                        auditService,
                        _clock);

                    var views = new Dictionary<string, FrameworkElement>
                    {
                        { "Dashboard", new DashboardView { DataContext = mainVM.DashboardVM } },
                        { "Dynamologio", new DynamologioView { DataContext = mainVM.DynamologioVM } },
                        { "Personnel", new PersonnelView { DataContext = mainVM.PersonnelVM } },
                        { "Absences", new AbsencesView { DataContext = mainVM.AbsencesVM } },
                        { "Services", new ServicesView { DataContext = mainVM.ServicesVM } },
                        { "Reports", new ReportsView { DataContext = mainVM.ReportsVM } },
                        { "Import", new ImportExportView { DataContext = mainVM.ImportExportVM } },
                        { "Validation", new DataValidationView { DataContext = mainVM.DataValidationVM } },
                        { "History", new HistoryView { DataContext = mainVM.HistoryVM } },
                        { "Settings", new SettingsView { DataContext = mainVM.SettingsVM } }
                    };

                    foreach (var kvp in views)
                    {
                        RenderAndSaveScreenshot(kvp.Value, 1366, 768, Path.Combine(screenshotsDir, $"{kvp.Key}_1366x768.png"));
                        RenderAndSaveScreenshot(kvp.Value, 1024, 768, Path.Combine(screenshotsDir, $"{kvp.Key}_1024x768.png"));
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

        private static void RenderAndSaveScreenshot(FrameworkElement element, int width, int height, string outputPath)
        {
            element.Width = width;
            element.Height = height;
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(element);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var fs = new FileStream(outputPath, FileMode.Create))
            {
                encoder.Save(fs);
            }
        }

        private class FailingAuditService : IAuditService
        {
            public void LogAction(AuditAction action, string entityType, string entityId, string summary, object oldValue = null, object newValue = null, Guid? importBatchId = null, string username = null)
            {
                throw new InvalidOperationException("Σφάλμα προσομοίωσης κατά την εγγραφή του AuditEvent!");
            }
        }
    }
}
