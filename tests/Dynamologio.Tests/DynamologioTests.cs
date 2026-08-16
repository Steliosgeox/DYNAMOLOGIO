using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

            var snapshot = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 8, 1));
            Assert.False(snapshot.IsInActiveStrength);
            Assert.Equal(StatusEffect.ExcludedFromStrength, snapshot.EffectiveStatus);
        }

        [Fact]
        public void AT_LIFECYCLE_004_HistoricalQuery_ShouldPreservePastActiveStatus_WhenArchivedToday()
        {
            var engine = new StatusEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΙΣΤΟΡΙΚΟΣ",
                FirstName = "ΝΙΚΟΛΑΟΣ",
                StrengthStartDate = new DateTime(2026, 1, 1),
                StrengthEndDate = new DateTime(2026, 9, 1),
                IsArchived = true
            };

            var snapshotPast = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 8, 20));
            Assert.True(snapshotPast.IsInActiveStrength);
            Assert.Equal(StatusEffect.Present, snapshotPast.EffectiveStatus);

            var snapshotFuture = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 9, 2));
            Assert.False(snapshotFuture.IsInActiveStrength);
        }

        // ==========================================
        // 2. ABSENCE & AUTOMATIC EXPIRATION TESTS
        // ==========================================

        [Fact]
        public void AT_ABS_001_StatusIntervalMath_IsActiveAt_ShouldRespectHalfOpenBoundary()
        {
            var start = new DateTime(2026, 8, 16, 0, 0, 0);
            var endExclusive = new DateTime(2026, 8, 21, 0, 0, 0);

            Assert.False(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 15, 23, 59, 59)));
            Assert.True(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 16, 0, 0, 0)));
            Assert.True(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 20, 23, 59, 59)));
            Assert.False(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 21, 0, 0, 0)));
        }

        [Fact]
        public void AT_ABS_002_AutomaticReturnUponExpiration_ShouldEvaluateToPresentOnReturnDate()
        {
            var engine = new StatusEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΠΑΠΑΔΟΠΟΥΛΟΣ",
                FirstName = "ΓΕΩΡΓΙΟΣ",
                StrengthStartDate = new DateTime(2026, 1, 1)
            };

            var kaType = _uow.StatusTypes.Find(x => x.ShortCode == "ΚΑ").First();
            var ev = new StatusEvent
            {
                PersonnelId = person.Id,
                StatusTypeId = kaType.Id,
                StartAt = new DateTime(2026, 8, 16, 0, 0, 0),
                EndAtExclusive = new DateTime(2026, 8, 21, 0, 0, 0)
            };

            var snapshotReturned = engine.CalculatePersonStatus(person, new[] { ev }, _uow.StatusTypes.GetAll(), null, null, null, null, new DateTime(2026, 8, 21, 0, 0, 0));
            Assert.Equal(StatusEffect.Present, snapshotReturned.EffectiveStatus);
        }

        [Fact]
        public void AT_ABS_003_SingleDayAbsence_ShouldHaveCorrectDurationAndReturn()
        {
            var start = new DateTime(2026, 8, 16);
            var endExclusive = new DateTime(2026, 8, 17);

            int days = StatusIntervalMath.CalculateDays(start, endExclusive);
            Assert.Equal(1, days);

            Assert.True(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 16, 14, 0, 0)));
            Assert.False(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 17, 0, 0, 0)));
        }

        [Fact]
        public void AT_ABS_004_CancelledAbsenceEvent_ShouldBeIgnored()
        {
            var engine = new StatusEngine();
            var person = new Personnel { Id = Guid.NewGuid(), StrengthStartDate = new DateTime(2026, 1, 1) };
            var kaType = _uow.StatusTypes.Find(x => x.ShortCode == "ΚΑ").First();

            var cancelledEvent = new StatusEvent
            {
                PersonnelId = person.Id,
                StatusTypeId = kaType.Id,
                StartAt = new DateTime(2026, 8, 16),
                EndAtExclusive = new DateTime(2026, 8, 25),
                IsCancelled = true
            };

            var snapshot = engine.CalculatePersonStatus(person, new[] { cancelledEvent }, _uow.StatusTypes.GetAll(), null, null, null, null, new DateTime(2026, 8, 20));
            Assert.Equal(StatusEffect.Present, snapshot.EffectiveStatus);
        }

        [Fact]
        public void AT_ABS_005_ReturningToday_ShouldCountAccuratelyOnReturnDate()
        {
            var statusEngine = new StatusEngine();
            var strengthEngine = new StrengthCalculationEngine(statusEngine);

            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΕΠΙΣΤΡΕΦΩΝ",
                FirstName = "ΙΩΑΝΝΗΣ",
                Category = PersonnelCategory.Conscript,
                StrengthStartDate = new DateTime(2026, 1, 1)
            };

            var kaType = _uow.StatusTypes.Find(x => x.ShortCode == "ΚΑ").First();
            var ev = new StatusEvent
            {
                PersonnelId = person.Id,
                StatusTypeId = kaType.Id,
                StartAt = new DateTime(2026, 8, 16),
                EndAtExclusive = new DateTime(2026, 8, 21)
            };

            var snapshot = strengthEngine.CalculateSnapshot(
                new[] { person },
                new[] { ev },
                _uow.StatusTypes.GetAll(),
                _uow.Ranks.GetAll(),
                _uow.OrganisationUnits.GetAll(),
                null,
                null,
                new DateTime(2026, 8, 21));

            Assert.Equal(1, snapshot.TotalPresent);
            Assert.Equal(0, snapshot.TotalAbsent);
            Assert.Equal(1, snapshot.ReturningTodayCount);
            Assert.True(snapshot.IsMathematicallyValid);
        }

        [Fact]
        public void AT_ABS_006_CivilianPersonnel_ShouldNotCountAsConscript()
        {
            var statusEngine = new StatusEngine();
            var strengthEngine = new StrengthCalculationEngine(statusEngine);

            var civilian = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΠΟΛΙΤΗΣ",
                FirstName = "ΜΑΡΙΑ",
                Category = PersonnelCategory.Civilian,
                StrengthStartDate = new DateTime(2026, 1, 1)
            };

            var snapshot = strengthEngine.CalculateSnapshot(
                new[] { civilian },
                null,
                _uow.StatusTypes.GetAll(),
                _uow.Ranks.GetAll(),
                _uow.OrganisationUnits.GetAll(),
                null,
                null,
                new DateTime(2026, 8, 16));

            Assert.Equal(1, snapshot.CiviliansActive);
            Assert.Equal(0, snapshot.ConscriptsActive);
            Assert.Equal(1, snapshot.TotalActiveStrength);
        }

        // ==========================================
        // 3. CONFLICT DETECTION ENGINE TESTS
        // ==========================================

        [Fact]
        public void AT_CONFLICT_001_InvalidDateRange_ShouldReturnError()
        {
            var conflictEngine = new ConflictEngine();
            var person = new Personnel { Id = Guid.NewGuid(), StrengthStartDate = new DateTime(2026, 1, 1) };
            var badEvent = new StatusEvent
            {
                PersonnelId = person.Id,
                StartAt = new DateTime(2026, 8, 21),
                EndAtExclusive = new DateTime(2026, 8, 16)
            };

            var conflicts = conflictEngine.ValidateStatusEvent(badEvent, person, null, _uow.StatusTypes.GetAll());
            Assert.Contains(conflicts, c => c.Severity == ConflictSeverity.Error && c.Code == "INVALID_DATE_RANGE");
        }

        [Fact]
        public void AT_CONFLICT_002_DuplicateAsm_ShouldReturnError()
        {
            var conflictEngine = new ConflictEngine();
            var existing = new Personnel { Id = Guid.NewGuid(), MilitaryServiceNumber = "12345/2026", LastName = "Α", FirstName = "Β" };
            var duplicate = new Personnel { Id = Guid.NewGuid(), MilitaryServiceNumber = "12345/2026", LastName = "Γ", FirstName = "Δ" };

            var conflicts = conflictEngine.ValidatePersonnel(duplicate, new[] { existing });
            Assert.Contains(conflicts, c => c.Severity == ConflictSeverity.Error && c.Code == "DUPLICATE_ASM");
        }

        [Fact]
        public void AT_CONFLICT_003_ServiceOutsideStrength_ShouldReturnError()
        {
            var conflictEngine = new ConflictEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                StrengthStartDate = new DateTime(2026, 6, 1),
                StrengthEndDate = new DateTime(2026, 9, 1)
            };

            var assignment = new ServiceAssignment
            {
                PersonnelId = person.Id,
                ServiceDate = new DateTime(2026, 10, 1),
                StartDateTime = new DateTime(2026, 10, 1, 8, 0, 0),
                EndDateTime = new DateTime(2026, 10, 1, 14, 0, 0)
            };

            var conflicts = conflictEngine.ValidateServiceAssignment(assignment, person, null, null);
            Assert.Contains(conflicts, c => c.Severity == ConflictSeverity.Error && c.Code == "SERVICE_OUTSIDE_STRENGTH");
        }

        [Fact]
        public void AT_CONFLICT_004_OverlappingServices_ShouldReturnError()
        {
            var conflictEngine = new ConflictEngine();
            var person = new Personnel { Id = Guid.NewGuid(), StrengthStartDate = new DateTime(2026, 1, 1) };

            var existingService = new ServiceAssignment
            {
                Id = Guid.NewGuid(),
                PersonnelId = person.Id,
                StartDateTime = new DateTime(2026, 8, 16, 8, 0, 0),
                EndDateTime = new DateTime(2026, 8, 16, 14, 0, 0)
            };

            var overlappingService = new ServiceAssignment
            {
                Id = Guid.NewGuid(),
                PersonnelId = person.Id,
                StartDateTime = new DateTime(2026, 8, 16, 12, 0, 0),
                EndDateTime = new DateTime(2026, 8, 16, 18, 0, 0)
            };

            var conflicts = conflictEngine.ValidateServiceAssignment(overlappingService, person, new[] { existingService }, null);
            Assert.Contains(conflicts, c => c.Severity == ConflictSeverity.Error && c.Code == "OVERLAPPING_SERVICE");
        }

        // ==========================================
        // 4. SECURITY & CRYPTOGRAPHY TESTS
        // ==========================================

        [Fact]
        public void AT_SECURITY_001_EncryptedBackup_ShouldRequireValidPassphrase()
        {
            var backupService = new BackupService(_uow, _tempDbPath);
            var testDir = Path.Combine(Path.GetTempPath(), $"backup_enc_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                string passphrase = "SecretPassword2026!";
                var manifest = backupService.CreateBackup(testDir, passphrase);
                Assert.True(manifest.IsEncrypted);

                string backupZip = Path.Combine(testDir, $"Dynamologio_Backup_{manifest.Timestamp:yyyyMMdd_HHmmss}.zip");
                Assert.True(File.Exists(backupZip));

                // Wrong passphrase should throw
                Assert.ThrowsAny<Exception>(() => backupService.RestoreBackup(backupZip, "WrongPassword"));

                // Correct passphrase restores cleanly
                backupService.RestoreBackup(backupZip, passphrase);
            }
            finally
            {
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public void AT_SECURITY_002_TamperedBackup_ShouldBeRejected()
        {
            var backupService = new BackupService(_uow, _tempDbPath);
            var testDir = Path.Combine(Path.GetTempPath(), $"backup_tamper_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                var manifest = backupService.CreateBackup(testDir);
                string backupZip = Path.Combine(testDir, $"Dynamologio_Backup_{manifest.Timestamp:yyyyMMdd_HHmmss}.zip");

                // Tamper with the archive by replacing db content
                string corruptZip = Path.Combine(testDir, "corrupted.zip");
                using (var archive = System.IO.Compression.ZipFile.Open(backupZip, System.IO.Compression.ZipArchiveMode.Read))
                using (var destArchive = System.IO.Compression.ZipFile.Open(corruptZip, System.IO.Compression.ZipArchiveMode.Create))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name == "dynamologio.db")
                        {
                            var newEntry = destArchive.CreateEntry("dynamologio.db");
                            using (var s = newEntry.Open())
                            using (var sw = new StreamWriter(s))
                            {
                                sw.Write("TAMPERED_CONTENT");
                            }
                        }
                        else
                        {
                            var newEntry = destArchive.CreateEntry(entry.FullName);
                            using (var s = entry.Open())
                            using (var ds = newEntry.Open())
                            {
                                s.CopyTo(ds);
                            }
                        }
                    }
                }

                Assert.Throws<InvalidOperationException>(() => backupService.RestoreBackup(corruptZip));
            }
            finally
            {
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        // ==========================================
        // 5. PRODUCTION EMOJI AUTOMATED SCAN TEST
        // ==========================================

        [Fact]
        public void AT_REPO_001_ZeroEmojiInProductionSources()
        {
            string solutionRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string srcDir = Path.Combine(solutionRoot, "src");

            if (!Directory.Exists(srcDir))
            {
                // Fallback to searching relative directory
                srcDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "src"));
            }

            if (Directory.Exists(srcDir))
            {
                var sourceFiles = Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
                    .ToList();

                var emojiRegex = new Regex(@"[\uD83C-\uDBFF\uDC00-\uDFFF]|[\u2600-\u27BF]", RegexOptions.Compiled);
                var violations = new List<string>();

                foreach (var file in sourceFiles)
                {
                    string content = File.ReadAllText(file);
                    var matches = emojiRegex.Matches(content);
                    if (matches.Count > 0)
                    {
                        violations.Add($"{Path.GetFileName(file)} contains {matches.Count} emoji characters (e.g. '{matches[0].Value}')");
                    }
                }

                Assert.Empty(violations);
            }
        }

        [Fact]
        public void AT_AUDIT_001_LogAction_ShouldRecordStructuredEvent()
        {
            var auditService = new AuditService(_uow);
            auditService.LogAction(AuditAction.Create, "Personnel", "123", "Δημιουργία προσώπου", null, new { Name = "Test" });

            var recorded = _uow.AuditEvents.Find(x => x.EntityType == "Personnel" && x.EntityId == "123").FirstOrDefault();
            Assert.NotNull(recorded);
            Assert.Equal(AuditAction.Create, recorded.Action);
            Assert.Equal("Δημιουργία προσώπου", recorded.Summary);
        }

        [Fact]
        public void AT_DIAG_001_ExportDiagnosticPackage_ShouldProduceValidZip()
        {
            var diagService = new DiagnosticPackageService(_uow, _tempDbPath);
            string outputZip = Path.Combine(Path.GetTempPath(), $"diag_test_{Guid.NewGuid():N}.zip");

            try
            {
                diagService.ExportDiagnosticPackage(outputZip);
                Assert.True(File.Exists(outputZip));
                Assert.True(new FileInfo(outputZip).Length > 0);
            }
            finally
            {
                if (File.Exists(outputZip)) File.Delete(outputZip);
            }
        }

        [Fact]
        public void AT_REPORT_001_PrintableDocument_ShouldProduceFlowDocument()
        {
            var statusEngine = new StatusEngine();
            var strengthEngine = new StrengthCalculationEngine(statusEngine);
            var reportService = new ReportGeneratorService(_uow, strengthEngine);

            var req = new ReportGenerationRequest
            {
                AsOfTimestamp = new DateTime(2026, 8, 16),
                UnitTitle = "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ"
            };

            var doc = reportService.GeneratePrintableDocument(req);
            Assert.NotNull(doc);
            Assert.True(doc.Blocks.Count >= 2);
        }
    }
}
