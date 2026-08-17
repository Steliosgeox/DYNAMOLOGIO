using Xunit;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Core.Models;
using System;
using Moq;
using Dynamologio.Core.Interfaces;

namespace Dynamologio.Tests
{
    public class PersonnelServiceTests
    {
        [Fact]
        public void CreatePerson_FailsIfFieldsAreMissing()
        {
            var tx = new TestTransactionRunner();

            var svc = new PersonnelService(
                new Mock<IUnitOfWork>().Object,
                new Mock<IAuditEventPublisher>().Object,
                tx,
                new Mock<IClock>().Object,
                new Mock<ICurrentActor>().Object
            );

            // Empty Person
            var p = new Personnel();
            
            var ex = Assert.Throws<ArgumentException>(() => svc.CreatePerson(p, "Reason"));
            Assert.Contains("επώνυμο", ex.Message);
        }

        [Fact]
        public void UpdatePerson_ThrowsIfMissing()
        {
            var uow = new Mock<IUnitOfWork>();
            var ranksRepo = new Mock<IRepository<Rank>>();
            var unitsRepo = new Mock<IRepository<OrganisationUnit>>();
            var personnelRepo = new Mock<IRepository<Personnel>>();
            
            uow.Setup(x => x.Ranks).Returns(ranksRepo.Object);
            uow.Setup(x => x.OrganisationUnits).Returns(unitsRepo.Object);
            uow.Setup(x => x.Personnel).Returns(personnelRepo.Object);
            
            personnelRepo.Setup(x => x.GetById(It.IsAny<Guid>())).Returns((Personnel)null);

            var tx = new TestTransactionRunner();

            var svc = new PersonnelService(
                uow.Object,
                new Mock<IAuditEventPublisher>().Object,
                tx,
                new Mock<IClock>().Object,
                new Mock<ICurrentActor>().Object
            );

            var p = new Personnel { LastName = "A", FirstName = "B", MilitaryServiceNumber = "1", RankId = Guid.NewGuid(), OrganisationUnitId = Guid.NewGuid() };
            
            personnelRepo.Setup(x => x.Find(It.IsAny<System.Linq.Expressions.Expression<Func<Personnel, bool>>>())).Returns(new System.Collections.Generic.List<Personnel>());
            ranksRepo.Setup(x => x.GetById(It.IsAny<Guid>())).Returns(new Rank { Category = Dynamologio.Core.Enums.PersonnelCategory.OfficerOrNco, Name = "Test" });
            unitsRepo.Setup(x => x.GetById(It.IsAny<Guid>())).Returns(new OrganisationUnit { Name = "Test" });
            p.Category = Dynamologio.Core.Enums.PersonnelCategory.OfficerOrNco;

            var ex = Assert.Throws<InvalidOperationException>(() => svc.UpdatePerson(p, "Reason"));
            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public void ArchivePerson_ThrowsIfMissing()
        {
            var uow = new Mock<IUnitOfWork>();
            var personnelRepo = new Mock<IRepository<Personnel>>();
            uow.Setup(x => x.Personnel).Returns(personnelRepo.Object);
            personnelRepo.Setup(x => x.GetById(It.IsAny<Guid>())).Returns((Personnel)null);

            var tx = new TestTransactionRunner();

            var svc = new PersonnelService(
                uow.Object,
                new Mock<IAuditEventPublisher>().Object,
                tx,
                new Mock<IClock>().Object,
                new Mock<ICurrentActor>().Object
            );

            var ex = Assert.Throws<InvalidOperationException>(() => svc.ArchivePerson(Guid.NewGuid(), "Reason"));
            Assert.Contains("not found", ex.Message);
        }
    }
}
