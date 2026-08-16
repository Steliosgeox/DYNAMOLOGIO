using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        [Fact]
        public void StatusIntervalMath_IsActiveAt_ShouldRespectHalfOpenBoundary()
        {
            var start = new DateTime(2026, 8, 16, 0, 0, 0);
            var endExclusive = new DateTime(2026, 8, 21, 0, 0, 0);

            Assert.False(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 15, 23, 59, 59)));
            Assert.True(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 16, 0, 0, 0)));
            Assert.True(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 20, 23, 59, 59)));
            Assert.False(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 21, 0, 0, 0)));
        }

        [Fact]
        public void StatusEngine_AutomaticReturnUponExpiration_ShouldEvaluateToPresent()
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

            // During leave
            var snapshotDuring = engine.CalculatePersonStatus(person, new[] { ev }, _uow.StatusTypes.GetAll(), null, null, null, null, new DateTime(2026, 8, 20, 12, 0, 0));
            Assert.Equal(StatusEffect.Absent, snapshotDuring.EffectiveStatus);

            // On Return Date
            var snapshotReturned = engine.CalculatePersonStatus(person, new[] { ev }, _uow.StatusTypes.GetAll(), null, null, null, null, new DateTime(2026, 8, 21, 0, 0, 0));
            Assert.Equal(StatusEffect.Present, snapshotReturned.EffectiveStatus);
        }

        [Fact]
        public void StatusEngine_HistoricalArchiveQuery_ShouldPreservePastActiveStatus()
        {
            // BUG AUD-001 Regression Test: Archiving person today should NOT exclude them from past historical queries
            var engine = new StatusEngine();
            var person = new Personnel
            {
                Id = Guid.NewGuid(),
                LastName = "ΑΡΧΕΙΟΘΕΤΗΜΕΝΟΣ",
                FirstName = "ΝΙΚΟΛΑΟΣ",
                StrengthStartDate = new DateTime(2026, 1, 1),
                StrengthEndDate = new DateTime(2026, 9, 1), // Departed September 1st
                IsArchived = true
            };

            // Query on August 20th (Historical query while active)
            var snapshotPast = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 8, 20));
            Assert.True(snapshotPast.IsInActiveStrength);
            Assert.Equal(StatusEffect.Present, snapshotPast.EffectiveStatus);

            // Query on September 2nd (After departure)
            var snapshotFuture = engine.CalculatePersonStatus(person, null, null, null, null, null, null, new DateTime(2026, 9, 2));
            Assert.False(snapshotFuture.IsInActiveStrength);
            Assert.Equal(StatusEffect.ExcludedFromStrength, snapshotFuture.EffectiveStatus);
        }

        [Fact]
        public void StrengthCalculationEngine_ReturningToday_ShouldCountAccurately()
        {
            // BUG AUD-004 Regression Test: Returning Today count must work when person returns to Present on return date
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
                EndAtExclusive = new DateTime(2026, 8, 21) // Return date is Aug 21
            };

            var snapshot = strengthEngine.CalculateSnapshot(
                new[] { person },
                new[] { ev },
                _uow.StatusTypes.GetAll(),
                _uow.Ranks.GetAll(),
                _uow.OrganisationUnits.GetAll(),
                null,
                null,
                new DateTime(2026, 8, 21)); // Evaluated ON the return date

            Assert.Equal(1, snapshot.TotalPresent);
            Assert.Equal(0, snapshot.TotalAbsent);
            Assert.Equal(1, snapshot.ReturningTodayCount);
            Assert.True(snapshot.IsMathematicallyValid);
        }

        [Fact]
        public void StrengthCalculationEngine_CivilianPersonnel_ShouldNotCountAsConscript()
        {
            // BUG AUD-006 Regression Test: Civilian personnel category must not silently fall through into Conscript count
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
            Assert.Equal(0, snapshot.OfficersAndNcosActive);
            Assert.Equal(1, snapshot.TotalActiveStrength);
        }

        [Fact]
        public void ConflictEngine_InvalidDateRange_ShouldReturnError()
        {
            // BUG AUD-003 Regression Test: Return <= Start must be detected as an error
            var conflictEngine = new ConflictEngine();
            var person = new Personnel { Id = Guid.NewGuid(), StrengthStartDate = new DateTime(2026, 1, 1) };
            var badEvent = new StatusEvent
            {
                PersonnelId = person.Id,
                StartAt = new DateTime(2026, 8, 21),
                EndAtExclusive = new DateTime(2026, 8, 16) // Return BEFORE start
            };

            var conflicts = conflictEngine.ValidateStatusEvent(badEvent, person, null, _uow.StatusTypes.GetAll());
            Assert.Contains(conflicts, c => c.Severity == ConflictSeverity.Error && c.Code == "INVALID_DATE_RANGE");
        }

        [Fact]
        public void ConflictEngine_DuplicateAsm_ShouldReturnError()
        {
            var conflictEngine = new ConflictEngine();
            var existing = new Personnel { Id = Guid.NewGuid(), MilitaryServiceNumber = "12345/2026", LastName = "Α", FirstName = "Β" };
            var duplicate = new Personnel { Id = Guid.NewGuid(), MilitaryServiceNumber = "12345/2026", LastName = "Γ", FirstName = "Δ" };

            var conflicts = conflictEngine.ValidatePersonnel(duplicate, new[] { existing });
            Assert.Contains(conflicts, c => c.Severity == ConflictSeverity.Error && c.Code == "DUPLICATE_ASM");
        }

        [Fact]
        public void ConflictEngine_ServiceOutsideStrength_ShouldReturnError()
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
                ServiceDate = new DateTime(2026, 10, 1), // Assigned AFTER departure date
                StartDateTime = new DateTime(2026, 10, 1, 8, 0, 0),
                EndDateTime = new DateTime(2026, 10, 1, 14, 0, 0)
            };

            var conflicts = conflictEngine.ValidateServiceAssignment(assignment, person, null, null);
            Assert.Contains(conflicts, c => c.Severity == ConflictSeverity.Error && c.Code == "SERVICE_OUTSIDE_STRENGTH");
        }

        [Fact]
        public void StatusIntervalMath_DoIntervalsOverlap_ShouldDetectCollisionsCorrectly()
        {
            var s1 = new DateTime(2026, 8, 10);
            var e1 = new DateTime(2026, 8, 15);

            // Adjacent: [10, 15) and [15, 20) -> Do NOT overlap
            Assert.False(StatusIntervalMath.DoIntervalsOverlap(s1, e1, new DateTime(2026, 8, 15), new DateTime(2026, 8, 20)));

            // Overlapping: [10, 15) and [14, 18) -> Overlap!
            Assert.True(StatusIntervalMath.DoIntervalsOverlap(s1, e1, new DateTime(2026, 8, 14), new DateTime(2026, 8, 18)));

            // Contained: [10, 15) and [11, 13) -> Overlap!
            Assert.True(StatusIntervalMath.DoIntervalsOverlap(s1, e1, new DateTime(2026, 8, 11), new DateTime(2026, 8, 13)));
        }

        [Fact]
        public void AuditService_LogAction_ShouldRecordStructuredEvent()
        {
            var auditService = new AuditService(_uow);
            auditService.LogAction(AuditAction.Create, "Personnel", "123", "Δημιουργία προσώπου", null, new { Name = "Test" });

            var recorded = _uow.AuditEvents.Find(x => x.EntityType == "Personnel" && x.EntityId == "123").FirstOrDefault();
            Assert.NotNull(recorded);
            Assert.Equal(AuditAction.Create, recorded.Action);
            Assert.Equal("Δημιουργία προσώπου", recorded.Summary);
        }

        [Fact]
        public void DiagnosticPackageService_Export_ShouldProduceValidZip()
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
        public void ReportGeneratorService_PrintableDocument_ShouldProduceFlowDocument()
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

        [Fact]
        public void BackupService_CreateAndRestore_ShouldGuaranteeDataIntegrity()
        {
            var backupService = new BackupService(_uow, _tempDbPath);
            var testDir = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                var manifest = backupService.CreateBackup(testDir);
                Assert.NotNull(manifest);
                Assert.NotEmpty(manifest.DatabaseSha256);

                string backupZip = Path.Combine(testDir, $"Dynamologio_Backup_{manifest.Timestamp:yyyyMMdd_HHmmss}.zip");
                Assert.True(File.Exists(backupZip));

                // Verify restore
                backupService.RestoreBackup(backupZip);
            }
            finally
            {
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }
    }
}
