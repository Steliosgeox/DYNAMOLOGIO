using System;
using System.Collections.Generic;
using System.Linq;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;

namespace Dynamologio.Infrastructure.Services
{
    public interface IPersonnelService
    {
        void CreatePerson(Personnel person, string reason);
        void UpdatePerson(Personnel person, string reason, Personnel oldPerson = null);
        void ArchivePerson(Guid id, string reason);
    }

    public class PersonnelService : IPersonnelService
    {
        private readonly IUnitOfWork _uow;
        private readonly IAuditService _audit;

        public PersonnelService(IUnitOfWork uow, IAuditService audit)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public void CreatePerson(Personnel person, string reason)
        {
            if (person == null) throw new ArgumentNullException(nameof(person));
            ValidatePersonnel(person);

            _uow.BeginTransaction();
            try
            {
                _uow.Personnel.Insert(person);
                _audit.LogAction(AuditAction.Create, "Personnel", person.Id.ToString(), reason ?? "Δημιουργία προσώπου", null, person);
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        public void UpdatePerson(Personnel person, string reason, Personnel oldPerson = null)
        {
            if (person == null) throw new ArgumentNullException(nameof(person));
            ValidatePersonnel(person, isUpdate: true);

            var existing = _uow.Personnel.GetById(person.Id);
            if (existing == null) throw new InvalidOperationException($"Personnel {person.Id} not found.");

            _uow.BeginTransaction();
            try
            {
                _uow.Personnel.Update(person);
                _audit.LogAction(AuditAction.Update, "Personnel", person.Id.ToString(), reason ?? "Ενημέρωση στοιχείων προσώπου", oldPerson, person);
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        public void ArchivePerson(Guid id, string reason)
        {
            _uow.BeginTransaction();
            try
            {
                var person = _uow.Personnel.GetById(id);
                if (person == null) throw new InvalidOperationException($"Personnel {id} not found.");

                var oldPerson = new Personnel
                {
                    Id = person.Id,
                    LastName = person.LastName,
                    FirstName = person.FirstName,
                    IsArchived = person.IsArchived,
                    StrengthEndDate = person.StrengthEndDate
                };

                person.IsArchived = true;
                person.StrengthEndDate = DateTime.Today;
                _uow.Personnel.Update(person);
                _audit.LogAction(AuditAction.Archive, "Personnel", person.Id.ToString(), reason ?? "Αρχειοθέτηση προσώπου", oldPerson, person);
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        private void ValidatePersonnel(Personnel person, bool isUpdate = false)
        {
            if (string.IsNullOrWhiteSpace(person.LastName)) throw new ArgumentException("Το επώνυμο είναι υποχρεωτικό.");
            if (string.IsNullOrWhiteSpace(person.FirstName)) throw new ArgumentException("Το όνομα είναι υποχρεωτικό.");
            if (string.IsNullOrWhiteSpace(person.MilitaryServiceNumber)) throw new ArgumentException("Ο ΑΣΜ είναι υποχρεωτικός.");
            if (person.RankId == Guid.Empty) throw new ArgumentException("Ο βαθμός είναι υποχρεωτικός.");
            if (person.OrganisationUnitId == Guid.Empty) throw new ArgumentException("Η μονάδα/λόχος είναι υποχρεωτική.");

            var rank = _uow.Ranks.GetById(person.RankId) ?? throw new ArgumentException($"Ο βαθμός {person.RankId} δεν βρέθηκε.");
            var unit = _uow.OrganisationUnits.GetById(person.OrganisationUnitId) ?? throw new ArgumentException($"Η μονάδα {person.OrganisationUnitId} δεν βρέθηκε.");

            if (person.Category != rank.Category)
            {
                throw new ArgumentException($"Αναντιστοιχία κατηγορίας προσωπικού. Ο βαθμός {rank.Name} ανήκει στην κατηγορία {rank.Category}.");
            }

            var duplicate = _uow.Personnel.Find(p => p.MilitaryServiceNumber == person.MilitaryServiceNumber && p.Id != person.Id).FirstOrDefault();
            if (duplicate != null)
            {
                throw new InvalidOperationException($"Υπάρχει ήδη στέλεχος/οπλίτης με ΑΣΜ {person.MilitaryServiceNumber}.");
            }
        }
    }

    public interface IAbsenceService
    {
        void CreateAbsence(StatusEvent ev, string reason);
        void CancelAbsence(Guid eventId, string reason);
    }

    public class AbsenceService : IAbsenceService
    {
        private readonly IUnitOfWork _uow;
        private readonly IAuditService _audit;

        public AbsenceService(IUnitOfWork uow, IAuditService audit)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public void CreateAbsence(StatusEvent ev, string reason)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            ValidateAbsence(ev);

            _uow.BeginTransaction();
            try
            {
                _uow.StatusEvents.Insert(ev);
                _audit.LogAction(AuditAction.Create, "StatusEvent", ev.Id.ToString(), reason ?? "Καταχώρηση απουσίας/άδειας", null, ev);
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        public void CancelAbsence(Guid eventId, string reason)
        {
            _uow.BeginTransaction();
            try
            {
                var ev = _uow.StatusEvents.GetById(eventId);
                if (ev == null) throw new InvalidOperationException($"StatusEvent {eventId} not found.");

                var oldEv = new StatusEvent { Id = ev.Id, IsCancelled = ev.IsCancelled, CancellationReason = ev.CancellationReason };
                ev.IsCancelled = true;
                ev.CancelledAt = DateTime.Now;
                ev.CancellationReason = reason ?? "Ακύρωση από χρήστη";
                ev.CancelledBy = Environment.UserName;
                _uow.StatusEvents.Update(ev);
                _audit.LogAction(AuditAction.Cancel, "StatusEvent", ev.Id.ToString(), reason ?? "Ακύρωση απουσίας", oldEv, ev);
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        private void ValidateAbsence(StatusEvent ev)
        {
            if (ev.PersonnelId == Guid.Empty) throw new ArgumentException("Το πρόσωπο είναι υποχρεωτικό.");
            if (ev.StatusTypeId == Guid.Empty) throw new ArgumentException("Ο τύπος απουσίας είναι υποχρεωτικός.");
            if (ev.StartAt >= ev.EndAtExclusive) throw new ArgumentException("Η ημερομηνία έναρξης πρέπει να προηγείται της λήξης.");

            var person = _uow.Personnel.GetById(ev.PersonnelId) ?? throw new ArgumentException($"Το πρόσωπο {ev.PersonnelId} δεν βρέθηκε.");
            var statusType = _uow.StatusTypes.GetById(ev.StatusTypeId) ?? throw new ArgumentException($"Ο τύπος απουσίας {ev.StatusTypeId} δεν βρέθηκε.");

            var overlaps = _uow.StatusEvents.Find(e =>
                e.PersonnelId == ev.PersonnelId &&
                !e.IsCancelled &&
                e.Id != ev.Id &&
                e.StartAt < ev.EndAtExclusive &&
                e.EndAtExclusive > ev.StartAt).ToList();

            if (overlaps.Any())
            {
                throw new InvalidOperationException("Υπάρχει επικάλυψη με άλλη καταχωρημένη απουσία/άδεια.");
            }
        }
    }

    public interface IDutyService
    {
        void AssignDuty(ServiceAssignment assignment, string reason);
        void CancelDuty(Guid assignmentId, string reason);
    }

    public class DutyService : IDutyService
    {
        private readonly IUnitOfWork _uow;
        private readonly IAuditService _audit;

        public DutyService(IUnitOfWork uow, IAuditService audit)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public void AssignDuty(ServiceAssignment assignment, string reason)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            ValidateDuty(assignment);

            _uow.BeginTransaction();
            try
            {
                _uow.ServiceAssignments.Insert(assignment);
                _audit.LogAction(AuditAction.Create, "ServiceAssignment", assignment.Id.ToString(), reason ?? "Ανάθεση υπηρεσίας", null, assignment);
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        public void CancelDuty(Guid assignmentId, string reason)
        {
            _uow.BeginTransaction();
            try
            {
                var assignment = _uow.ServiceAssignments.GetById(assignmentId);
                if (assignment == null) throw new InvalidOperationException($"ServiceAssignment {assignmentId} not found.");

                var oldAssignment = new ServiceAssignment { Id = assignment.Id, IsCancelled = assignment.IsCancelled };
                assignment.IsCancelled = true;
                assignment.CancellationReason = reason ?? "Ακύρωση υπηρεσίας";
                _uow.ServiceAssignments.Update(assignment);
                _audit.LogAction(AuditAction.Cancel, "ServiceAssignment", assignment.Id.ToString(), reason ?? "Ακύρωση υπηρεσίας", oldAssignment, assignment);
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        private void ValidateDuty(ServiceAssignment assignment)
        {
            if (assignment.PersonnelId == Guid.Empty) throw new ArgumentException("Το πρόσωπο είναι υποχρεωτικό.");
            if (assignment.ServiceTypeId == Guid.Empty) throw new ArgumentException("Ο τύπος υπηρεσίας είναι υποχρεωτικός.");
            if (assignment.StartDateTime >= assignment.EndDateTime) throw new ArgumentException("Η έναρξη υπηρεσίας πρέπει να προηγείται της λήξης.");

            var person = _uow.Personnel.GetById(assignment.PersonnelId) ?? throw new ArgumentException($"Το πρόσωπο {assignment.PersonnelId} δεν βρέθηκε.");
            var serviceType = _uow.ServiceTypes.GetById(assignment.ServiceTypeId) ?? throw new ArgumentException($"Ο τύπος υπηρεσίας {assignment.ServiceTypeId} δεν βρέθηκε.");

            var overlaps = _uow.ServiceAssignments.Find(s =>
                s.PersonnelId == assignment.PersonnelId &&
                !s.IsCancelled &&
                s.Id != assignment.Id &&
                s.StartDateTime < assignment.EndDateTime &&
                s.EndDateTime > assignment.StartDateTime).ToList();

            if (overlaps.Any())
            {
                throw new InvalidOperationException("Το πρόσωπο εκτελεί ήδη άλλη υπηρεσία σε αυτό το διάστημα.");
            }

            var absences = _uow.StatusEvents.Find(e =>
                e.PersonnelId == assignment.PersonnelId &&
                !e.IsCancelled &&
                e.StartAt.Date <= assignment.ServiceDate.Date &&
                e.EndAtExclusive.Date > assignment.ServiceDate.Date).ToList();

            if (absences.Any(a => _uow.StatusTypes.GetById(a.StatusTypeId)?.Effect == StatusEffect.Absent))
            {
                throw new InvalidOperationException("Το πρόσωπο απουσιάζει την ημέρα της υπηρεσίας.");
            }
        }
    }
}
