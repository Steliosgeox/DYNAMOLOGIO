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
        private readonly IAuditEventPublisher _audit;
        private readonly ITransactionRunner _tx;
        private readonly IClock _clock;
        private readonly ICurrentActor _actor;

        public PersonnelService(IUnitOfWork uow, IAuditEventPublisher audit, ITransactionRunner tx, IClock clock, ICurrentActor actor)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _tx = tx ?? throw new ArgumentNullException(nameof(tx));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }

        public void CreatePerson(Personnel person, string reason)
        {
            if (person == null) throw new ArgumentNullException(nameof(person));
            ValidatePersonnel(person);

            _tx.RunInTransaction(() =>
            {
                person.CreatedAt = _clock.Now;
                person.CreatedBy = _actor.GetActor();
                person.ModifiedAt = _clock.Now;
                person.ModifiedBy = _actor.GetActor();

                _uow.Personnel.Insert(person);
                _audit.Publish(AuditAction.Create, "Personnel", person.Id.ToString(), reason ?? "Δημιουργία προσώπου", null, person);
            });
        }

        public void UpdatePerson(Personnel person, string reason, Personnel oldPerson = null)
        {
            if (person == null) throw new ArgumentNullException(nameof(person));
            ValidatePersonnel(person, isUpdate: true);

            var existing = _uow.Personnel.GetById(person.Id);
            if (existing == null) throw new InvalidOperationException($"Personnel {person.Id} not found.");

            _tx.RunInTransaction(() =>
            {
                person.ModifiedAt = _clock.Now;
                person.ModifiedBy = _actor.GetActor();
                
                _uow.Personnel.Update(person);
                _audit.Publish(AuditAction.Update, "Personnel", person.Id.ToString(), reason ?? "Ενημέρωση στοιχείων προσώπου", oldPerson, person);
            });
        }

        public void ArchivePerson(Guid id, string reason)
        {
            _tx.RunInTransaction(() =>
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
                person.StrengthEndDate = _clock.Today;
                person.ModifiedAt = _clock.Now;
                person.ModifiedBy = _actor.GetActor();

                _uow.Personnel.Update(person);
                _audit.Publish(AuditAction.Archive, "Personnel", person.Id.ToString(), reason ?? "Αρχειοθέτηση προσώπου", oldPerson, person);
            });
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
        private readonly IAuditEventPublisher _audit;
        private readonly ITransactionRunner _tx;
        private readonly IClock _clock;
        private readonly ICurrentActor _actor;

        public AbsenceService(IUnitOfWork uow, IAuditEventPublisher audit, ITransactionRunner tx, IClock clock, ICurrentActor actor)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _tx = tx ?? throw new ArgumentNullException(nameof(tx));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }

        public void CreateAbsence(StatusEvent ev, string reason)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            ValidateAbsence(ev);

            _tx.RunInTransaction(() =>
            {
                ev.CreatedAt = _clock.Now;
                ev.CreatedBy = _actor.GetActor();
                ev.ModifiedAt = _clock.Now;
                ev.ModifiedBy = _actor.GetActor();

                _uow.StatusEvents.Insert(ev);
                _audit.Publish(AuditAction.Create, "StatusEvent", ev.Id.ToString(), reason ?? "Καταχώρηση απουσίας/άδειας", null, ev);
            });
        }

        public void CancelAbsence(Guid eventId, string reason)
        {
            _tx.RunInTransaction(() =>
            {
                var ev = _uow.StatusEvents.GetById(eventId);
                if (ev == null) throw new InvalidOperationException($"StatusEvent {eventId} not found.");

                var oldEv = new StatusEvent { Id = ev.Id, IsCancelled = ev.IsCancelled, CancellationReason = ev.CancellationReason };
                ev.IsCancelled = true;
                ev.CancelledAt = _clock.Now;
                ev.CancellationReason = reason ?? "Ακύρωση από χρήστη";
                ev.CancelledBy = _actor.GetActor();
                ev.ModifiedAt = _clock.Now;
                ev.ModifiedBy = _actor.GetActor();

                _uow.StatusEvents.Update(ev);
                _audit.Publish(AuditAction.Cancel, "StatusEvent", ev.Id.ToString(), reason ?? "Ακύρωση απουσίας", oldEv, ev);
            });
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
        private readonly IAuditEventPublisher _audit;
        private readonly ITransactionRunner _tx;
        private readonly IClock _clock;
        private readonly ICurrentActor _actor;

        public DutyService(IUnitOfWork uow, IAuditEventPublisher audit, ITransactionRunner tx, IClock clock, ICurrentActor actor)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _tx = tx ?? throw new ArgumentNullException(nameof(tx));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }

        public void AssignDuty(ServiceAssignment assignment, string reason)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            ValidateDuty(assignment);

            _tx.RunInTransaction(() =>
            {
                assignment.CreatedAt = _clock.Now;
                assignment.CreatedBy = _actor.GetActor();
                assignment.ModifiedAt = _clock.Now;
                assignment.ModifiedBy = _actor.GetActor();

                _uow.ServiceAssignments.Insert(assignment);
                _audit.Publish(AuditAction.Create, "ServiceAssignment", assignment.Id.ToString(), reason ?? "Ανάθεση υπηρεσίας", null, assignment);
            });
        }

        public void CancelDuty(Guid assignmentId, string reason)
        {
            _tx.RunInTransaction(() =>
            {
                var assignment = _uow.ServiceAssignments.GetById(assignmentId);
                if (assignment == null) throw new InvalidOperationException($"ServiceAssignment {assignmentId} not found.");

                var oldAssignment = new ServiceAssignment { Id = assignment.Id, IsCancelled = assignment.IsCancelled };
                assignment.IsCancelled = true;
                assignment.CancellationReason = reason ?? "Ακύρωση υπηρεσίας";
                assignment.ModifiedAt = _clock.Now;
                assignment.ModifiedBy = _actor.GetActor();

                _uow.ServiceAssignments.Update(assignment);
                _audit.Publish(AuditAction.Cancel, "ServiceAssignment", assignment.Id.ToString(), reason ?? "Ακύρωση υπηρεσίας", oldAssignment, assignment);
            });
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
