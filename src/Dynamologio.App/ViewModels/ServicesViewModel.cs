using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Infrastructure.Services;

namespace Dynamologio.App.ViewModels
{
    public class ServicePresentationItem
    {
        public ServiceAssignment Assignment { get; set; }
        public Personnel Person { get; set; }
        public Rank Rank { get; set; }
        public ServiceType ServiceType { get; set; }

        public string RankName => Rank?.ShortName ?? "-";
        public string PersonFullName => Person?.FullName ?? "-";
        public string ServiceName => ServiceType?.Name ?? "-";
        public string DateDisplay => Assignment?.ServiceDate.ToString("dd/MM/yyyy") ?? "-";
        public string HoursDisplay => Assignment != null ? $"{Assignment.StartDateTime:HH:mm} - {Assignment.EndDateTime:HH:mm}" : "-";
        public string Location => Assignment?.DutyLocation ?? "-";
        public string Notes => Assignment?.Notes ?? "-";
    }

    public class ServicesViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IConflictEngine _conflictEngine;
        private readonly IDutyService _dutyService;
        private readonly IClock _clock;
        private readonly MainViewModel _mainVM;

        public ObservableCollection<ServicePresentationItem> DailyServicesList { get; } = new ObservableCollection<ServicePresentationItem>();
        public ObservableCollection<Personnel> PersonnelList { get; } = new ObservableCollection<Personnel>();
        public ObservableCollection<ServiceType> ServiceTypesList { get; } = new ObservableCollection<ServiceType>();

        private DateTime _selectedDate;
        private Personnel _selectedPerson;
        private ServiceType _selectedServiceType;
        private string _startTime = "08:00";
        private string _endTime = "14:00";
        private string _location = string.Empty;
        private string _notes = string.Empty;
        private string _inlineErrorMessage = string.Empty;

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    LoadData();
                }
            }
        }

        public Personnel SelectedPerson { get => _selectedPerson; set => SetProperty(ref _selectedPerson, value); }
        public ServiceType SelectedServiceType { get => _selectedServiceType; set => SetProperty(ref _selectedServiceType, value); }
        public string StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }
        public string EndTime { get => _endTime; set => SetProperty(ref _endTime, value); }
        public string Location { get => _location; set => SetProperty(ref _location, value); }
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
        public string InlineErrorMessage { get => _inlineErrorMessage; set => SetProperty(ref _inlineErrorMessage, value); }

        public ICommand SaveServiceCommand { get; }
        public ICommand CancelServiceCommand { get; }
        public ICommand SetTodayCommand { get; }
        public ICommand SetTomorrowCommand { get; }

        public ServicesViewModel(
            IUnitOfWork uow,
            IConflictEngine conflictEngine,
            IDutyService dutyService,
            IClock clock,
            MainViewModel mainVM)
        {
            _uow = uow;
            _conflictEngine = conflictEngine;
            _dutyService = dutyService;
            _clock = clock ?? SystemClock.Instance;
            _mainVM = mainVM;

            _selectedDate = _clock.Today;

            SaveServiceCommand = new RelayCommand(SaveService);
            CancelServiceCommand = new RelayCommand(param => CancelService(param as ServicePresentationItem));
            SetTodayCommand = new RelayCommand(() => SelectedDate = _clock.Today);
            SetTomorrowCommand = new RelayCommand(() => SelectedDate = _clock.Today.AddDays(1));
        }

        public void LoadData()
        {
            PersonnelList.Clear();
            foreach (var p in _uow.Personnel.Find(x => !x.IsArchived).OrderBy(x => x.LastName)) PersonnelList.Add(p);
            if (SelectedPerson == null) SelectedPerson = PersonnelList.FirstOrDefault();

            ServiceTypesList.Clear();
            foreach (var st in _uow.ServiceTypes.GetAll().OrderBy(x => x.SortOrder)) ServiceTypesList.Add(st);
            if (SelectedServiceType == null) SelectedServiceType = ServiceTypesList.FirstOrDefault();

            var ranks = _uow.Ranks.GetAll().ToDictionary(r => r.Id);
            var persons = _uow.Personnel.GetAll().ToDictionary(p => p.Id);
            var stTypes = _uow.ServiceTypes.GetAll().ToDictionary(st => st.Id);

            DailyServicesList.Clear();
            var assignments = _uow.ServiceAssignments.Find(s => !s.IsCancelled && s.ServiceDate.Date == SelectedDate.Date).OrderBy(s => s.StartDateTime);

            foreach (var item in assignments)
            {
                persons.TryGetValue(item.PersonnelId, out var person);
                Rank rank = null;
                if (person != null) ranks.TryGetValue(person.RankId, out rank);
                stTypes.TryGetValue(item.ServiceTypeId, out var stType);

                DailyServicesList.Add(new ServicePresentationItem
                {
                    Assignment = item,
                    Person = person,
                    Rank = rank,
                    ServiceType = stType
                });
            }
        }

        private void SaveService()
        {
            InlineErrorMessage = string.Empty;

            if (SelectedPerson == null)
            {
                InlineErrorMessage = "Παρακαλώ επιλέξτε στέλεχος/οπλίτη.";
                return;
            }

            if (SelectedServiceType == null)
            {
                InlineErrorMessage = "Παρακαλώ επιλέξτε είδος υπηρεσίας.";
                return;
            }

            if (!TimeSpan.TryParse(StartTime, out var sTime) || !TimeSpan.TryParse(EndTime, out var eTime))
            {
                InlineErrorMessage = "Μη έγκυρη μορφή ώρας (π.χ. 08:00).";
                return;
            }

            var startDt = SelectedDate.Date.Add(sTime);
            var endDt = SelectedDate.Date.Add(eTime);
            if (endDt <= startDt)
            {
                endDt = endDt.AddDays(1); // Overnight shift
            }

            var assignment = new ServiceAssignment
            {
                PersonnelId = SelectedPerson.Id,
                ServiceTypeId = SelectedServiceType.Id,
                ServiceDate = SelectedDate.Date,
                StartDateTime = startDt,
                EndDateTime = endDt,
                DutyLocation = (Location ?? "").Trim(),
                Notes = (Notes ?? "").Trim()
            };

            var existingServices = _uow.ServiceAssignments.Find(s => s.PersonnelId == SelectedPerson.Id);
            var existingAbsences = _uow.StatusEvents.Find(e => e.PersonnelId == SelectedPerson.Id);
            var conflicts = _conflictEngine.ValidateServiceAssignment(assignment, SelectedPerson, existingServices, existingAbsences);

            var error = conflicts.FirstOrDefault(c => c.Severity == ConflictSeverity.Error);
            if (error != null)
            {
                InlineErrorMessage = error.Message;
                return;
            }

            var warning = conflicts.FirstOrDefault(c => c.Severity == ConflictSeverity.Warning);
            if (warning != null)
            {
                var proceed = MessageBox.Show($"{warning.Message}\n\nΕπιθυμείτε να συνεχίσετε με την καταχώρηση;", "Προειδοποίηση Σύγκρουσης", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes) return;
            }

            _dutyService.AssignDuty(assignment, $"Ανάθεση υπηρεσίας {SelectedServiceType.Name} σε {SelectedPerson.FullName} για {SelectedDate:dd/MM/yyyy}");

            Location = string.Empty;
            Notes = string.Empty;
            LoadData();
        }

        private void CancelService(ServicePresentationItem item)
        {
            if (item?.Assignment == null) return;

            var res = MessageBox.Show(
                $"Θα ακυρωθεί η υπηρεσία:\n{item.RankName} {item.PersonFullName} - {item.ServiceName} ({item.HoursDisplay})\n\nΣυνέχεια;",
                "Επιβεβαίωση Ακύρωσης",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                _dutyService.CancelDuty(item.Assignment.Id, $"Ακύρωση υπηρεσίας {item.PersonFullName}");
                LoadData();
            }
        }
    }
}
