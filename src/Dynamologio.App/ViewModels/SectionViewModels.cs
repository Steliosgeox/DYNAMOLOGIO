using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Dynamologio.ImportExport.Excel.Import;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Reporting.Services;
using Microsoft.Win32;

namespace Dynamologio.App.ViewModels
{
    // =========================================================================
    // 1. DASHBOARD VIEWMODEL
    // =========================================================================
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IStrengthCalculator _strengthCalculator;
        private readonly MainViewModel _mainVM;

        public UnitStrengthSnapshot Snapshot { get; private set; }

        public int TotalActive => Snapshot?.TotalActiveStrength ?? 0;
        public int TotalPresent => Snapshot?.TotalPresent ?? 0;
        public int TotalAbsent => Snapshot?.TotalAbsent ?? 0;
        public int OfficersActive => Snapshot?.OfficersAndNcosActive ?? 0;
        public int ConscriptsActive => Snapshot?.ConscriptsActive ?? 0;
        public int ReturningToday => Snapshot?.ReturningTodayCount ?? 0;
        public int ReturningTomorrow => Snapshot?.ReturningTomorrowCount ?? 0;

        public ObservableCollection<PersonnelStatusSnapshot> AbsentPersonnelList { get; } = new ObservableCollection<PersonnelStatusSnapshot>();

        public ICommand NavigateToDynamologioCommand { get; }
        public ICommand NavigateToAbsencesCommand { get; }
        public ICommand NavigateToPersonnelCommand { get; }
        public ICommand NavigateToServicesCommand { get; }

        public DashboardViewModel(IUnitOfWork uow, IStrengthCalculator strengthCalculator, MainViewModel mainVM)
        {
            _uow = uow;
            _strengthCalculator = strengthCalculator;
            _mainVM = mainVM;

            NavigateToDynamologioCommand = new RelayCommand(() => _mainVM.Navigate("Dynamologio"));
            NavigateToAbsencesCommand = new RelayCommand(() => _mainVM.Navigate("Absences"));
            NavigateToPersonnelCommand = new RelayCommand(() => _mainVM.Navigate("Personnel"));
            NavigateToServicesCommand = new RelayCommand(() => _mainVM.Navigate("Services"));
        }

        public void LoadData()
        {
            var p = _uow.Personnel.GetAll();
            var ev = _uow.StatusEvents.GetAll();
            var st = _uow.StatusTypes.GetAll();
            var rk = _uow.Ranks.GetAll();
            var un = _uow.OrganisationUnits.GetAll();
            var sa = _uow.ServiceAssignments.GetAll();
            var sv = _uow.ServiceTypes.GetAll();

            Snapshot = _strengthCalculator.CalculateSnapshot(p, ev, st, rk, un, sa, sv, DateTime.Today);

            AbsentPersonnelList.Clear();
            foreach (var a in Snapshot.AbsentPersonnel.OrderBy(x => x.Rank?.SortOrder ?? 99))
            {
                AbsentPersonnelList.Add(a);
            }

            OnPropertyChanged(nameof(TotalActive));
            OnPropertyChanged(nameof(TotalPresent));
            OnPropertyChanged(nameof(TotalAbsent));
            OnPropertyChanged(nameof(OfficersActive));
            OnPropertyChanged(nameof(ConscriptsActive));
            OnPropertyChanged(nameof(ReturningToday));
            OnPropertyChanged(nameof(ReturningTomorrow));
        }
    }

    // =========================================================================
    // 2. DYNAMOLOGIO (STRENGTH) VIEWMODEL
    // =========================================================================
    public class DynamologioViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IStrengthCalculator _strengthCalculator;
        private readonly IReportGeneratorService _reportService;
        private readonly MainViewModel _mainVM;

        private DateTime _selectedDate = DateTime.Today;
        private OrganisationUnit _selectedUnit;

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

        public OrganisationUnit SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (SetProperty(ref _selectedUnit, value))
                {
                    LoadData();
                }
            }
        }

        public ObservableCollection<OrganisationUnit> UnitsList { get; } = new ObservableCollection<OrganisationUnit>();
        public ObservableCollection<PersonnelStatusSnapshot> PresentList { get; } = new ObservableCollection<PersonnelStatusSnapshot>();
        public ObservableCollection<PersonnelStatusSnapshot> AbsentList { get; } = new ObservableCollection<PersonnelStatusSnapshot>();

        public UnitStrengthSnapshot CurrentSnapshot { get; private set; }

        public ICommand ExportExcelCommand { get; }
        public ICommand PrintReportCommand { get; }

        public DynamologioViewModel(
            IUnitOfWork uow,
            IStrengthCalculator strengthCalculator,
            IReportGeneratorService reportService,
            MainViewModel mainVM)
        {
            _uow = uow;
            _strengthCalculator = strengthCalculator;
            _reportService = reportService;
            _mainVM = mainVM;

            ExportExcelCommand = new RelayCommand(ExportExcel);
            PrintReportCommand = new RelayCommand(PrintReport);
        }

        public void LoadData()
        {
            if (UnitsList.Count == 0)
            {
                UnitsList.Add(new OrganisationUnit { Name = "Όλη η Μονάδα (Όλοι οι Λόχοι)", Id = Guid.Empty });
                foreach (var u in _uow.OrganisationUnits.GetAll().OrderBy(x => x.SortOrder))
                {
                    UnitsList.Add(u);
                }
                _selectedUnit = UnitsList.First();
            }

            Guid? filterUnitId = _selectedUnit != null && _selectedUnit.Id != Guid.Empty ? (Guid?)_selectedUnit.Id : null;

            var p = _uow.Personnel.GetAll();
            var ev = _uow.StatusEvents.GetAll();
            var st = _uow.StatusTypes.GetAll();
            var rk = _uow.Ranks.GetAll();
            var un = _uow.OrganisationUnits.GetAll();
            var sa = _uow.ServiceAssignments.GetAll();
            var sv = _uow.ServiceTypes.GetAll();

            CurrentSnapshot = _strengthCalculator.CalculateSnapshot(p, ev, st, rk, un, sa, sv, SelectedDate, filterUnitId);

            PresentList.Clear();
            foreach (var item in CurrentSnapshot.PresentPersonnel.OrderBy(x => x.Rank?.SortOrder ?? 99))
            {
                PresentList.Add(item);
            }

            AbsentList.Clear();
            foreach (var item in CurrentSnapshot.AbsentPersonnel.OrderBy(x => x.Rank?.SortOrder ?? 99))
            {
                AbsentList.Add(item);
            }

            OnPropertyChanged(nameof(CurrentSnapshot));
        }

        private void ExportExcel()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"ΔΥΝΑΜΟΛΟΓΙΟ_{SelectedDate:yyyyMMdd}.xlsx"
                };

                if (sfd.ShowDialog() == true)
                {
                    var req = new ReportGenerationRequest
                    {
                        AsOfTimestamp = SelectedDate,
                        OrganisationUnitId = _selectedUnit?.Id != Guid.Empty ? (Guid?)_selectedUnit.Id : null,
                        UnitTitle = _selectedUnit?.Name ?? "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ"
                    };

                    _reportService.ExportToExcel(req, sfd.FileName);
                    MessageBox.Show($"Το αρχείο Excel δημιουργήθηκε επιτυχώς:\n{sfd.FileName}", "Εξαγωγή Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα εξαγωγής Excel: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReport()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var req = new ReportGenerationRequest
                    {
                        AsOfTimestamp = SelectedDate,
                        OrganisationUnitId = _selectedUnit?.Id != Guid.Empty ? (Guid?)_selectedUnit.Id : null,
                        UnitTitle = _selectedUnit?.Name ?? "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ"
                    };

                    var doc = _reportService.GeneratePrintableDocument(req);
                    IDocumentPaginatorSource dps = doc;
                    printDialog.PrintDocument(dps.DocumentPaginator, "Ημερήσιο Δυναμολόγιο");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα εκτύπωσης: {ex.Message}", "Σφάλμα Εκτύπωσης", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // =========================================================================
    // 3. PERSONNEL VIEWMODEL
    // =========================================================================
    public class PersonnelViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IStatusEngine _statusEngine;
        private readonly IConflictEngine _conflictEngine;
        private readonly IAuditService _auditService;
        private readonly MainViewModel _mainVM;

        private string _searchText = string.Empty;
        private string _selectedCategoryFilter = "Όλοι";
        private PersonnelStatusSnapshot _selectedPerson;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterPersonnel();
                }
            }
        }

        public string SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (SetProperty(ref _selectedCategoryFilter, value))
                {
                    FilterPersonnel();
                }
            }
        }

        public PersonnelStatusSnapshot SelectedPerson
        {
            get => _selectedPerson;
            set => SetProperty(ref _selectedPerson, value);
        }

        public ObservableCollection<PersonnelStatusSnapshot> DisplayPersonnel { get; } = new ObservableCollection<PersonnelStatusSnapshot>();
        private List<PersonnelStatusSnapshot> _allSnapshots = new List<PersonnelStatusSnapshot>();

        public ICommand AddPersonCommand { get; }
        public ICommand EditPersonCommand { get; }
        public ICommand ArchivePersonCommand { get; }

        public PersonnelViewModel(
            IUnitOfWork uow,
            IStatusEngine statusEngine,
            IConflictEngine conflictEngine,
            IAuditService auditService,
            MainViewModel mainVM)
        {
            _uow = uow;
            _statusEngine = statusEngine;
            _conflictEngine = conflictEngine;
            _auditService = auditService;
            _mainVM = mainVM;

            AddPersonCommand = new RelayCommand(AddPerson);
            EditPersonCommand = new RelayCommand(EditPerson, () => SelectedPerson != null);
            ArchivePersonCommand = new RelayCommand(ArchivePerson, () => SelectedPerson != null);
        }

        public void LoadData()
        {
            var personnel = _uow.Personnel.GetAll().ToList();
            var ranks = _uow.Ranks.GetAll().ToDictionary(r => r.Id);
            var units = _uow.OrganisationUnits.GetAll().ToDictionary(u => u.Id);
            var events = _uow.StatusEvents.GetAll().GroupBy(e => e.PersonnelId).ToDictionary(g => g.Key, g => g.ToList());
            var statusTypes = _uow.StatusTypes.GetAll().ToList();
            var services = _uow.ServiceAssignments.GetAll().GroupBy(s => s.PersonnelId).ToDictionary(g => g.Key, g => g.ToList());
            var serviceTypes = _uow.ServiceTypes.GetAll().ToList();

            _allSnapshots.Clear();
            foreach (var p in personnel)
            {
                ranks.TryGetValue(p.RankId, out var rank);
                units.TryGetValue(p.OrganisationUnitId, out var unit);
                events.TryGetValue(p.Id, out var pEvents);
                services.TryGetValue(p.Id, out var pServices);

                var sn = _statusEngine.CalculatePersonStatus(p, pEvents, statusTypes, rank, unit, pServices, serviceTypes, DateTime.Today);
                _allSnapshots.Add(sn);
            }

            FilterPersonnel();
        }

        private void FilterPersonnel()
        {
            DisplayPersonnel.Clear();
            var query = _allSnapshots.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string s = SearchText.Trim();
                query = query.Where(x =>
                    (x.Person.LastName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (x.Person.FirstName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (x.Person.MilitaryServiceNumber?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (x.Rank?.Name?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
            }

            if (SelectedCategoryFilter == "Στελέχη")
            {
                query = query.Where(x => x.Person.Category == PersonnelCategory.OfficerOrNco);
            }
            else if (SelectedCategoryFilter == "Οπλίτες")
            {
                query = query.Where(x => x.Person.Category == PersonnelCategory.Conscript);
            }

            foreach (var item in query.OrderBy(x => x.Rank?.SortOrder ?? 99).ThenBy(x => x.Person.LastName))
            {
                DisplayPersonnel.Add(item);
            }
        }

        private void AddPerson()
        {
            // Prompt add dialog or quick add
            var ranks = _uow.Ranks.GetAll().OrderBy(r => r.SortOrder).ToList();
            var units = _uow.OrganisationUnits.GetAll().OrderBy(u => u.SortOrder).ToList();

            var newPerson = new Personnel
            {
                LastName = "ΝΕΟ",
                FirstName = "ΠΡΟΣΩΠΟ",
                RankId = ranks.FirstOrDefault()?.Id ?? Guid.Empty,
                OrganisationUnitId = units.FirstOrDefault()?.Id ?? Guid.Empty,
                Category = ranks.FirstOrDefault()?.Category ?? PersonnelCategory.Conscript,
                StrengthStartDate = DateTime.Today
            };

            _uow.Personnel.Insert(newPerson);
            _auditService.LogAction(AuditAction.Create, "Personnel", newPerson.Id.ToString(), $"Προσθήκη νέου προσωπικού {newPerson.FullName}");
            LoadData();
        }

        private void EditPerson()
        {
            if (SelectedPerson?.Person == null) return;
            _uow.Personnel.Update(SelectedPerson.Person);
            _auditService.LogAction(AuditAction.Update, "Personnel", SelectedPerson.Person.Id.ToString(), $"Ενημέρωση στοιχείων {SelectedPerson.Person.FullName}");
            LoadData();
        }

        private void ArchivePerson()
        {
            if (SelectedPerson?.Person == null) return;
            var res = MessageBox.Show(
                $"Θα αρχειοθετηθεί ο:\n{SelectedPerson.Rank?.ShortName} {SelectedPerson.Person.FullName}\n\nΗ εγγραφή θα παραμείνει διαθέσιμη στο ιστορικό.\nΣυνέχεια;",
                "Επιβεβαίωση Αρχειοθέτησης",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                SelectedPerson.Person.IsArchived = true;
                _uow.Personnel.Update(SelectedPerson.Person);
                _auditService.LogAction(AuditAction.Archive, "Personnel", SelectedPerson.Person.Id.ToString(), $"Αρχειοθέτηση {SelectedPerson.Person.FullName}");
                LoadData();
            }
        }
    }

    // =========================================================================
    // 4. ABSENCES VIEWMODEL
    // =========================================================================
    public class AbsencesViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IStatusEngine _statusEngine;
        private readonly IConflictEngine _conflictEngine;
        private readonly IAuditService _auditService;
        private readonly MainViewModel _mainVM;

        public ObservableCollection<StatusEvent> ActiveEventsList { get; } = new ObservableCollection<StatusEvent>();
        public ObservableCollection<StatusType> StatusTypesList { get; } = new ObservableCollection<StatusType>();
        public ObservableCollection<Personnel> PersonnelList { get; } = new ObservableCollection<Personnel>();

        // Form Fields for New Absence
        private Personnel _selectedPerson;
        private StatusType _selectedStatusType;
        private DateTime _startDate = DateTime.Today;
        private DateTime _returnDate = DateTime.Today.AddDays(5);
        private string _referenceDoc = string.Empty;
        private string _comment = string.Empty;

        public Personnel SelectedPerson { get => _selectedPerson; set => SetProperty(ref _selectedPerson, value); }
        public StatusType SelectedStatusType { get => _selectedStatusType; set => SetProperty(ref _selectedStatusType, value); }
        public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }
        public DateTime ReturnDate { get => _returnDate; set => SetProperty(ref _returnDate, value); }
        public string ReferenceDoc { get => _referenceDoc; set => SetProperty(ref _referenceDoc, value); }
        public string Comment { get => _comment; set => SetProperty(ref _comment, value); }

        public ICommand SaveAbsenceCommand { get; }
        public ICommand CancelAbsenceCommand { get; }

        public AbsencesViewModel(
            IUnitOfWork uow,
            IStatusEngine statusEngine,
            IConflictEngine conflictEngine,
            IAuditService auditService,
            MainViewModel mainVM)
        {
            _uow = uow;
            _statusEngine = statusEngine;
            _conflictEngine = conflictEngine;
            _auditService = auditService;
            _mainVM = mainVM;

            SaveAbsenceCommand = new RelayCommand(SaveAbsence);
            CancelAbsenceCommand = new RelayCommand(param => CancelAbsence(param as StatusEvent));
        }

        public void LoadData()
        {
            StatusTypesList.Clear();
            foreach (var st in _uow.StatusTypes.GetAll().OrderBy(x => x.SortOrder)) StatusTypesList.Add(st);
            if (SelectedStatusType == null) SelectedStatusType = StatusTypesList.FirstOrDefault();

            PersonnelList.Clear();
            foreach (var p in _uow.Personnel.Find(x => !x.IsArchived).OrderBy(x => x.LastName)) PersonnelList.Add(p);
            if (SelectedPerson == null) SelectedPerson = PersonnelList.FirstOrDefault();

            ActiveEventsList.Clear();
            foreach (var ev in _uow.StatusEvents.Find(x => !x.IsCancelled).OrderByDescending(x => x.StartAt))
            {
                ActiveEventsList.Add(ev);
            }
        }

        private void SaveAbsence()
        {
            if (SelectedPerson == null || SelectedStatusType == null)
            {
                MessageBox.Show("Παρακαλώ επιλέξτε πρόσωπο και είδος απουσίας.", "Ελλιπή Στοιχεία", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusIntervalMath.CreateDayInterval(StartDate, ReturnDate, out var startAt, out var endExclusive);

            var newEvent = new StatusEvent
            {
                PersonnelId = SelectedPerson.Id,
                StatusTypeId = SelectedStatusType.Id,
                StartAt = startAt,
                EndAtExclusive = endExclusive,
                ReferenceDocument = ReferenceDoc,
                Comment = Comment
            };

            var existingEvents = _uow.StatusEvents.Find(e => e.PersonnelId == SelectedPerson.Id);
            var conflicts = _conflictEngine.ValidateStatusEvent(newEvent, SelectedPerson, existingEvents, StatusTypesList);

            var errors = conflicts.Where(c => c.Severity == ConflictSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                string msg = string.Join("\n", errors.Select(e => "• " + e.Message));
                MessageBox.Show($"Αδυναμία αποθήκευσης λόγω συγκρούσεων:\n{msg}", "Έλεγχος Δεδομένων", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var warnings = conflicts.Where(c => c.Severity == ConflictSeverity.Warning).ToList();
            if (warnings.Count > 0)
            {
                string msg = string.Join("\n", warnings.Select(w => "• " + w.Message));
                var confirm = MessageBox.Show($"Εντοπίστηκαν προειδοποιήσεις:\n{msg}\n\nΕπιθυμείτε να συνεχίσετε;", "Προειδοποίηση", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
            }

            _uow.StatusEvents.Insert(newEvent);
            _auditService.LogAction(AuditAction.Create, "StatusEvent", newEvent.Id.ToString(), $"Καταχώρηση {SelectedStatusType.Name} για {SelectedPerson.FullName} ({startAt:dd/MM} - {endExclusive:dd/MM})");

            MessageBox.Show($"Η απουσία καταχωρήθηκε επιτυχώς!\nΚατάσταση: ΑΠΩΝ μέχρι {endExclusive:dd/MM/yyyy}", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);

            ReferenceDoc = string.Empty;
            Comment = string.Empty;
            LoadData();
        }

        private void CancelAbsence(StatusEvent ev)
        {
            if (ev == null) return;
            var res = MessageBox.Show("Θέλετε να ακυρώσετε την επιλεγμένη απουσία;", "Επιβεβαίωση Ακύρωσης", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                ev.IsCancelled = true;
                ev.CancelledAt = DateTime.Now;
                _uow.StatusEvents.Update(ev);
                _auditService.LogAction(AuditAction.Cancel, "StatusEvent", ev.Id.ToString(), "Ακύρωση απουσίας");
                LoadData();
            }
        }
    }

    // =========================================================================
    // 5. SERVICES (DUTY ROSTER) VIEWMODEL
    // =========================================================================
    public class ServicesViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IAuditService _auditService;
        private readonly MainViewModel _mainVM;

        public ObservableCollection<ServiceAssignment> TodayAssignments { get; } = new ObservableCollection<ServiceAssignment>();
        public ObservableCollection<ServiceType> ServiceTypesList { get; } = new ObservableCollection<ServiceType>();
        public ObservableCollection<Personnel> PersonnelList { get; } = new ObservableCollection<Personnel>();

        private Personnel _selectedPerson;
        private ServiceType _selectedServiceType;
        private DateTime _serviceDate = DateTime.Today;
        private string _dutyLocation = "Κεντρική Πύλη";

        public Personnel SelectedPerson { get => _selectedPerson; set => SetProperty(ref _selectedPerson, value); }
        public ServiceType SelectedServiceType { get => _selectedServiceType; set => SetProperty(ref _selectedServiceType, value); }
        public DateTime ServiceDate { get => _serviceDate; set => SetProperty(ref _serviceDate, value); }
        public string DutyLocation { get => _dutyLocation; set => SetProperty(ref _dutyLocation, value); }

        public ICommand AssignServiceCommand { get; }

        public ServicesViewModel(IUnitOfWork uow, IAuditService auditService, MainViewModel mainVM)
        {
            _uow = uow;
            _auditService = auditService;
            _mainVM = mainVM;

            AssignServiceCommand = new RelayCommand(AssignService);
        }

        public void LoadData()
        {
            ServiceTypesList.Clear();
            foreach (var st in _uow.ServiceTypes.GetAll().OrderBy(x => x.SortOrder)) ServiceTypesList.Add(st);
            if (SelectedServiceType == null) SelectedServiceType = ServiceTypesList.FirstOrDefault();

            PersonnelList.Clear();
            foreach (var p in _uow.Personnel.Find(x => !x.IsArchived).OrderBy(x => x.LastName)) PersonnelList.Add(p);
            if (SelectedPerson == null) SelectedPerson = PersonnelList.FirstOrDefault();

            TodayAssignments.Clear();
            foreach (var sa in _uow.ServiceAssignments.Find(s => !s.IsCancelled && s.ServiceDate == ServiceDate))
            {
                TodayAssignments.Add(sa);
            }
        }

        private void AssignService()
        {
            if (SelectedPerson == null || SelectedServiceType == null) return;

            var assignment = new ServiceAssignment
            {
                PersonnelId = SelectedPerson.Id,
                ServiceTypeId = SelectedServiceType.Id,
                ServiceDate = ServiceDate,
                StartDateTime = ServiceDate.Date.Add(SelectedServiceType.DefaultStartTime),
                EndDateTime = ServiceDate.Date.Add(SelectedServiceType.DefaultEndTime),
                DutyLocation = DutyLocation
            };

            _uow.ServiceAssignments.Insert(assignment);
            _auditService.LogAction(AuditAction.Create, "ServiceAssignment", assignment.Id.ToString(), $"Ανάθεση υπηρεσίας {SelectedServiceType.Name} στον {SelectedPerson.FullName}");

            MessageBox.Show("Η υπηρεσία ανατέθηκε επιτυχώς!", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadData();
        }
    }

    // =========================================================================
    // 6. IMPORT / EXPORT VIEWMODEL
    // =========================================================================
    public class ImportExportViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IExcelImportService _importService;
        private readonly IAuditService _auditService;
        private readonly MainViewModel _mainVM;

        private string _selectedFilePath = string.Empty;
        private ImportPreviewReport _previewReport;

        public string SelectedFilePath { get => _selectedFilePath; set => SetProperty(ref _selectedFilePath, value); }
        public ImportPreviewReport PreviewReport { get => _previewReport; set => SetProperty(ref _previewReport, value); }

        public ICommand SelectFileCommand { get; }
        public ICommand CommitImportCommand { get; }

        public ImportExportViewModel(IUnitOfWork uow, IExcelImportService importService, IAuditService auditService, MainViewModel mainVM)
        {
            _uow = uow;
            _importService = importService;
            _auditService = auditService;
            _mainVM = mainVM;

            SelectFileCommand = new RelayCommand(SelectFile);
            CommitImportCommand = new RelayCommand(CommitImport, () => PreviewReport != null && PreviewReport.TotalRows > 0);
        }

        private void SelectFile()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls",
                Title = "Επιλογή Αρχείου Excel Προσωπικού"
            };

            if (ofd.ShowDialog() == true)
            {
                SelectedFilePath = ofd.FileName;
                try
                {
                    PreviewReport = _importService.AnalyzeAndPreviewImport(SelectedFilePath, _uow);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Σφάλμα ανάλυσης αρχείου: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CommitImport()
        {
            if (PreviewReport == null) return;
            try
            {
                var batch = _importService.CommitImport(PreviewReport, _uow);
                _auditService.LogAction(AuditAction.Import, "ImportBatch", batch.Id.ToString(), $"Εισαγωγή {batch.InsertedCount} νέων και {batch.UpdatedCount} ενημερώσεων από {batch.FileName}", null, null, batch.Id);

                MessageBox.Show($"Η εισαγωγή ολοκληρώθηκε επιτυχώς!\nΝέες Εγγραφές: {batch.InsertedCount}\nΕνημερώσεις: {batch.UpdatedCount}", "Επιτυχής Εισαγωγή", MessageBoxButton.OK, MessageBoxImage.Information);

                PreviewReport = null;
                SelectedFilePath = string.Empty;
                _mainVM.Navigate("Personnel");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα κατά την εισαγωγή: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // =========================================================================
    // 7. SETTINGS & BACKUP VIEWMODEL
    // =========================================================================
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IBackupService _backupService;
        private readonly string _dbFilePath;
        private readonly MainViewModel _mainVM;

        public ObservableCollection<AuditEvent> RecentAudits { get; } = new ObservableCollection<AuditEvent>();

        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand ExportDiagnosticsCommand { get; }

        public SettingsViewModel(IUnitOfWork uow, IBackupService backupService, string dbFilePath, MainViewModel mainVM)
        {
            _uow = uow;
            _backupService = backupService;
            _dbFilePath = dbFilePath;
            _mainVM = mainVM;

            CreateBackupCommand = new RelayCommand(CreateBackup);
            RestoreBackupCommand = new RelayCommand(RestoreBackup);
            ExportDiagnosticsCommand = new RelayCommand(ExportDiagnostics);
        }

        public void LoadData()
        {
            RecentAudits.Clear();
            foreach (var a in _uow.AuditEvents.GetAll().OrderByDescending(x => x.CreatedAt).Take(50))
            {
                RecentAudits.Add(a);
            }
        }

        private void CreateBackup()
        {
            try
            {
                string zipPath = _backupService.CreateBackup();
                MessageBox.Show($"Το αντίγραφο ασφαλείας δημιουργήθηκε επιτυχώς:\n{zipPath}", "Αντίγραφο Ασφαλείας", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα δημιουργίας αντιγράφου: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreBackup()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Backup Zip (*.zip)|*.zip",
                Title = "Επιλογή Αντιγράφου Ασφαλείας προς Επαναφορά"
            };

            if (ofd.ShowDialog() == true)
            {
                var confirm = MessageBox.Show(
                    "ΠΡΟΣΟΧΗ: Η επαναφορά θα αντικαταστήσει την τρέχουσα βάση δεδομένων.\n(Θα δημιουργηθεί αυτόματα προστατευτικό αντίγραφο ασφαλείας πριν την αντικατάσταση).\n\nΕπιθυμείτε να συνεχίσετε;",
                    "Επαλήθευση Επαναφοράς",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm == MessageBoxResult.Yes)
                {
                    if (_backupService.RestoreBackup(ofd.FileName, out var error))
                    {
                        MessageBox.Show("Η επαναφορά ολοκληρώθηκε επιτυχώς! Η εφαρμογή θα ανανεώσει τα δεδομένα της.", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);
                        _mainVM.RefreshActive();
                    }
                    else
                    {
                        MessageBox.Show($"Αποτυχία επαναφοράς: {error}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ExportDiagnosticsCommandAction() => ExportDiagnostics();

        private void ExportDiagnostics()
        {
            try
            {
                string zip = DiagnosticPackageService.GenerateDiagnosticPackage(_uow, _dbFilePath);
                MessageBox.Show($"Το τεχνικό διαγνωστικό πακέτο εξήχθη επιτυχώς στην Επιφάνεια Εργασίας:\n{zip}", "Διαγνωστικό Πακέτο", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα εξαγωγής διαγνωστικών: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
