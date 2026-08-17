using System;
using System.Collections.Generic;
using System.Linq;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;

namespace Dynamologio.Infrastructure.Services
{
    public class StrengthQueryService : IStrengthQueryService
    {
        private readonly IUnitOfWork _uow;
        private readonly IStrengthCalculator _strengthCalculator;

        public StrengthQueryService(IUnitOfWork uow, IStrengthCalculator strengthCalculator)
        {
            _uow = uow;
            _strengthCalculator = strengthCalculator;
        }

        public UnitStrengthSnapshot GetCurrentStrengthSnapshot(DateTime now)
        {
            return GetStrengthSnapshot(now, null);
        }

        public UnitStrengthSnapshot GetStrengthSnapshot(DateTime date, Guid? filterUnitId = null)
        {
            var p = _uow.Personnel.GetAll();
            var ev = _uow.StatusEvents.GetAll();
            var st = _uow.StatusTypes.GetAll();
            var rk = _uow.Ranks.GetAll();
            var un = _uow.OrganisationUnits.GetAll();
            var sa = _uow.ServiceAssignments.GetAll();
            var sv = _uow.ServiceTypes.GetAll();
            return _strengthCalculator.CalculateSnapshot(p, ev, st, rk, un, sa, sv, date, filterUnitId);
        }
    }

    public class PersonnelQueryService : IPersonnelQueryService
    {
        private readonly IUnitOfWork _uow;
        private readonly IStatusEngine _statusEngine;

        public PersonnelQueryService(IUnitOfWork uow, IStatusEngine statusEngine)
        {
            _uow = uow;
            _statusEngine = statusEngine;
        }

        public IEnumerable<PersonnelStatusSnapshot> GetAllPersonnelStatus(DateTime now)
        {
            var personnel = _uow.Personnel.GetAll().ToList();
            var ranks = _uow.Ranks.GetAll().ToDictionary(r => r.Id);
            var units = _uow.OrganisationUnits.GetAll().ToDictionary(u => u.Id);
            var events = _uow.StatusEvents.GetAll().GroupBy(e => e.PersonnelId).ToDictionary(g => g.Key, g => g.ToList());
            var statusTypes = _uow.StatusTypes.GetAll().ToList();
            var services = _uow.ServiceAssignments.GetAll().GroupBy(s => s.PersonnelId).ToDictionary(g => g.Key, g => g.ToList());
            var serviceTypes = _uow.ServiceTypes.GetAll().ToList();

            var result = new List<PersonnelStatusSnapshot>();
            foreach (var p in personnel)
            {
                ranks.TryGetValue(p.RankId, out var rank);
                units.TryGetValue(p.OrganisationUnitId, out var unit);
                events.TryGetValue(p.Id, out var pEvents);
                services.TryGetValue(p.Id, out var pServices);

                var sn = _statusEngine.CalculatePersonStatus(p, pEvents, statusTypes, rank, unit, pServices, serviceTypes, now);
                result.Add(sn);
            }
            return result;
        }

        public IEnumerable<StatusEvent> GetAbsenceHistory(Guid personId)
        {
            return _uow.StatusEvents.Find(e => e.PersonnelId == personId).OrderByDescending(e => e.StartAt).ToList();
        }

        public IEnumerable<ServiceAssignment> GetServiceHistory(Guid personId)
        {
            return _uow.ServiceAssignments.Find(s => s.PersonnelId == personId).OrderByDescending(s => s.ServiceDate).ToList();
        }

        public IEnumerable<Personnel> GetActivePersonnel()
        {
            return _uow.Personnel.Find(p => !p.IsArchived).ToList();
        }

        public IEnumerable<Rank> GetAllRanks()
        {
            return _uow.Ranks.GetAll().ToList();
        }

        public IEnumerable<OrganisationUnit> GetAllUnits()
        {
            return _uow.OrganisationUnits.GetAll().ToList();
        }

        public Personnel GetPerson(Guid personId)
        {
            return _uow.Personnel.GetById(personId);
        }
    }

    public class AbsenceQueryService : IAbsenceQueryService
    {
        private readonly IUnitOfWork _uow;

        public AbsenceQueryService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public IEnumerable<StatusEvent> GetAllEvents()
        {
            return _uow.StatusEvents.Find(x => !x.IsCancelled).OrderByDescending(x => x.StartAt).ToList();
        }

        public IEnumerable<StatusEvent> GetAllActiveEvents(DateTime now)
        {
            return _uow.StatusEvents.Find(x => !x.IsCancelled && x.StartAt <= now && x.EndAtExclusive > now).OrderByDescending(x => x.StartAt).ToList();
        }

        public IEnumerable<StatusEvent> GetAllPlannedEvents(DateTime now)
        {
            return _uow.StatusEvents.Find(x => !x.IsCancelled && x.StartAt > now).OrderByDescending(x => x.StartAt).ToList();
        }

        public IEnumerable<StatusEvent> GetAllPastEvents(DateTime now)
        {
            return _uow.StatusEvents.Find(x => !x.IsCancelled && x.EndAtExclusive <= now).OrderByDescending(x => x.StartAt).ToList();
        }

        public IEnumerable<StatusEvent> GetEventsForPerson(Guid personId)
        {
            return _uow.StatusEvents.Find(x => x.PersonnelId == personId).ToList();
        }

        public IEnumerable<StatusType> GetStatusTypes()
        {
            return _uow.StatusTypes.GetAll().OrderBy(x => x.SortOrder).ToList();
        }
    }

    public class ServiceRosterQueryService : IServiceRosterQueryService
    {
        private readonly IUnitOfWork _uow;

        public ServiceRosterQueryService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public IEnumerable<ServiceAssignment> GetServicesForDate(DateTime date)
        {
            return _uow.ServiceAssignments.Find(x => x.ServiceDate.Date == date.Date).ToList();
        }

        public IEnumerable<ServiceType> GetServiceTypes()
        {
            return _uow.ServiceTypes.GetAll().OrderBy(x => x.SortOrder).ToList();
        }
    }
}
