using System;

namespace Dynamologio.Core.Enums
{
    /// <summary>
    /// Κατηγορία Προσωπικού (Στελέχη, Οπλίτες, Πολιτικό Προσωπικό)
    /// </summary>
    public enum PersonnelCategory
    {
        /// <summary>
        /// Στέλεχος (Αξιωματικοί, Ανθυπασπιστές, Υπαξιωματικοί, ΕΠΟΠ, ΟΒΑ)
        /// </summary>
        OfficerOrNco = 1,

        /// <summary>
        /// Οπλίτης Θητείας / Στρατιώτης
        /// </summary>
        Conscript = 2,

        /// <summary>
        /// Πολιτικό Προσωπικό
        /// </summary>
        Civilian = 3
    }

    /// <summary>
    /// Επίπτωση Κατάστασης στη Δύναμη (Παρών, Απών, Εκτός Δυνάμεως)
    /// </summary>
    public enum StatusEffect
    {
        /// <summary>
        /// Παρών στη Μονάδα
        /// </summary>
        Present = 1,

        /// <summary>
        /// Απών από τη Μονάδα (π.χ. Άδεια, Νοσηλεία, Φύλλο Πορείας)
        /// </summary>
        Absent = 2,

        /// <summary>
        /// Εκτός Παρούσας Δυνάμεως (π.χ. Μετάθεση, Απόλυση)
        /// </summary>
        ExcludedFromStrength = 3
    }

    /// <summary>
    /// Βαθμός Σοβαρότητας Ελέγχου Συγκρούσεων
    /// </summary>
    public enum ConflictSeverity
    {
        Information = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Είδος Ενέργειας Καταγραφής Ελέγχου (Audit)
    /// </summary>
    public enum AuditAction
    {
        Create = 1,
        Update = 2,
        Cancel = 3,
        Archive = 4,
        Restore = 5,
        Import = 6,
        Delete = 7
    }
}
