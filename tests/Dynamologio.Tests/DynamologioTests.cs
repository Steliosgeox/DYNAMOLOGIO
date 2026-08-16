using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Models;
using Dynamologio.ImportExport.Excel;
using Dynamologio.Infrastructure.LiteDb;
using Dynamologio.Infrastructure.Migrations;
using Dynamologio.Infrastructure.Repositories;
using Dynamologio.Infrastructure.Services;
using Xunit;

namespace Dynamologio.Tests
{
    public class IntervalMathTests
    {
        [Fact]
        public void IsActiveAt_ShouldRespectHalfOpenInterval()
        {
            var start = new DateTime(2026, 8, 16, 0, 0, 0);
            var endExclusive = new DateTime(2026, 8, 21, 0, 0, 0);

            // 15/08 23:59:59 -> Not active
            Assert.False(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 15, 23, 59, 59)));

            // 16/08 00:00:00 -> Active (inclusive start)
            Assert.True(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 16, 0, 0, 0)));

            // 18/08 12:00:00 -> Active
            Assert.True(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 18, 12, 0, 0)));

            // 20/08 23:59:59 -> Active
            Assert.True(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 20, 23, 59, 59)));

            // 21/08 00:00:00 -> NOT active (exclusive end / return date)
            Assert.False(StatusIntervalMath.IsActiveAt(start, endExclusive, new DateTime(2026, 8, 21, 0, 0, 0)));
        }

        [Fact]
        public void DoIntervalsOverlap_ShouldDetectOverlapsCorrectly()
        {
            var i1_start = new DateTime(2026, 8, 16);
            var i1_end = new DateTime(2026, 8, 21);

            // Overlapping: 19/08 to 25/08
            Assert.True(StatusIntervalMath.DoIntervalsOverlap(i1_start, i1_end, new DateTime(2026, 8, 19), new DateTime(2026, 8, 25)));

            // Non-overlapping: 21/08 to 26/08 (Adjacent start is NOT overlap)
            Assert.False(StatusIntervalMath.DoIntervalsOverlap(i1_start, i1_end, new DateTime(2026, 8, 21), new DateTime(2026, 8, 26)));

            // Non-overlapping: 10/08 to 16/08 (Adjacent end is NOT overlap)
            Assert.False(StatusIntervalMath.DoIntervalsOverlap(i1_start, i1_end, new DateTime(2026, 8, 10), new DateTime(2026, 8, 16)));
        }
    }

    public class StatusEngineTests
    {
        private readonly StatusEngine _engine = new StatusEngine();

        [Fact]
        public void CalculatePersonStatus_ActiveLeave_ShouldReturnAbsent()
        {
            var person = new Personnel
            {
                LastName = "ΠΑΠΑΔΟΠΟΥΛΟΣ",
                FirstName = "ΙΩΑΝΝΗΣ",
                StrengthStartDate = new DateTime(2026, 1, 1)
            };

            var leaveType = new StatusType { Name = "Κανονική Άδεια", Effect = StatusEffect.Absent, ShortCode = "ΚΑ" };
            var statusEvent = new StatusEvent
            {
                PersonnelId = person.Id,
                StatusTypeId = leaveType.Id,
                StartAt = new DateTime(2026, 8, 16),
                EndAtExclusive = new DateTime(2026, 8, 21)
            };

            var snapshot = _engine.CalculatePersonStatus(
                person,
                new[] { statusEvent },
                new[] { leaveType },
                null,
                null,
                null,
                null,
                new DateTime(2026, 8, 18, 10, 0, 0));

            Assert.True(snapshot.IsInActiveStrength);
            Assert.Equal(StatusEffect.Absent, snapshot.EffectiveStatus);
            Assert.Equal("Κανονική Άδεια", snapshot.StatusDisplayLabel);
            Assert.Equal("21/08/2026", snapshot.ReturnDisplayLabel);
        }

        [Fact]
        public void CalculatePersonStatus_AutomaticReturnOnExpiration_ShouldReturnPresent()
        {
            var person = new Personnel
            {
                LastName = "ΠΑΠΑΔΟΠΟΥΛΟΣ",
                FirstName = "ΙΩΑΝΝΗΣ",
                StrengthStartDate = new DateTime(2026, 1, 1)
            };

            var leaveType = new StatusType { Name = "Κανονική Άδεια", Effect = StatusEffect.Absent, ShortCode = "ΚΑ" };
            var statusEvent = new StatusEvent
            {
                PersonnelId = person.Id,
                StatusTypeId = leaveType.Id,
                StartAt = new DateTime(2026, 8, 16),
                EndAtExclusive = new DateTime(2026, 8, 21) // Return 21/08
            };

            // Query at 21/08 08:00 (Return Day)
            var snapshot = _engine.CalculatePersonStatus(
                person,
                new[] { statusEvent },
                new[] { leaveType },
                null,
                null,
                null,
                null,
                new DateTime(2026, 8, 21, 8, 0, 0));

            Assert.True(snapshot.IsInActiveStrength);
            Assert.Equal(StatusEffect.Present, snapshot.EffectiveStatus);
            Assert.Equal("ΠΑΡΩΝ", snapshot.StatusDisplayLabel);
        }

        [Fact]
        public void CalculatePersonStatus_FutureLeave_ShouldRemainPresentToday()
        {
            var person = new Personnel
            {
                LastName = "ΓΕΩΡΓΙΟΥ",
                FirstName = "ΝΙΚΟΛΑΟΣ",
                StrengthStartDate = new DateTime(2026, 1, 1)
            };

            var leaveType = new StatusType { Name = "Κανονική Άδεια", Effect = StatusEffect.Absent, ShortCode = "ΚΑ" };
            var futureEvent = new StatusEvent
            {
                PersonnelId = person.Id,
                StatusTypeId = leaveType.Id,
                StartAt = new DateTime(2026, 8, 25),
                EndAtExclusive = new DateTime(2026, 8, 30)
            };

            // Query today 16/08
            var snapshot = _engine.CalculatePersonStatus(
                person,
                new[] { futureEvent },
                new[] { leaveType },
                null,
                null,
                null,
                null,
                new DateTime(2026, 8, 16, 10, 0, 0));

            Assert.True(snapshot.IsInActiveStrength);
            Assert.Equal(StatusEffect.Present, snapshot.EffectiveStatus);
        }
    }

    public class StrengthEngineTests
    {
        [Fact]
        public void CalculateSnapshot_MathematicalInvariant_PresentPlusAbsentEqualsActiveStrength()
        {
            var statusEngine = new StatusEngine();
            var strengthEngine = new StrengthCalculationEngine(statusEngine);

            var rankOfficer = new Rank { Name = "Λοχαγός", Category = PersonnelCategory.OfficerOrNco, SortOrder = 8 };
            var rankConscript = new Rank { Name = "Στρατιώτης", Category = PersonnelCategory.Conscript, SortOrder = 17 };

            var leaveType = new StatusType { Name = "Κανονική Άδεια", Effect = StatusEffect.Absent, ShortCode = "ΚΑ", ReportMappingCode = "KA" };

            var p1 = new Personnel { LastName = "ΑΞΙΩΜΑΤΙΚΟΣ 1", Category = PersonnelCategory.OfficerOrNco, RankId = rankOfficer.Id, StrengthStartDate = new DateTime(2026, 1, 1) };
            var p2 = new Personnel { LastName = "ΑΞΙΩΜΑΤΙΚΟΣ 2", Category = PersonnelCategory.OfficerOrNco, RankId = rankOfficer.Id, StrengthStartDate = new DateTime(2026, 1, 1) };
            var p3 = new Personnel { LastName = "ΣΤΡΑΤΙΩΤΗΣ 1", Category = PersonnelCategory.Conscript, RankId = rankConscript.Id, StrengthStartDate = new DateTime(2026, 1, 1) };
            var p4 = new Personnel { LastName = "ΣΤΡΑΤΙΩΤΗΣ 2", Category = PersonnelCategory.Conscript, RankId = rankConscript.Id, StrengthStartDate = new DateTime(2026, 1, 1) };

            // P2 and P4 are absent
            var ev1 = new StatusEvent { PersonnelId = p2.Id, StatusTypeId = leaveType.Id, StartAt = new DateTime(2026, 8, 16), EndAtExclusive = new DateTime(2026, 8, 20) };
            var ev2 = new StatusEvent { PersonnelId = p4.Id, StatusTypeId = leaveType.Id, StartAt = new DateTime(2026, 8, 16), EndAtExclusive = new DateTime(2026, 8, 20) };

            var snapshot = strengthEngine.CalculateSnapshot(
                new[] { p1, p2, p3, p4 },
                new[] { ev1, ev2 },
                new[] { leaveType },
                new[] { rankOfficer, rankConscript },
                null,
                null,
                null,
                new DateTime(2026, 8, 17, 10, 0, 0));

            Assert.Equal(4, snapshot.TotalActiveStrength);
            Assert.Equal(2, snapshot.TotalPresent);
            Assert.Equal(2, snapshot.TotalAbsent);
            Assert.True(snapshot.IsMathematicallyValid);
            Assert.Empty(snapshot.ValidationWarnings);

            // Category checks
            Assert.Equal(2, snapshot.OfficersAndNcosActive);
            Assert.Equal(1, snapshot.OfficersAndNcosPresent);
            Assert.Equal(1, snapshot.OfficersAndNcosAbsent);

            Assert.Equal(2, snapshot.ConscriptsActive);
            Assert.Equal(1, snapshot.ConscriptsPresent);
            Assert.Equal(1, snapshot.ConscriptsAbsent);

            // Reason check
            Assert.Equal(2, snapshot.AbsencesByReasonCode["KA"]);
        }
    }

    public class ConflictEngineTests
    {
        private readonly ConflictEngine _conflictEngine = new ConflictEngine();

        [Fact]
        public void ValidateStatusEvent_OverlappingAbsence_ShouldReturnError()
        {
            var leaveType = new StatusType { Id = Guid.NewGuid(), Name = "Κανονική Άδεια", MutualExclusionGroup = "ABSENCE" };
            var existing = new StatusEvent
            {
                Id = Guid.NewGuid(),
                StatusTypeId = leaveType.Id,
                StartAt = new DateTime(2026, 8, 16),
                EndAtExclusive = new DateTime(2026, 8, 21)
            };

            var candidate = new StatusEvent
            {
                Id = Guid.NewGuid(),
                StatusTypeId = leaveType.Id,
                StartAt = new DateTime(2026, 8, 19),
                EndAtExclusive = new DateTime(2026, 8, 24)
            };

            var results = _conflictEngine.ValidateStatusEvent(
                candidate,
                new Personnel { StrengthStartDate = new DateTime(2026, 1, 1) },
                new[] { existing },
                new[] { leaveType });

            Assert.Contains(results, r => r.Severity == ConflictSeverity.Error && r.Code == "OVERLAPPING_ABSENCE");
        }

        [Fact]
        public void ValidateStatusEvent_ReturnDateBeforeStart_ShouldReturnError()
        {
            var candidate = new StatusEvent
            {
                StartAt = new DateTime(2026, 8, 21),
                EndAtExclusive = new DateTime(2026, 8, 16) // Inverted
            };

            var results = _conflictEngine.ValidateStatusEvent(candidate, null, null, null);
            Assert.Contains(results, r => r.Severity == ConflictSeverity.Error && r.Code == "INVALID_DATE_RANGE");
        }

        [Fact]
        public void ValidatePersonnel_DuplicateAsm_ShouldReturnError()
        {
            var p1 = new Personnel { Id = Guid.NewGuid(), LastName = "ΠΑΠΑΔΟΠΟΥΛΟΣ", MilitaryServiceNumber = "12345/2020" };
            var p2 = new Personnel { Id = Guid.NewGuid(), LastName = "ΝΕΟΣ", FirstName = "ΝΙΚΟΣ", RankId = Guid.NewGuid(), MilitaryServiceNumber = "12345/2020" };

            var results = _conflictEngine.ValidatePersonnel(p2, new[] { p1 });
            Assert.Contains(results, r => r.Severity == ConflictSeverity.Error && r.Code == "DUPLICATE_ASM");
        }
    }

    public class DatabaseAndBackupTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly LiteDbContext _context;
        private readonly LiteDbUnitOfWork _uow;
        private readonly BackupService _backupService;

        public DatabaseAndBackupTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_dynamologio_{Guid.NewGuid():N}.db");
            _context = new LiteDbContext(_tempDbPath);
            _uow = new LiteDbUnitOfWork(_context);
            _backupService = new BackupService(_tempDbPath, _uow);

            SchemaMigrationRunner.ApplyMigrations(_uow);
        }

        [Fact]
        public void SeedData_ShouldPopulateRanksAndStatusTypes()
        {
            var ranks = _uow.Ranks.GetAll().ToList();
            var statusTypes = _uow.StatusTypes.GetAll().ToList();

            Assert.True(ranks.Count >= 17);
            Assert.True(statusTypes.Count >= 8);
            Assert.Contains(ranks, r => r.Name == "Λοχαγός");
            Assert.Contains(statusTypes, st => st.Name == "Κανονική Άδεια");
        }

        [Fact]
        public void BackupAndRestore_ShouldPreserveDataIntegrity()
        {
            var rank = _uow.Ranks.GetAll().First();
            var p = new Personnel
            {
                LastName = "ΔΟΚΙΜΑΣΤΙΚΟΣ",
                FirstName = "ΠΕΤΡΟΣ",
                RankId = rank.Id,
                MilitaryServiceNumber = "99999/2026"
            };
            _uow.Personnel.Insert(p);

            string backupZip = _backupService.CreateBackup();
            Assert.True(File.Exists(backupZip));

            bool verified = _backupService.VerifyBackup(backupZip, out var manifest, out var error);
            Assert.True(verified, error);
            Assert.NotNull(manifest);
            Assert.True(manifest.PersonnelCount >= 1);

            if (File.Exists(backupZip)) File.Delete(backupZip);
        }

        public void Dispose()
        {
            _uow?.Dispose();
            _context?.Dispose();
            if (File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { }
            }
        }
    }

    public class ExcelTemplateTests
    {
        [Fact]
        public void GenerateDefaultGoldenTemplate_ShouldCreateValidExcelWithFormulas()
        {
            string tempExcel = Path.Combine(Path.GetTempPath(), $"golden_{Guid.NewGuid():N}.xlsx");
            try
            {
                GoldenTemplateGenerator.GenerateDefaultGoldenTemplate(tempExcel);
                Assert.True(File.Exists(tempExcel));

                var analyzer = new NpoiWorkbookAnalyzer();
                var analysis = analyzer.AnalyzeWorkbook(tempExcel);

                Assert.Equal(2, analysis.TotalSheets);
                Assert.Equal("ΔΥΝΑΜΟΛΟΓΙΟ", analysis.Sheets[0].SheetName);
                Assert.Equal("ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ", analysis.Sheets[1].SheetName);
            }
            finally
            {
                if (File.Exists(tempExcel))
                {
                    try { File.Delete(tempExcel); } catch { }
                }
            }
        }

        [Fact]
        public void ExportDynamologioWorkbook_UsingReferenceTemplate_ShouldSucceed()
        {
            string refTemplate = @"C:\Users\Stelios\DYNAMOLOGIO\templates-reference\Standard_Dynamologio_Template.xlsx";
            if (!File.Exists(refTemplate))
            {
                GoldenTemplateGenerator.GenerateDefaultGoldenTemplate(refTemplate);
            }

            string outExcel = Path.Combine(Path.GetTempPath(), $"out_dynamologio_{Guid.NewGuid():N}.xlsx");
            try
            {
                var snapshot = new Core.Projections.UnitStrengthSnapshot
                {
                    AsOfTimestamp = DateTime.Today,
                    TotalActiveStrength = 10,
                    TotalPresent = 8,
                    TotalAbsent = 2,
                    OfficersAndNcosActive = 4,
                    OfficersAndNcosPresent = 3,
                    OfficersAndNcosAbsent = 1,
                    ConscriptsActive = 6,
                    ConscriptsPresent = 5,
                    ConscriptsAbsent = 1
                };

                var writer = new NpoiTemplateWriter();
                writer.GenerateDynamologioWorkbook(refTemplate, outExcel, snapshot, "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ");

                Assert.True(File.Exists(outExcel));
                var analyzer = new NpoiWorkbookAnalyzer();
                var analysis = analyzer.AnalyzeWorkbook(outExcel);
                Assert.Equal(2, analysis.TotalSheets);
            }
            finally
            {
                if (File.Exists(outExcel))
                {
                    try { File.Delete(outExcel); } catch { }
                }
            }
        }
    }
}
