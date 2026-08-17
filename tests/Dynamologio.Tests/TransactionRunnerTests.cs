using Xunit;
using System;
using System.Linq;
using Dynamologio.Core.Models;
using Dynamologio.Core.Enums;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Infrastructure.LiteDb;
using Dynamologio.Infrastructure.Security;

namespace Dynamologio.Tests
{
    public class TransactionRunnerTests : DynamologioTests
    {
        [Fact]
        public void AT_TX_001_AuditFailureRollback_ForPersonnel()
        {
            var failingAudit = new FailingAuditPublisher();
            var actor = new TestCurrentActor();
            var tx = new LiteDbTransactionRunner(_uow);
            var svc = new PersonnelService(_uow, failingAudit, tx, _clock, actor);

            var rk = new Rank { Name = "Λγος", Category = PersonnelCategory.OfficerOrNco };
            var un = new OrganisationUnit { Name = "ΛΧ" };
            _uow.Ranks.Insert(rk);
            _uow.OrganisationUnits.Insert(un);

            var person = new Personnel 
            { 
                LastName = "ΔΟΚΙΜΗ", 
                FirstName = "ΑΠΟΤΥΧΙΑΣ",
                MilitaryServiceNumber = "12345",
                RankId = rk.Id,
                OrganisationUnitId = un.Id,
                Category = PersonnelCategory.OfficerOrNco
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                svc.CreatePerson(person, "Δοκιμή");
            });

            Assert.Empty(_uow.Personnel.GetAll());
            Assert.Empty(_uow.AuditEvents.GetAll());
        }

        [Fact]
        public void AT_TX_002_AuditFailureRollback_ForAbsence()
        {
            var failingAudit = new FailingAuditPublisher();
            var actor = new TestCurrentActor();
            var tx = new LiteDbTransactionRunner(_uow);
            var svc = new AbsenceService(_uow, failingAudit, tx, _clock, actor);

            var p = new Personnel { LastName = "Δ", FirstName = "Α", MilitaryServiceNumber = "1", RankId = Guid.NewGuid(), OrganisationUnitId = Guid.NewGuid() };
            _uow.Personnel.Insert(p);
            var st = new StatusType { Name = "ΚΑ", Effect = StatusEffect.Absent };
            _uow.StatusTypes.Insert(st);

            var ev = new StatusEvent { PersonnelId = p.Id, StatusTypeId = st.Id, StartAt = DateTime.Today, EndAtExclusive = DateTime.Today.AddDays(3) };

            Assert.Throws<InvalidOperationException>(() =>
            {
                svc.CreateAbsence(ev, "Δοκιμή");
            });

            Assert.Empty(_uow.StatusEvents.GetAll());
            Assert.Empty(_uow.AuditEvents.GetAll());
        }

        [Fact]
        public void AT_TX_003_AuditFailureRollback_ForService()
        {
            var failingAudit = new FailingAuditPublisher();
            var actor = new TestCurrentActor();
            var tx = new LiteDbTransactionRunner(_uow);
            var svc = new DutyService(_uow, failingAudit, tx, _clock, actor);

            var p = new Personnel { LastName = "Δ", FirstName = "Α", MilitaryServiceNumber = "1", RankId = Guid.NewGuid(), OrganisationUnitId = Guid.NewGuid() };
            _uow.Personnel.Insert(p);
            var sv = new ServiceType { Name = "ΑΥ" };
            _uow.ServiceTypes.Insert(sv);

            var duty = new ServiceAssignment
            {
                PersonnelId = p.Id,
                ServiceTypeId = sv.Id,
                ServiceDate = DateTime.Today,
                StartDateTime = DateTime.Today.AddHours(8),
                EndDateTime = DateTime.Today.AddHours(16),
                DutyLocation = "Διοικητήριο"
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                svc.AssignDuty(duty, "Δοκιμή");
            });

            Assert.Empty(_uow.ServiceAssignments.GetAll());
            Assert.Empty(_uow.AuditEvents.GetAll());
        }
    }
}
