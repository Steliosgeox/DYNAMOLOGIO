using System;
using System.Collections.Generic;
using System.Linq;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;

namespace Dynamologio.Core.Engines
{
    public class StatusEngine : IStatusEngine
    {
        public PersonnelStatusSnapshot CalculatePersonStatus(
            Personnel person,
            IEnumerable<StatusEvent> allEventsForPerson,
            IEnumerable<StatusType> statusTypes,
            Rank rank,
            OrganisationUnit unit,
            IEnumerable<ServiceAssignment> serviceAssignments,
            IEnumerable<ServiceType> serviceTypes,
            DateTime asOfTimestamp)
        {
            var snapshot = new PersonnelStatusSnapshot
            {
                Person = person,
                Rank = rank,
                Unit = unit,
                Timestamp = asOfTimestamp
            };

            if (person == null)
            {
                snapshot.IsInActiveStrength = false;
                snapshot.EffectiveStatus = StatusEffect.ExcludedFromStrength;
                return snapshot;
            }

            // 1. Έλεγχος Ένταξης στη Δύναμη βάσει Ημερομηνιών Ισχύος (Effective Dates)
            // ΣΗΜΕΙΩΣΗ: Το IsArchived ΔΕΝ αλλοιώνει τα ιστορικά δεδομένα πριν την ημερομηνία εξόδου
            bool isAfterStart = asOfTimestamp >= person.StrengthStartDate;
            bool isBeforeEnd = !person.StrengthEndDate.HasValue || asOfTimestamp < person.StrengthEndDate.Value;
            bool isActiveInStrength = isAfterStart && isBeforeEnd;

            snapshot.IsInActiveStrength = isActiveInStrength;

            if (!isActiveInStrength)
            {
                snapshot.EffectiveStatus = StatusEffect.ExcludedFromStrength;
                return snapshot;
            }

            // 2. Εύρεση Ενεργών Γεγονότων Κατάστασης κατά το asOfTimestamp
            var activeEvent = allEventsForPerson?
                .Where(e => !e.IsCancelled && StatusIntervalMath.IsActiveAt(e.StartAt, e.EndAtExclusive, asOfTimestamp))
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();

            if (activeEvent != null)
            {
                snapshot.ActiveStatusEvent = activeEvent;
                var sType = statusTypes?.FirstOrDefault(st => st.Id == activeEvent.StatusTypeId);
                snapshot.ActiveStatusType = sType;
                snapshot.EffectiveStatus = sType?.Effect ?? StatusEffect.Absent;
            }
            else
            {
                // Αν δεν υπάρχει ενεργή απουσία, είναι αυτόματα ΠΑΡΩΝ
                snapshot.EffectiveStatus = StatusEffect.Present;
            }

            // 3. Εύρεση Ενεργής Υπηρεσίας
            var activeService = serviceAssignments?
                .Where(s => !s.IsCancelled && asOfTimestamp >= s.StartDateTime && asOfTimestamp < s.EndDateTime)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            if (activeService != null)
            {
                snapshot.ActiveServiceAssignment = activeService;
                snapshot.ActiveServiceType = serviceTypes?.FirstOrDefault(st => st.Id == activeService.ServiceTypeId);
            }

            return snapshot;
        }
    }
}
