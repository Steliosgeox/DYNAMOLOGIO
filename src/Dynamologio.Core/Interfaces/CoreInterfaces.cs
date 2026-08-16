using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;

namespace Dynamologio.Core.Interfaces
{
    public interface IRepository<T> where T : EntityBase
    {
        T GetById(Guid id);
        IEnumerable<T> GetAll();
        IEnumerable<T> Find(Expression<Func<T, bool>> predicate);
        void Insert(T entity);
        void Update(T entity);
        void Delete(Guid id);
        bool Exists(Guid id);
        int Count();
    }

    public interface IUnitOfWork : IDisposable
    {
        IRepository<Personnel> Personnel { get; }
        IRepository<Rank> Ranks { get; }
        IRepository<OrganisationUnit> OrganisationUnits { get; }
        IRepository<StatusType> StatusTypes { get; }
        IRepository<StatusEvent> StatusEvents { get; }
        IRepository<ServiceType> ServiceTypes { get; }
        IRepository<ServiceAssignment> ServiceAssignments { get; }
        IRepository<AuditEvent> AuditEvents { get; }
        IRepository<ReportTemplate> ReportTemplates { get; }
        IRepository<ImportBatch> ImportBatches { get; }
        IRepository<AppSetting> AppSettings { get; }

        void Commit();
        void Rollback();
    }

    public interface IStatusEngine
    {
        PersonnelStatusSnapshot CalculatePersonStatus(
            Personnel person,
            IEnumerable<StatusEvent> allEventsForPerson,
            IEnumerable<StatusType> statusTypes,
            Rank rank,
            OrganisationUnit unit,
            IEnumerable<ServiceAssignment> serviceAssignments,
            IEnumerable<ServiceType> serviceTypes,
            DateTime asOfTimestamp);
    }

    public interface IStrengthCalculator
    {
        UnitStrengthSnapshot CalculateSnapshot(
            IEnumerable<Personnel> personnelList,
            IEnumerable<StatusEvent> allStatusEvents,
            IEnumerable<StatusType> statusTypes,
            IEnumerable<Rank> ranks,
            IEnumerable<OrganisationUnit> organisationUnits,
            IEnumerable<ServiceAssignment> serviceAssignments,
            IEnumerable<ServiceType> serviceTypes,
            DateTime asOfTimestamp,
            Guid? filterUnitId = null);
    }

    public interface IConflictEngine
    {
        List<ConflictResult> ValidateStatusEvent(
            StatusEvent candidateEvent,
            Personnel person,
            IEnumerable<StatusEvent> existingEventsForPerson,
            IEnumerable<StatusType> statusTypes);

        List<ConflictResult> ValidatePersonnel(
            Personnel candidatePerson,
            IEnumerable<Personnel> existingPersonnel);
    }
}
