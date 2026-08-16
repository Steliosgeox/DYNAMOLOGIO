using System;
using System.Collections.Generic;
using System.Linq;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;

namespace Dynamologio.Core.Engines
{
    public class StrengthCalculationEngine : IStrengthCalculator
    {
        private readonly IStatusEngine _statusEngine;

        public StrengthCalculationEngine(IStatusEngine statusEngine)
        {
            _statusEngine = statusEngine ?? new StatusEngine();
        }

        public UnitStrengthSnapshot CalculateSnapshot(
            IEnumerable<Personnel> personnelList,
            IEnumerable<StatusEvent> allStatusEvents,
            IEnumerable<StatusType> statusTypes,
            IEnumerable<Rank> ranks,
            IEnumerable<OrganisationUnit> organisationUnits,
            IEnumerable<ServiceAssignment> serviceAssignments,
            IEnumerable<ServiceType> serviceTypes,
            DateTime asOfTimestamp,
            Guid? filterUnitId = null)
        {
            var snapshot = new UnitStrengthSnapshot
            {
                AsOfTimestamp = asOfTimestamp,
                OrganisationUnitId = filterUnitId
            };

            var rankDict = ranks?.ToDictionary(r => r.Id) ?? new Dictionary<Guid, Rank>();
            var unitDict = organisationUnits?.ToDictionary(u => u.Id) ?? new Dictionary<Guid, OrganisationUnit>();
            var statusTypeDict = statusTypes?.ToDictionary(st => st.Id) ?? new Dictionary<Guid, StatusType>();
            var serviceTypeDict = serviceTypes?.ToDictionary(st => st.Id) ?? new Dictionary<Guid, ServiceType>();

            var eventsList = allStatusEvents?.Where(e => !e.IsCancelled).ToList() ?? new List<StatusEvent>();
            var eventsByPerson = eventsList.GroupBy(e => e.PersonnelId).ToDictionary(g => g.Key, g => g.ToList());

            var servicesByPerson = serviceAssignments?
                .Where(s => !s.IsCancelled)
                .GroupBy(s => s.PersonnelId)
                .ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<Guid, List<ServiceAssignment>>();

            if (filterUnitId.HasValue && unitDict.TryGetValue(filterUnitId.Value, out var selectedUnit))
            {
                snapshot.OrganisationUnitName = selectedUnit.Name;
            }

            var candidatePersonnel = personnelList ?? Enumerable.Empty<Personnel>();
            if (filterUnitId.HasValue)
            {
                candidatePersonnel = candidatePersonnel.Where(p => p.OrganisationUnitId == filterUnitId.Value);
            }

            var todayDate = asOfTimestamp.Date;
            var tomorrowDate = todayDate.AddDays(1);

            // 1. Calculate Returning Counts across ALL scheduled absence events for active personnel on this date
            var candidateList = candidatePersonnel.ToList();
            var candidateIds = new HashSet<Guid>(candidateList.Select(p => p.Id));

            foreach (var ev in eventsList.Where(e => candidateIds.Contains(e.PersonnelId)))
            {
                if (ev.EndAtExclusive.Date == todayDate)
                {
                    snapshot.ReturningTodayCount++;
                }
                else if (ev.EndAtExclusive.Date == tomorrowDate)
                {
                    snapshot.ReturningTomorrowCount++;
                }
            }

            // 2. Aggregate Personnel Status
            foreach (var person in candidateList)
            {
                rankDict.TryGetValue(person.RankId, out var rank);
                unitDict.TryGetValue(person.OrganisationUnitId, out var unit);
                eventsByPerson.TryGetValue(person.Id, out var personEvents);
                servicesByPerson.TryGetValue(person.Id, out var personServices);

                var pSnapshot = _statusEngine.CalculatePersonStatus(
                    person,
                    personEvents,
                    statusTypes,
                    rank,
                    unit,
                    personServices,
                    serviceTypes,
                    asOfTimestamp);

                if (!pSnapshot.IsInActiveStrength)
                {
                    snapshot.TotalExcluded++;
                    snapshot.ExcludedPersonnel.Add(pSnapshot);
                    continue;
                }

                snapshot.TotalActiveStrength++;
                snapshot.AllActivePersonnel.Add(pSnapshot);

                // Explicit Category handling (no silent fallthrough)
                switch (person.Category)
                {
                    case PersonnelCategory.OfficerOrNco:
                        snapshot.OfficersAndNcosActive++;
                        break;
                    case PersonnelCategory.Conscript:
                        snapshot.ConscriptsActive++;
                        break;
                    case PersonnelCategory.Civilian:
                        snapshot.CiviliansActive++;
                        break;
                }

                if (pSnapshot.EffectiveStatus == StatusEffect.Present)
                {
                    snapshot.TotalPresent++;
                    snapshot.PresentPersonnel.Add(pSnapshot);

                    switch (person.Category)
                    {
                        case PersonnelCategory.OfficerOrNco:
                            snapshot.OfficersAndNcosPresent++;
                            break;
                        case PersonnelCategory.Conscript:
                            snapshot.ConscriptsPresent++;
                            break;
                        case PersonnelCategory.Civilian:
                            snapshot.CiviliansPresent++;
                            break;
                    }
                }
                else if (pSnapshot.EffectiveStatus == StatusEffect.Absent)
                {
                    snapshot.TotalAbsent++;
                    snapshot.AbsentPersonnel.Add(pSnapshot);

                    switch (person.Category)
                    {
                        case PersonnelCategory.OfficerOrNco:
                            snapshot.OfficersAndNcosAbsent++;
                            break;
                        case PersonnelCategory.Conscript:
                            snapshot.ConscriptsAbsent++;
                            break;
                        case PersonnelCategory.Civilian:
                            snapshot.CiviliansAbsent++;
                            break;
                    }

                    // Reason counts
                    if (pSnapshot.ActiveStatusType != null)
                    {
                        string code = pSnapshot.ActiveStatusType.ReportMappingCode ?? pSnapshot.ActiveStatusType.ShortCode ?? "ΑΠ";
                        string name = pSnapshot.ActiveStatusType.Name ?? "Απουσία";

                        snapshot.AbsencesByReasonCode[code] = snapshot.AbsencesByReasonCode.TryGetValue(code, out var cVal) ? cVal + 1 : 1;
                        snapshot.AbsencesByReasonName[name] = snapshot.AbsencesByReasonName.TryGetValue(name, out var nVal) ? nVal + 1 : 1;
                    }
                }
            }

            // Invariant verification check
            if (!snapshot.IsMathematicallyValid)
            {
                snapshot.ValidationWarnings.Add($"Σφάλμα Αναλλοίωτου: Παρόντες ({snapshot.TotalPresent}) + Απόντες ({snapshot.TotalAbsent}) != Ενεργή Δύναμη ({snapshot.TotalActiveStrength})");
            }

            return snapshot;
        }
    }
}
