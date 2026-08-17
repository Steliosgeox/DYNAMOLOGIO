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
                if (person != null)
                {
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
                }
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
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
                if (ev != null)
                {
                    var oldEv = new StatusEvent { Id = ev.Id, IsCancelled = ev.IsCancelled, CancellationReason = ev.CancellationReason };
                    ev.IsCancelled = true;
                    ev.CancelledAt = DateTime.Now;
                    ev.CancellationReason = reason ?? "Ακύρωση από χρήστη";
                    ev.CancelledBy = Environment.UserName;
                    _uow.StatusEvents.Update(ev);
                    _audit.LogAction(AuditAction.Cancel, "StatusEvent", ev.Id.ToString(), reason ?? "Ακύρωση απουσίας", oldEv, ev);
                }
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
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
                if (assignment != null)
                {
                    var oldAssignment = new ServiceAssignment { Id = assignment.Id, IsCancelled = assignment.IsCancelled };
                    assignment.IsCancelled = true;
                    assignment.CancellationReason = reason ?? "Ακύρωση υπηρεσίας";
                    _uow.ServiceAssignments.Update(assignment);
                    _audit.LogAction(AuditAction.Cancel, "ServiceAssignment", assignment.Id.ToString(), reason ?? "Ακύρωση υπηρεσίας", oldAssignment, assignment);
                }
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }
    }
}
