using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Infrastructure.Services;
using Dynamologio.App.Services;

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

    public class ServicesViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IPersonnelQueryService _personnelQueryService;
        private readonly IServiceRosterQueryService _serviceQueryService;
        private readonly IAbsenceQueryService _absenceQueryService;
        private readonly IConflictEngine _conflictEngine;
        private readonly IDutyService _dutyService;
        private readonly IClock _clock;
        private readonly INotificationService _notificationService;
        private readonly IConfirmationService _confirmationService;

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
            IPersonnelQueryService personnelQueryService,
            IServiceRosterQueryService serviceQueryService,
            IAbsenceQueryService absenceQueryService,
            IConflictEngine conflictEngine,
            IDutyService dutyService,
            IClock clock,
            INotificationService notificationService,
            IConfirmationService confirmationService)
        {
            _personnelQueryService = personnelQueryService ?? throw new ArgumentNullException(nameof(personnelQueryService));
            _serviceQueryService = serviceQueryService ?? throw new ArgumentNullException(nameof(serviceQueryService));
            _absenceQueryService = absenceQueryService ?? throw new ArgumentNullException(nameof(absenceQueryService));
            _conflictEngine = conflictEngine;
            _dutyService = dutyService;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));

            _selectedDate = _clock.Today;

            SaveServiceCommand = new RelayCommand(SaveService);
            CancelServiceCommand = new RelayCommand(param => CancelService(param as ServicePresentationItem));
            SetTodayCommand = new RelayCommand(() => SelectedDate = _clock.Today);
            SetTomorrowCommand = new RelayCommand(() => SelectedDate = _clock.Today.AddDays(1));
        }

        public void Activate()
        {
            LoadData();
        }

        public void LoadData()
        {
            PersonnelList.Clear();
            foreach (var p in _personnelQueryService.GetActivePersonnel().OrderBy(x => x.LastName)) PersonnelList.Add(p);
            if (SelectedPerson == null) SelectedPerson = PersonnelList.FirstOrDefault();

            ServiceTypesList.Clear();
            foreach (var st in _serviceQueryService.GetServiceTypes().OrderBy(x => x.SortOrder)) ServiceTypesList.Add(st);
            if (SelectedServiceType == null) SelectedServiceType = ServiceTypesList.FirstOrDefault();

            var ranks = _personnelQueryService.GetAllRanks().ToDictionary(r => r.Id);
            var persons = _personnelQueryService.GetActivePersonnel().ToDictionary(p => p.Id);
            var stTypes = _serviceQueryService.GetServiceTypes().ToDictionary(st => st.Id);

            DailyServicesList.Clear();
            var assignments = _serviceQueryService.GetServicesForDate(SelectedDate).Where(s => !s.IsCancelled).OrderBy(s => s.StartDateTime);

            foreach (var item in assignments)
            {
                if (!persons.TryGetValue(item.PersonnelId, out var person))
                {
                    person = _personnelQueryService.GetPerson(item.PersonnelId);
                }
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

            var existingServices = _personnelQueryService.GetServiceHistory(SelectedPerson.Id);
            var existingAbsences = _absenceQueryService.GetEventsForPerson(SelectedPerson.Id);
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
                var proceed = _confirmationService.Confirm("Προειδοποίηση Σύγκρουσης", $"{warning.Message}\n\nΕπιθυμείτε να συνεχίσετε με την καταχώρηση;");
                if (!proceed) return;
            }

            _dutyService.AssignDuty(assignment, $"Ανάθεση υπηρεσίας {SelectedServiceType.Name} σε {SelectedPerson.FullName} για {SelectedDate:dd/MM/yyyy}");

            Location = string.Empty;
            Notes = string.Empty;
            LoadData();
        }

        private void CancelService(ServicePresentationItem item)
        {
            if (item?.Assignment == null) return;

            var res = _confirmationService.Confirm(
                "Επιβεβαίωση Ακύρωσης",
                $"Θα ακυρωθεί η υπηρεσία:\n{item.RankName} {item.PersonFullName} - {item.ServiceName} ({item.HoursDisplay})\n\nΣυνέχεια;");

            if (res)
            {
                _dutyService.CancelDuty(item.Assignment.Id, $"Ακύρωση υπηρεσίας {item.PersonFullName}");
                LoadData();
            }
        }
    }
}
