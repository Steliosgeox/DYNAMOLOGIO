using System;
using System.Collections.Generic;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;

namespace Dynamologio.Core.Interfaces
{
    public interface IStrengthQueryService
    {
        UnitStrengthSnapshot GetCurrentStrengthSnapshot(DateTime now);
        UnitStrengthSnapshot GetStrengthSnapshot(DateTime date, Guid? filterUnitId = null);
    }

    public interface IPersonnelQueryService
    {
        IEnumerable<PersonnelStatusSnapshot> GetAllPersonnelStatus(DateTime now);
        IEnumerable<StatusEvent> GetAbsenceHistory(Guid personId);
        IEnumerable<ServiceAssignment> GetServiceHistory(Guid personId);
        IEnumerable<Personnel> GetActivePersonnel();
        IEnumerable<Rank> GetAllRanks();
        IEnumerable<OrganisationUnit> GetAllUnits();
        Personnel GetPerson(Guid personId);
    }

    public interface IAbsenceQueryService
    {
        IEnumerable<StatusEvent> GetAllActiveEvents(DateTime now);
        IEnumerable<StatusEvent> GetAllPlannedEvents(DateTime now);
        IEnumerable<StatusEvent> GetAllPastEvents(DateTime now);
        IEnumerable<StatusEvent> GetAllEvents();
        IEnumerable<StatusEvent> GetEventsForPerson(Guid personId);
        IEnumerable<StatusType> GetStatusTypes();
    }

    public interface IServiceRosterQueryService
    {
        IEnumerable<ServiceAssignment> GetServicesForDate(DateTime date);
        IEnumerable<ServiceType> GetServiceTypes();
    }
}
