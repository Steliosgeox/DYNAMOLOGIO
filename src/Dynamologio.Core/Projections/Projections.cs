using System;
using System.Collections.Generic;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Models;

namespace Dynamologio.Core.Projections
{
    /// <summary>
    /// Υπολογισμένη κατάσταση ενός προσώπου σε συγκεκριμένη χρονική στιγμή
    /// </summary>
    public class PersonnelStatusSnapshot
    {
        public Personnel Person { get; set; }
        public Rank Rank { get; set; }
        public OrganisationUnit Unit { get; set; }
        public DateTime Timestamp { get; set; }
        
        public bool IsInActiveStrength { get; set; }
        public StatusEffect EffectiveStatus { get; set; } = StatusEffect.Present;
        public StatusType ActiveStatusType { get; set; }
        public StatusEvent ActiveStatusEvent { get; set; }
        public DateTime? ExpectedReturnDate => ActiveStatusEvent?.EndAtExclusive;
        public ServiceAssignment ActiveServiceAssignment { get; set; }
        public ServiceType ActiveServiceType { get; set; }

        public string StatusDisplayLabel
        {
            get
            {
                if (!IsInActiveStrength) return "ΕΚΤΟΣ ΔΥΝΑΜΕΩΣ";
                if (EffectiveStatus == StatusEffect.Absent && ActiveStatusType != null)
                {
                    return ActiveStatusType.Name;
                }
                return "ΠΑΡΩΝ";
            }
        }

        public string ReturnDisplayLabel
        {
            get
            {
                if (EffectiveStatus == StatusEffect.Absent && ExpectedReturnDate.HasValue)
                {
                    return ExpectedReturnDate.Value.ToString("dd/MM/yyyy");
                }
                return "-";
            }
        }

        public string ServiceDisplayLabel => ActiveServiceType != null ? ActiveServiceType.Name : "-";
    }

    /// <summary>
    /// Συγκεντρωτική δύναμη Μονάδας / Υπομονάδας σε συγκεκριμένη χρονική στιγμή
    /// </summary>
    public class UnitStrengthSnapshot
    {
        public DateTime AsOfTimestamp { get; set; }
        public Guid? OrganisationUnitId { get; set; }
        public string OrganisationUnitName { get; set; } = "ΣΥΝΟΛΟ ΜΟΝΑΔΟΣ";

        public int TotalActiveStrength { get; set; }
        public int TotalPresent { get; set; }
        public int TotalAbsent { get; set; }
        public int TotalExcluded { get; set; }

        // Category breakdowns
        public int OfficersAndNcosActive { get; set; }
        public int OfficersAndNcosPresent { get; set; }
        public int OfficersAndNcosAbsent { get; set; }

        public int ConscriptsActive { get; set; }
        public int ConscriptsPresent { get; set; }
        public int ConscriptsAbsent { get; set; }

        // Reason breakdown counts (e.g. "KA" -> 4, "AA" -> 1, "ΦΠ" -> 2)
        public Dictionary<string, int> AbsencesByReasonCode { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> AbsencesByReasonName { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Returning counts
        public int ReturningTodayCount { get; set; }
        public int ReturningTomorrowCount { get; set; }

        // Detailed nominal lists
        public List<PersonnelStatusSnapshot> AllActivePersonnel { get; set; } = new List<PersonnelStatusSnapshot>();
        public List<PersonnelStatusSnapshot> PresentPersonnel { get; set; } = new List<PersonnelStatusSnapshot>();
        public List<PersonnelStatusSnapshot> AbsentPersonnel { get; set; } = new List<PersonnelStatusSnapshot>();
        public List<PersonnelStatusSnapshot> ExcludedPersonnel { get; set; } = new List<PersonnelStatusSnapshot>();

        /// <summary>
        /// Επαλήθευση Μαθηματικών Αναλλοίωτων: ΠΑΡΟΝΤΕΣ + ΑΠΟΝΤΕΣ == ΥΠΑΡΧΟΥΣΑ ΔΥΝΑΜΗ
        /// </summary>
        public bool IsMathematicallyValid => (TotalPresent + TotalAbsent) == TotalActiveStrength;

        public List<string> ValidationWarnings { get; set; } = new List<string>();
    }
}
