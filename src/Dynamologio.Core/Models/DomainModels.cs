using System;
using Dynamologio.Core.Enums;

namespace Dynamologio.Core.Models
{
    public abstract class EntityBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; } = "SYSTEM";
    }

    /// <summary>
    /// Στέλεχος ή Οπλίτης
    /// </summary>
    public class Personnel : EntityBase
    {
        public string MilitaryServiceNumber { get; set; } = string.Empty; // ΑΣΜ / ΑΣΜΑ
        public string LastName { get; set; } = string.Empty;              // Επώνυμο
        public string FirstName { get; set; } = string.Empty;             // Όνομα
        public string FatherName { get; set; } = string.Empty;            // Πατρώνυμο
        public Guid RankId { get; set; }                                  // Βαθμός
        public PersonnelCategory Category { get; set; } = PersonnelCategory.OfficerOrNco;
        public Guid OrganisationUnitId { get; set; }                      // Μονάδα / Υπομονάδα
        public string CompanyOrSection { get; set; } = string.Empty;      // Λόχος / Γραφείο
        public string Specialty { get; set; } = string.Empty;             // Ειδικότητα (π.χ. Τ/Φ, Χειρ. Ασυρμάτου)
        public DateTime StrengthStartDate { get; set; } = DateTime.Today; // Έναρξη στη δύναμη
        public DateTime? StrengthEndDate { get; set; }                    // Λήξη/Διαγραφή (null αν ενεργός)
        public bool IsArchived { get; set; } = false;                     // Αρχειοθετημένος
        public string Notes { get; set; } = string.Empty;                 // Παρατηρήσεις

        public string FullName => $"{LastName} {FirstName}".Trim();
    }

    /// <summary>
    /// Βαθμός (Ιεραρχία)
    /// </summary>
    public class Rank : EntityBase
    {
        public string Name { get; set; } = string.Empty;                  // π.χ. Λοχαγός, Επιλοχίας
        public string ShortName { get; set; } = string.Empty;             // π.χ. Λγος, Επχιας
        public PersonnelCategory Category { get; set; } = PersonnelCategory.OfficerOrNco;
        public int SortOrder { get; set; }                                // Προτεραιότητα ιεραρχίας (1 = υψηλότερος)
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Οργανωτική Μονάδα / Υπομονάδα / Γραφείο
    /// </summary>
    public class OrganisationUnit : EntityBase
    {
        public string Name { get; set; } = string.Empty;                  // π.χ. 1ος Λόχος, 1ο Γραφείο
        public string Code { get; set; } = string.Empty;                  // π.χ. 1ΛΧ, 1ΓΡ
        public Guid? ParentUnitId { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Τύπος Μεταβολής / Απουσίας (Κανονική, Αναρρωτική, ΦΠ, κλπ.)
    /// </summary>
    public class StatusType : EntityBase
    {
        public string Name { get; set; } = string.Empty;                  // π.χ. Κανονική Άδεια
        public string ShortCode { get; set; } = string.Empty;             // π.χ. ΚΑ
        public StatusEffect Effect { get; set; } = StatusEffect.Absent;
        public bool RequiresEndDate { get; set; } = true;
        public bool AllowsTime { get; set; } = false;
        public string MutualExclusionGroup { get; set; } = "ABSENCE";
        public string ReportMappingCode { get; set; } = "KA";             // Κωδικός για το επίσημο Excel
        public string DisplayColorHex { get; set; } = "#D97706";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Γεγονός Κατάστασης / Μεταβολή (ημι-ανοιχτό διάστημα [StartAt, EndAtExclusive))
    /// </summary>
    public class StatusEvent : EntityBase
    {
        public Guid PersonnelId { get; set; }
        public Guid StatusTypeId { get; set; }
        public DateTime StartAt { get; set; }                             // Inclusive έναρξη (π.χ. 16/08 00:00)
        public DateTime EndAtExclusive { get; set; }                      // Exclusive επιστροφή (π.χ. 21/08 00:00)
        public bool IsAllDay { get; set; } = true;
        public string ReferenceDocument { get; set; } = string.Empty;     // Αρ. Διαταγής / Έγκρισης
        public string Comment { get; set; } = string.Empty;               // Αιτιολογία
        public bool IsCancelled { get; set; } = false;                    // Ακυρωμένο (soft cancel για audit)
        public string CancellationReason { get; set; } = string.Empty;
        public DateTime? CancelledAt { get; set; }
        public string CancelledBy { get; set; } = string.Empty;
        public Guid? ImportBatchId { get; set; }                          // Provenance
    }

    /// <summary>
    /// Είδος Υπηρεσίας (Αξ/κος Υπηρεσίας, Επόπτης, Αρχιφύλακας, Σκοπός)
    /// </summary>
    public class ServiceType : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
        public TimeSpan DefaultStartTime { get; set; } = new TimeSpan(8, 0, 0);
        public TimeSpan DefaultEndTime { get; set; } = new TimeSpan(8, 0, 0).Add(TimeSpan.FromHours(24));
        public bool AffectsPresence { get; set; } = false;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Ανάθεση Υπηρεσίας σε Πρόσωπο
    /// </summary>
    public class ServiceAssignment : EntityBase
    {
        public Guid PersonnelId { get; set; }
        public Guid ServiceTypeId { get; set; }
        public DateTime ServiceDate { get; set; }                         // Ημερομηνία Υπηρεσίας
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string DutyLocation { get; set; } = string.Empty;          // Τοποθεσία
        public string Notes { get; set; } = string.Empty;
        public bool IsCancelled { get; set; } = false;
        public string CancellationReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Εγγραφή Ελέγχου Μεταβολών (Audit Trail)
    /// </summary>
    public class AuditEvent : EntityBase
    {
        public string Username { get; set; } = "OPERATOR";
        public AuditAction Action { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string OldValueJson { get; set; } = string.Empty;
        public string NewValueJson { get; set; } = string.Empty;
        public Guid? ImportBatchId { get; set; }
        public string AppVersion { get; set; } = "1.0.0.0";
    }

    /// <summary>
    /// Πρότυπο Επίσημης Αναφοράς (Excel / PDF Template)
    /// </summary>
    public class ReportTemplate : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Sha256Hash { get; set; } = string.Empty;            // Hash προτύπου
        public string RelativeTemplatePath { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
        public string CellMappingJson { get; set; } = "{}";               // JSON με τις αντιστοιχίσεις κελιών
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Παρτίδα Εισαγωγής Αρχείου (Provenance)
    /// </summary>
    public class ImportBatch : EntityBase
    {
        public string FileName { get; set; } = string.Empty;
        public string Sha256Hash { get; set; } = string.Empty;
        public int TotalRecordsProcessed { get; set; }
        public int InsertedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int WarningCount { get; set; }
        public string SummaryNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Ρυθμίσεις Εφαρμογής
    /// </summary>
    public class AppSetting : EntityBase
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
