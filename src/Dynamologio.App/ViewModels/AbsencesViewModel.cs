using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.App.Controls;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Infrastructure.Services;

namespace Dynamologio.App.ViewModels
{
    public class AbsencePresentationItem
    {
        public StatusEvent Event { get; set; }
        public Personnel Person { get; set; }
        public Rank Rank { get; set; }
        public OrganisationUnit Unit { get; set; }
        public StatusType StatusType { get; set; }

        public string PersonFullName => Person != null ? $"{Rank?.ShortName} {Person.FullName}".Trim() : "-";
        public string UnitName => Unit?.Name ?? "-";
        public string ReasonName => StatusType?.Name ?? "Απουσία";
        public string StartDisplay => Event?.StartAt.ToString("dd/MM/yyyy") ?? "-";
        public string ReturnDisplay => Event?.EndAtExclusive.ToString("dd/MM/yyyy") ?? "-";
        public string LastAbsentDayDisplay => Event?.EndAtExclusive.AddDays(-1).ToString("dd/MM/yyyy") ?? "-";
        public int DurationDays => Event != null ? StatusIntervalMath.CalculateDays(Event.StartAt, Event.EndAtExclusive) : 0;
        public string ReferenceDoc => Event?.ReferenceDocument ?? "-";
        public string Comment => Event?.Comment ?? "";
    }

    public class AbsencesViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IUnitOfWork _uow;
        private readonly IStatusEngine _statusEngine;
        private readonly IConflictEngine _conflictEngine;
        private readonly IAbsenceService _absenceService;
        private readonly IClock _clock;

        public ObservableCollection<AbsencePresentationItem> ActiveEventsList { get; } = new ObservableCollection<AbsencePresentationItem>();
        public ObservableCollection<AbsencePresentationItem> PlannedEventsList { get; } = new ObservableCollection<AbsencePresentationItem>();
        public ObservableCollection<AbsencePresentationItem> PastEventsList { get; } = new ObservableCollection<AbsencePresentationItem>();

        public ObservableCollection<StatusType> StatusTypesList { get; } = new ObservableCollection<StatusType>();
        public ObservableCollection<Personnel> PersonnelList { get; } = new ObservableCollection<Personnel>();
        public ObservableCollection<PersonPickerItem> SearchablePersonnelList { get; } = new ObservableCollection<PersonPickerItem>();

        // Drawer / Modal State
        private bool _isCreateDrawerOpen;
        public bool IsCreateDrawerOpen
        {
            get => _isCreateDrawerOpen;
            set => SetProperty(ref _isCreateDrawerOpen, value);
        }

        // Form Fields
        private Personnel _selectedPerson;
        private StatusType _selectedStatusType;
        private DateTime _startDate;
        private DateTime _returnDate;
        private string _referenceDoc = string.Empty;
        private string _comment = string.Empty;
        private string _inlineErrorMessage = string.Empty;
        private string _inlineSuccessMessage = string.Empty;

        public Personnel SelectedPerson
        {
            get => _selectedPerson;
            set
            {
                if (SetProperty(ref _selectedPerson, value))
                {
                    ValidateForm();
                }
            }
        }

        public StatusType SelectedStatusType
        {
            get => _selectedStatusType;
            set
            {
                if (SetProperty(ref _selectedStatusType, value))
                {
                    ValidateForm();
                }
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    ValidateForm();
                    OnPropertyChanged(nameof(ComputedSummaryText));
                }
            }
        }

        public DateTime ReturnDate
        {
            get => _returnDate;
            set
            {
                if (SetProperty(ref _returnDate, value))
                {
                    ValidateForm();
                    OnPropertyChanged(nameof(ComputedSummaryText));
                }
            }
        }

        public string ReferenceDoc { get => _referenceDoc; set => SetProperty(ref _referenceDoc, value); }
        public string Comment { get => _comment; set => SetProperty(ref _comment, value); }

        public string InlineErrorMessage { get => _inlineErrorMessage; set => SetProperty(ref _inlineErrorMessage, value); }
        public string InlineSuccessMessage { get => _inlineSuccessMessage; set => SetProperty(ref _inlineSuccessMessage, value); }

        public string ComputedSummaryText
        {
            get
            {
                if (ReturnDate <= StartDate)
                {
                    return "Η ημερομηνία επιστροφής πρέπει να είναι μετά την ημερομηνία έναρξης.";
                }
                int days = StatusIntervalMath.CalculateDays(StartDate, ReturnDate);
                var lastDay = ReturnDate.AddDays(-1);
                return $"Διάρκεια: {days} {(days == 1 ? "ημέρα" : "ημέρες")} | Τελευταία ημέρα απουσίας: {lastDay:dd/MM/yyyy} | Επιστροφή σε Παρών: {ReturnDate:dd/MM/yyyy}";
            }
        }

        public ICommand OpenCreateDrawerCommand { get; }
        public ICommand CloseCreateDrawerCommand { get; }
        public ICommand SaveAbsenceCommand { get; }
        public ICommand CancelAbsenceCommand { get; }

        public AbsencesViewModel(
            IUnitOfWork uow,
            IStatusEngine statusEngine,
            IConflictEngine conflictEngine,
            IAbsenceService absenceService,
            IClock clock)
        {
            _uow = uow;
            _statusEngine = statusEngine;
            _conflictEngine = conflictEngine;
            _absenceService = absenceService;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            _startDate = _clock.Today;
            _returnDate = _clock.Today.AddDays(5);

            OpenCreateDrawerCommand = new RelayCommand(_ =>
            {
                IsCreateDrawerOpen = true;
                InlineErrorMessage = string.Empty;
                InlineSuccessMessage = string.Empty;
            });

            CloseCreateDrawerCommand = new RelayCommand(_ =>
            {
                IsCreateDrawerOpen = false;
            });

            SaveAbsenceCommand = new RelayCommand(SaveAbsence);
            CancelAbsenceCommand = new RelayCommand(param => CancelAbsence(param as AbsencePresentationItem));
        }

        public void Activate()
        {
            LoadData();
        }

        public void LoadData()
        {
            var ranks = _uow.Ranks.GetAll().ToDictionary(r => r.Id);
            var units = _uow.OrganisationUnits.GetAll().ToDictionary(u => u.Id);
            var persons = _uow.Personnel.GetAll().ToDictionary(p => p.Id);
            var stTypes = _uow.StatusTypes.GetAll().ToDictionary(st => st.Id);

            StatusTypesList.Clear();
            foreach (var st in _uow.StatusTypes.GetAll().OrderBy(x => x.SortOrder)) StatusTypesList.Add(st);
            if (SelectedStatusType == null) SelectedStatusType = StatusTypesList.FirstOrDefault();

            PersonnelList.Clear();
            SearchablePersonnelList.Clear();
            foreach (var p in _uow.Personnel.Find(x => !x.IsArchived).OrderBy(x => x.LastName))
            {
                PersonnelList.Add(p);
                ranks.TryGetValue(p.RankId, out var rank);
                units.TryGetValue(p.OrganisationUnitId, out var unit);
                SearchablePersonnelList.Add(new PersonPickerItem
                {
                    SourceItem = p,
                    DisplayRank = rank?.ShortName ?? rank?.Name ?? "",
                    DisplayFullName = p.FullName,
                    DisplayUnit = unit?.Name ?? p.CompanyOrSection ?? "",
                    DisplayAsm = p.MilitaryServiceNumber ?? "",
                    Specialty = p.Specialty ?? ""
                });
            }
            if (SelectedPerson == null) SelectedPerson = PersonnelList.FirstOrDefault();

            var now = _clock.Now;

            ActiveEventsList.Clear();
            PlannedEventsList.Clear();
            PastEventsList.Clear();

            var allEvents = _uow.StatusEvents.Find(x => !x.IsCancelled).OrderByDescending(x => x.StartAt).ToList();
            foreach (var ev in allEvents)
            {
                persons.TryGetValue(ev.PersonnelId, out var person);
                Rank rank2 = null;
                OrganisationUnit unit2 = null;
                if (person != null)
                {
                    ranks.TryGetValue(person.RankId, out rank2);
                    units.TryGetValue(person.OrganisationUnitId, out unit2);
                }
                stTypes.TryGetValue(ev.StatusTypeId, out var sType);

                var item = new AbsencePresentationItem
                {
                    Event = ev,
                    Person = person,
                    Rank = rank2,
                    Unit = unit2,
                    StatusType = sType
                };

                if (StatusIntervalMath.IsActiveAt(ev.StartAt, ev.EndAtExclusive, now))
                {
                    ActiveEventsList.Add(item);
                }
                else if (ev.StartAt > now)
                {
                    PlannedEventsList.Add(item);
                }
                else
                {
                    PastEventsList.Add(item);
                }
            }

            ValidateForm();
        }

        private void ValidateForm()
        {
            InlineErrorMessage = string.Empty;

            if (SelectedPerson == null)
            {
                InlineErrorMessage = "Παρακαλώ επιλέξτε στέλεχος/οπλίτη.";
                return;
            }

            if (SelectedStatusType == null)
            {
                InlineErrorMessage = "Παρακαλώ επιλέξτε είδος απουσίας.";
                return;
            }

            if (ReturnDate <= StartDate)
            {
                InlineErrorMessage = "Η ημερομηνία επιστροφής πρέπει να είναι μεταγενέστερη της έναρξης.";
                return;
            }

            StatusIntervalMath.CreateDayInterval(StartDate, ReturnDate, out var startAt, out var endExclusive);
            var tempEvent = new StatusEvent
            {
                PersonnelId = SelectedPerson.Id,
                StatusTypeId = SelectedStatusType.Id,
                StartAt = startAt,
                EndAtExclusive = endExclusive
            };

            var existingEvents = _uow.StatusEvents.Find(e => e.PersonnelId == SelectedPerson.Id);
            var conflicts = _conflictEngine.ValidateStatusEvent(tempEvent, SelectedPerson, existingEvents, StatusTypesList);

            var error = conflicts.FirstOrDefault(c => c.Severity == ConflictSeverity.Error);
            if (error != null)
            {
                InlineErrorMessage = error.Message;
            }
        }

        private void SaveAbsence()
        {
            InlineSuccessMessage = string.Empty;
            ValidateForm();

            if (!string.IsNullOrEmpty(InlineErrorMessage))
            {
                return;
            }

            StatusIntervalMath.CreateDayInterval(StartDate, ReturnDate, out var startAt, out var endExclusive);

            var newEvent = new StatusEvent
            {
                PersonnelId = SelectedPerson.Id,
                StatusTypeId = SelectedStatusType.Id,
                StartAt = startAt,
                EndAtExclusive = endExclusive,
                ReferenceDocument = (ReferenceDoc ?? "").Trim(),
                Comment = (Comment ?? "").Trim()
            };

            _absenceService.CreateAbsence(
                newEvent,
                $"Καταχώρηση {SelectedStatusType.Name} για {SelectedPerson.FullName} ({startAt:dd/MM/yyyy} -> {endExclusive:dd/MM/yyyy})");

            InlineSuccessMessage = $"Επιτυχής καταχώρηση: {SelectedPerson.FullName} [{SelectedStatusType.Name}] έως {endExclusive:dd/MM/yyyy}.";

            ReferenceDoc = string.Empty;
            Comment = string.Empty;
            IsCreateDrawerOpen = false;
            LoadData();
        }

        private void CancelAbsence(AbsencePresentationItem item)
        {
            if (item?.Event == null) return;

            // Will be moved to IConfirmationService in Commit 2
            var res = System.Windows.MessageBox.Show(
                $"Θα ακυρωθεί η απουσία:\n{item.PersonFullName} - {item.ReasonName} ({item.StartDisplay} - {item.ReturnDisplay})\n\nΣυνέχεια;",
                "Επιβεβαίωση Ακύρωσης",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (res == System.Windows.MessageBoxResult.Yes)
            {
                _absenceService.CancelAbsence(item.Event.Id, $"Ακύρωση απουσίας {item.PersonFullName}");
                LoadData();
            }
        }
    }
}
