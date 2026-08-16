using System;

namespace Dynamologio.Core.Engines
{
    /// <summary>
    /// Κεντρική Μαθηματική Μηχανή Χρονικών Διαστημάτων [StartAt, EndAtExclusive)
    /// </summary>
    public static class StatusIntervalMath
    {
        /// <summary>
        /// Ελέγχει αν το timestamp T ανήκει στο ημι-ανοιχτό διάστημα [StartAt, EndAtExclusive)
        /// </summary>
        public static bool IsActiveAt(DateTime startAt, DateTime endAtExclusive, DateTime target)
        {
            if (endAtExclusive <= startAt) return false;
            return target >= startAt && target < endAtExclusive;
        }

        /// <summary>
        /// Ελέγχει αν δύο ημι-ανοιχτά διαστήματα [s1, e1) και [s2, e2) επικαλύπτονται
        /// </summary>
        public static bool DoIntervalsOverlap(DateTime s1, DateTime e1, DateTime s2, DateTime e2)
        {
            if (e1 <= s1 || e2 <= s2) return false;
            return s1 < e2 && s2 < e1;
        }

        /// <summary>
        /// Υπολογίζει τη διάρκεια απουσίας σε ημέρες
        /// </summary>
        public static int CalculateDays(DateTime startAt, DateTime endAtExclusive)
        {
            if (endAtExclusive <= startAt) return 0;
            return (int)Math.Ceiling((endAtExclusive.Date - startAt.Date).TotalDays);
        }

        /// <summary>
        /// Δημιουργεί κανονικοποιημένο ημερήσιο διάστημα χωρίς σιωπηρή αλλοίωση εσφαλμένων ημερομηνιών:
        /// Π.χ. Έναρξη 16/08 00:00, Επιστροφή 21/08 00:00 -> [16/08 00:00, 21/08 00:00)
        /// </summary>
        public static void CreateDayInterval(DateTime startDate, DateTime returnDate, out DateTime startAt, out DateTime endAtExclusive)
        {
            startAt = startDate.Date;
            endAtExclusive = returnDate.Date;
        }
    }
}
