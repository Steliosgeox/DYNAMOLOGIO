using Xunit;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Core.Models;
using System;
using Moq;
using Dynamologio.Core.Interfaces;

namespace Dynamologio.Tests
{
    public class AbsenceServiceTests
    {
        [Fact]
        public void CancelAbsence_ThrowsIfMissing()
        {
            var uow = new Mock<IUnitOfWork>();
            uow.Setup(x => x.StatusEvents.GetById(It.IsAny<Guid>())).Returns((StatusEvent)null);

            var tx = new Mock<ITransactionRunner>();
            tx.Setup(x => x.RunInTransaction(It.IsAny<Action>())).Callback<Action>(a => a());

            var svc = new AbsenceService(
                uow.Object,
                new Mock<IAuditEventPublisher>().Object,
                tx.Object,
                new Mock<IClock>().Object,
                new Mock<ICurrentActor>().Object
            );

            var ex = Assert.Throws<InvalidOperationException>(() => svc.CancelAbsence(Guid.NewGuid(), "Reason"));
            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public void CreateAbsence_FailsOnInvertedDates()
        {
            var svc = new AbsenceService(
                new Mock<IUnitOfWork>().Object,
                new Mock<IAuditEventPublisher>().Object,
                new Mock<ITransactionRunner>().Object,
                new Mock<IClock>().Object,
                new Mock<ICurrentActor>().Object
            );

            var ev = new StatusEvent
            {
                PersonnelId = Guid.NewGuid(),
                StatusTypeId = Guid.NewGuid(),
                StartAt = new DateTime(2025, 1, 10),
                EndAtExclusive = new DateTime(2025, 1, 9)
            };
            
            var ex = Assert.Throws<ArgumentException>(() => svc.CreateAbsence(ev, "Reason"));
            Assert.Contains("Η ημερομηνία έναρξης πρέπει", ex.Message);
        }
    }
}
