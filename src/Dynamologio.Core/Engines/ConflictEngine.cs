using System;
using System.Collections.Generic;
using System.Linq;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;

namespace Dynamologio.Core.Engines
{
    public class ConflictResult
    {
        public ConflictSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Guid? TargetEntityId { get; set; }
    }

    public class ConflictEngine : IConflictEngine
    {
        public List<ConflictResult> ValidateStatusEvent(
            StatusEvent candidateEvent,
            Personnel person,
            IEnumerable<StatusEvent> existingEventsForPerson,
            IEnumerable<StatusType> statusTypes)
        {
            var results = new List<ConflictResult>();

            if (candidateEvent == null)
            {
                results.Add(new ConflictResult
                {
                    Severity = ConflictSeverity.Error,
                    Code = "NULL_EVENT",
                    Message = "Το γεγονός κατάστασης δεν μπορεί να είναι κενό."
                });
                return results;
            }

            // 1. Έλεγχος Ημερομηνιών
            if (candidateEvent.EndAtExclusive <= candidateEvent.StartAt)
            {
                results.Add(new ConflictResult
                {
                    Severity = ConflictSeverity.Error,
                    Code = "INVALID_DATE_RANGE",
                    Message = "Η ημερομηνία επιστροφής/λήξης πρέπει να είναι μεταγενέστερη της ημερομηνίας έναρξης.",
                    TargetEntityId = candidateEvent.Id
                });
            }

            // 2. Έλεγχος Ένταξης στη Δύναμη
            if (person != null)
            {
                if (candidateEvent.StartAt < person.StrengthStartDate)
                {
                    results.Add(new ConflictResult
                    {
                        Severity = ConflictSeverity.Warning,
                        Code = "BEFORE_STRENGTH_START",
                        Message = $"Η έναρξη της μεταβολής είναι πριν την ημερομηνία ένταξης στη δύναμη ({person.StrengthStartDate:dd/MM/yyyy}).",
                        TargetEntityId = candidateEvent.Id
                    });
                }

                if (person.StrengthEndDate.HasValue && candidateEvent.StartAt >= person.StrengthEndDate.Value)
                {
                    results.Add(new ConflictResult
                    {
                        Severity = ConflictSeverity.Error,
                        Code = "AFTER_STRENGTH_END",
                        Message = $"Η μεταβολή ξεκινά μετά την ημερομηνία διαγραφής από τη δύναμη ({person.StrengthEndDate.Value:dd/MM/yyyy}).",
                        TargetEntityId = candidateEvent.Id
                    });
                }
            }

            // 3. Έλεγχος Αμοιβαία Αποκλειόμενων Επικαλύψεων (Overlaps)
            var sTypeDict = statusTypes?.ToDictionary(st => st.Id) ?? new Dictionary<Guid, StatusType>();
            sTypeDict.TryGetValue(candidateEvent.StatusTypeId, out var candidateType);

            if (existingEventsForPerson != null && candidateEvent.EndAtExclusive > candidateEvent.StartAt)
            {
                foreach (var existing in existingEventsForPerson.Where(e => !e.IsCancelled && e.Id != candidateEvent.Id))
                {
                    if (StatusIntervalMath.DoIntervalsOverlap(candidateEvent.StartAt, candidateEvent.EndAtExclusive, existing.StartAt, existing.EndAtExclusive))
                    {
                        sTypeDict.TryGetValue(existing.StatusTypeId, out var existingType);

                        bool sameGroup = candidateType != null && existingType != null &&
                                         !string.IsNullOrEmpty(candidateType.MutualExclusionGroup) &&
                                         string.Equals(candidateType.MutualExclusionGroup, existingType.MutualExclusionGroup, StringComparison.OrdinalIgnoreCase);

                        if (sameGroup)
                        {
                            results.Add(new ConflictResult
                            {
                                Severity = ConflictSeverity.Error,
                                Code = "OVERLAPPING_ABSENCE",
                                Message = $"Υπάρχει ήδη καταχωρημένη ενεργή απουσία ({existingType?.Name} {existing.StartAt:dd/MM/yyyy} - {existing.EndAtExclusive:dd/MM/yyyy}) που επικαλύπτει το επιλεγμένο διάστημα.",
                                TargetEntityId = existing.Id
                            });
                        }
                        else
                        {
                            results.Add(new ConflictResult
                            {
                                Severity = ConflictSeverity.Warning,
                                Code = "OVERLAPPING_EVENT_DIFF_GROUP",
                                Message = $"Υπάρχει ταυτόχρονη μεταβολή ({existingType?.Name}) στο ίδιο διάστημα.",
                                TargetEntityId = existing.Id
                            });
                        }
                    }
                }
            }

            return results;
        }

        public List<ConflictResult> ValidatePersonnel(Personnel candidatePerson, IEnumerable<Personnel> existingPersonnel)
        {
            var results = new List<ConflictResult>();

            if (candidatePerson == null)
            {
                results.Add(new ConflictResult { Severity = ConflictSeverity.Error, Message = "Το πρόσωπο δεν μπορεί να είναι κενό." });
                return results;
            }

            if (string.IsNullOrWhiteSpace(candidatePerson.LastName))
            {
                results.Add(new ConflictResult { Severity = ConflictSeverity.Error, Code = "EMPTY_LASTNAME", Message = "Το επώνυμο είναι υποχρεωτικό πεδίο." });
            }

            if (string.IsNullOrWhiteSpace(candidatePerson.FirstName))
            {
                results.Add(new ConflictResult { Severity = ConflictSeverity.Error, Code = "EMPTY_FIRSTNAME", Message = "Το όνομα είναι υποχρεωτικό πεδίο." });
            }

            if (candidatePerson.RankId == Guid.Empty)
            {
                results.Add(new ConflictResult { Severity = ConflictSeverity.Error, Code = "EMPTY_RANK", Message = "Ο βαθμός είναι υποχρεωτικό πεδίο." });
            }

            if (existingPersonnel != null)
            {
                // Duplicate ASM check
                if (!string.IsNullOrWhiteSpace(candidatePerson.MilitaryServiceNumber))
                {
                    var duplicateAsm = existingPersonnel.FirstOrDefault(p =>
                        p.Id != candidatePerson.Id &&
                        !string.IsNullOrWhiteSpace(p.MilitaryServiceNumber) &&
                        string.Equals(p.MilitaryServiceNumber.Trim(), candidatePerson.MilitaryServiceNumber.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (duplicateAsm != null)
                    {
                        results.Add(new ConflictResult
                        {
                            Severity = ConflictSeverity.Error,
                            Code = "DUPLICATE_ASM",
                            Message = $"Υπάρχει ήδη καταχωρημένο πρόσωπο ({duplicateAsm.FullName}) με τον ίδιο ΑΣΜ: '{candidatePerson.MilitaryServiceNumber}'.",
                            TargetEntityId = duplicateAsm.Id
                        });
                    }
                }

                // Probable duplicate check (Same name + same rank)
                var probableDuplicate = existingPersonnel.FirstOrDefault(p =>
                    p.Id != candidatePerson.Id &&
                    p.RankId == candidatePerson.RankId &&
                    string.Equals(p.LastName?.Trim(), candidatePerson.LastName?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.FirstName?.Trim(), candidatePerson.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (probableDuplicate != null)
                {
                    results.Add(new ConflictResult
                    {
                        Severity = ConflictSeverity.Warning,
                        Code = "PROBABLE_DUPLICATE_NAME",
                        Message = $"Πιθανή διπλοεγγραφή: Υπάρχει ήδη πρόσωπο με το ίδιο όνομα και βαθμό ({probableDuplicate.FullName}).",
                        TargetEntityId = probableDuplicate.Id
                    });
                }
            }

            return results;
        }

        public List<ConflictResult> ValidateServiceAssignment(
            ServiceAssignment assignment,
            Personnel person,
            IEnumerable<ServiceAssignment> existingAssignmentsForPerson,
            IEnumerable<StatusEvent> statusEventsForPerson)
        {
            var results = new List<ConflictResult>();

            if (assignment == null)
            {
                results.Add(new ConflictResult { Severity = ConflictSeverity.Error, Message = "Η ανάθεση υπηρεσίας δεν μπορεί να είναι κενή." });
                return results;
            }

            if (assignment.EndDateTime <= assignment.StartDateTime)
            {
                results.Add(new ConflictResult
                {
                    Severity = ConflictSeverity.Error,
                    Code = "INVALID_SERVICE_TIME",
                    Message = "Η ώρα λήξης της υπηρεσίας πρέπει να είναι μετά την ώρα έναρξης."
                });
            }

            // Check person lifecycle
            if (person != null)
            {
                if (assignment.ServiceDate < person.StrengthStartDate || (person.StrengthEndDate.HasValue && assignment.ServiceDate >= person.StrengthEndDate.Value))
                {
                    results.Add(new ConflictResult
                    {
                        Severity = ConflictSeverity.Error,
                        Code = "SERVICE_OUTSIDE_STRENGTH",
                        Message = $"Η ημερομηνία υπηρεσίας ({assignment.ServiceDate:dd/MM/yyyy}) είναι εκτός του διαστήματος ένταξης του προσώπου στη δύναμη."
                    });
                }
            }

            // Check overlapping services for same person
            if (existingAssignmentsForPerson != null)
            {
                foreach (var existing in existingAssignmentsForPerson.Where(s => !s.IsCancelled && s.Id != assignment.Id))
                {
                    if (StatusIntervalMath.DoIntervalsOverlap(assignment.StartDateTime, assignment.EndDateTime, existing.StartDateTime, existing.EndDateTime))
                    {
                        results.Add(new ConflictResult
                        {
                            Severity = ConflictSeverity.Error,
                            Code = "OVERLAPPING_SERVICE",
                            Message = $"Το στέλεχος/οπλίτης έχει ήδη ανατεθειμένη υπηρεσία ({existing.StartDateTime:HH:mm} - {existing.EndDateTime:HH:mm}) κατά το ίδιο χρονικό διάστημα."
                        });
                    }
                }
            }

            // Check if person has an active absence on the service date
            if (statusEventsForPerson != null)
            {
                var activeAbsence = statusEventsForPerson.FirstOrDefault(e => !e.IsCancelled && StatusIntervalMath.IsActiveAt(e.StartAt, e.EndAtExclusive, assignment.StartDateTime));
                if (activeAbsence != null)
                {
                    results.Add(new ConflictResult
                    {
                        Severity = ConflictSeverity.Warning,
                        Code = "SERVICE_DURING_ABSENCE",
                        Message = $"Προσοχή: Το στέλεχος/οπλίτης έχει καταχωρημένη απουσία ({activeAbsence.StartAt:dd/MM} - {activeAbsence.EndAtExclusive:dd/MM}) κατά την ημερομηνία της υπηρεσίας."
                    });
                }
            }

            return results;
        }
    }
}
