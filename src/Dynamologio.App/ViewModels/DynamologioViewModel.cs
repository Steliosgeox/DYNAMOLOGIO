using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Dynamologio.Reporting.Services;
using Dynamologio.App.Services;

namespace Dynamologio.App.ViewModels
{
    public enum TemplateVerificationState
    {
        Verified,
        ShaMismatch,
        Missing
    }

    public class DynamologioViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IUnitOfWork _uow;
        private readonly IStrengthCalculator _strengthCalculator;
        private readonly IReportGeneratorService _reportService;
        private readonly IClock _clock;
        private readonly IShellStateService _shellState;
        private readonly IPrintService _printService;
        private readonly IFileDialogService _fileDialogService;
        private readonly INotificationService _notificationService;

        public ObservableCollection<OrganisationUnit> OrganisationUnitsList { get; } = new ObservableCollection<OrganisationUnit>();

        private DateTime _selectedDate;
        private OrganisationUnit _selectedUnit;
        private UnitStrengthSnapshot _currentSnapshot;
        private TemplateVerificationState _templateStatus = TemplateVerificationState.Verified;
        private string _templateStatusMessage = "Πρότυπο: Επαληθευμένο";

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    RefreshCalculations();
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
                    RefreshCalculations();
                }
            }
        }

        public UnitStrengthSnapshot CurrentSnapshot
        {
            get => _currentSnapshot;
            set => SetProperty(ref _currentSnapshot, value);
        }

        public TemplateVerificationState TemplateStatus
        {
            get => _templateStatus;
            set => SetProperty(ref _templateStatus, value);
        }

        public string TemplateStatusMessage
        {
            get => _templateStatusMessage;
            set => SetProperty(ref _templateStatusMessage, value);
        }

        public ICommand SetTodayCommand { get; }
        public ICommand SetTomorrowCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand PrintReportCommand { get; }

        public DynamologioViewModel(
            IUnitOfWork uow,
            IStrengthCalculator strengthCalculator,
            IReportGeneratorService reportService,
            IClock clock,
            IShellStateService shellState,
            IPrintService printService,
            IFileDialogService fileDialogService,
            INotificationService notificationService)
        {
            _uow = uow;
            _strengthCalculator = strengthCalculator;
            _reportService = reportService;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _shellState = shellState ?? throw new ArgumentNullException(nameof(shellState));
            _printService = printService ?? throw new ArgumentNullException(nameof(printService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

            _selectedDate = _clock.Today;

            SetTodayCommand = new RelayCommand(_ => SelectedDate = _clock.Today);
            SetTomorrowCommand = new RelayCommand(_ => SelectedDate = _clock.Today.AddDays(1));
            ExportExcelCommand = new RelayCommand(ExportToExcel);
            PrintReportCommand = new RelayCommand(PrintReport);
        }

        public void Activate()
        {
            LoadData();
        }

        public void LoadData()
        {
            OrganisationUnitsList.Clear();
            OrganisationUnitsList.Add(new OrganisationUnit { Name = "Όλη η Μονάδα (Συνολικό)", Id = Guid.Empty });
            foreach (var u in _uow.OrganisationUnits.GetAll().OrderBy(x => x.SortOrder))
            {
                OrganisationUnitsList.Add(u);
            }
            if (SelectedUnit == null) SelectedUnit = OrganisationUnitsList.First();

            CheckTemplateStatus();
            RefreshCalculations();
        }

        private void CheckTemplateStatus()
        {
            var status = _reportService.CheckTemplateStatus(out _, out _, out _);
            switch (status)
            {
                case TemplateVerificationStatus.Verified:
                    TemplateStatus = TemplateVerificationState.Verified;
                    TemplateStatusMessage = "Πρότυπο: Επαληθευμένο (SHA-256)";
                    break;
                case TemplateVerificationStatus.ShaMismatch:
                    TemplateStatus = TemplateVerificationState.ShaMismatch;
                    TemplateStatusMessage = "Πρότυπο: Ασυμφωνία SHA-256";
                    break;
                case TemplateVerificationStatus.Missing:
                default:
                    TemplateStatus = TemplateVerificationState.Missing;
                    TemplateStatusMessage = "Πρότυπο: Μη Διαθέσιμο";
                    break;
            }
        }

        private void RefreshCalculations()
        {
            Guid? filterUnitId = SelectedUnit != null && SelectedUnit.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null;

            var p = _uow.Personnel.GetAll();
            var ev = _uow.StatusEvents.GetAll();
            var st = _uow.StatusTypes.GetAll();
            var rk = _uow.Ranks.GetAll();
            var un = _uow.OrganisationUnits.GetAll();
            var sa = _uow.ServiceAssignments.GetAll();
            var sv = _uow.ServiceTypes.GetAll();

            CurrentSnapshot = _strengthCalculator.CalculateSnapshot(p, ev, st, rk, un, sa, sv, SelectedDate, filterUnitId);
        }

        private void ExportToExcel()
        {
            try
            {
                var sfd = _fileDialogService.SaveExcelFile($"ΔΥΝΑΜΟΛΟΓΙΟ_{SelectedDate:yyyyMMdd}.xlsx");

                if (sfd != null)
                {
                    var req = new ReportGenerationRequest
                    {
                        Type = ReportType.DailyDynamologio,
                        AsOfTimestamp = SelectedDate,
                        OrganisationUnitId = SelectedUnit?.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null,
                        UnitTitle = _shellState.UnitName
                    };

                    _reportService.ExportToExcel(req, sfd);
                    _notificationService.Info("Εξαγωγή Δυναμολογίου", $"Το αρχείο Excel εξήχθη επιτυχώς:\n{sfd}");
                }
            }
            catch (Exception ex)
            {
                _notificationService.Error("Σφάλμα", $"Σφάλμα εξαγωγής: {ex.Message}");
            }
        }

        private void PrintReport()
        {
            try
            {
                var req = new ReportGenerationRequest
                {
                    Type = ReportType.DailyDynamologio,
                    AsOfTimestamp = SelectedDate,
                    OrganisationUnitId = SelectedUnit?.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null,
                    UnitTitle = _shellState.UnitName
                };

                var doc = _reportService.GeneratePrintableDocument(req);
                _printService.PrintDocument(doc, "Ημερήσιο Δυναμολόγιο");
            }
            catch (Exception ex)
            {
                _notificationService.Error("Σφάλμα", $"Σφάλμα εκτύπωσης: {ex.Message}");
            }
        }
    }
}
