using Xunit;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Core.Models;
using System;
using Moq;
using Dynamologio.Core.Interfaces;

namespace Dynamologio.Tests
{
    public class DutyServiceTests
    {
        [Fact]
        public void CancelDuty_ThrowsIfMissing()
        {
            var uow = new Mock<IUnitOfWork>();
            uow.Setup(x => x.ServiceAssignments.GetById(It.IsAny<Guid>())).Returns((ServiceAssignment)null);

            var tx = new Mock<ITransactionRunner>();
            tx.Setup(x => x.RunInTransaction(It.IsAny<Action>())).Callback<Action>(a => a());

            var svc = new DutyService(
                uow.Object,
                new Mock<IAuditEventPublisher>().Object,
                tx.Object,
                new Mock<IClock>().Object,
                new Mock<ICurrentActor>().Object
            );

            var ex = Assert.Throws<InvalidOperationException>(() => svc.CancelDuty(Guid.NewGuid(), "Reason"));
            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public void AssignDuty_FailsOnInvertedDates()
        {
            var svc = new DutyService(
                new Mock<IUnitOfWork>().Object,
                new Mock<IAuditEventPublisher>().Object,
                new Mock<ITransactionRunner>().Object,
                new Mock<IClock>().Object,
                new Mock<ICurrentActor>().Object
            );

            var sa = new ServiceAssignment
            {
                PersonnelId = Guid.NewGuid(),
                ServiceTypeId = Guid.NewGuid(),
                ServiceDate = new DateTime(2025, 1, 10),
                StartDateTime = new DateTime(2025, 1, 10, 10, 0, 0),
                EndDateTime = new DateTime(2025, 1, 10, 8, 0, 0)
            };
            
            var ex = Assert.Throws<ArgumentException>(() => svc.AssignDuty(sa, "Reason"));
            Assert.Contains("Η έναρξη υπηρεσίας πρέπει", ex.Message);
        }
    }
}
