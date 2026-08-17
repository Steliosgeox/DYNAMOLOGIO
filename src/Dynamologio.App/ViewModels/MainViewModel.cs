using System;
using System.Linq;
using System.Windows.Input;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.ImportExport.Excel.Import;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Reporting.Services;

namespace Dynamologio.App.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IStatusEngine _statusEngine;
        private readonly IStrengthCalculator _strengthCalculator;
        private readonly IConflictEngine _conflictEngine;
        private readonly IReportGeneratorService _reportService;
        private readonly IExcelImportService _importService;
        private readonly IBackupService _backupService;
        private readonly IDiagnosticPackageService _diagnosticService;
        private readonly IAuditService _auditService;
        private readonly IPersonnelService _personnelService;
        private readonly IAbsenceService _absenceService;
        private readonly IDutyService _dutyService;
        private readonly IClock _clock;

        private object _currentViewModel;
        private string _activeSection = "Dashboard";
        private string _unitNameHeader = "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ";
        private string _officeNameHeader = "1ο ΓΡΑΦΕΙΟ";

        public object CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public string ActiveSection
        {
            get => _activeSection;
            set => SetProperty(ref _activeSection, value);
        }

        public string UnitNameHeader
        {
            get => _unitNameHeader;
            set => SetProperty(ref _unitNameHeader, value);
        }

        public string OfficeNameHeader
        {
            get => _officeNameHeader;
            set => SetProperty(ref _officeNameHeader, value);
        }

        public DashboardViewModel DashboardVM { get; }
        public DynamologioViewModel DynamologioVM { get; }
        public PersonnelViewModel PersonnelVM { get; }
        public AbsencesViewModel AbsencesVM { get; }
        public ServicesViewModel ServicesVM { get; }
        public ReportsViewModel ReportsVM { get; }
        public ImportExportViewModel ImportExportVM { get; }
        public HistoryViewModel HistoryVM { get; }
        public DataValidationViewModel DataValidationVM { get; }
        public SettingsViewModel SettingsVM { get; }

        public ICommand NavigateCommand { get; }

        public MainViewModel(
            IUnitOfWork uow,
            IStatusEngine statusEngine,
            IStrengthCalculator strengthCalculator,
            IConflictEngine conflictEngine,
            IReportGeneratorService reportService,
            IExcelImportService importService,
            IBackupService backupService,
            IDiagnosticPackageService diagnosticService,
            IAuditService auditService,
            IClock clock,
            IPersonnelService personnelService = null,
            IAbsenceService absenceService = null,
            IDutyService dutyService = null)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _statusEngine = statusEngine ?? throw new ArgumentNullException(nameof(statusEngine));
            _strengthCalculator = strengthCalculator ?? throw new ArgumentNullException(nameof(strengthCalculator));
            _conflictEngine = conflictEngine ?? throw new ArgumentNullException(nameof(conflictEngine));
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _diagnosticService = diagnosticService ?? throw new ArgumentNullException(nameof(diagnosticService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _clock = clock ?? SystemClock.Instance;

            _personnelService = personnelService ?? new PersonnelService(_uow, _auditService);
            _absenceService = absenceService ?? new AbsenceService(_uow, _auditService);
            _dutyService = dutyService ?? new DutyService(_uow, _auditService);

            DashboardVM = new DashboardViewModel(_uow, _strengthCalculator, _clock, this);
            DynamologioVM = new DynamologioViewModel(_uow, _strengthCalculator, _reportService, _clock, this);
            PersonnelVM = new PersonnelViewModel(_uow, _statusEngine, _conflictEngine, _personnelService, _clock, this);
            AbsencesVM = new AbsencesViewModel(_uow, _statusEngine, _conflictEngine, _absenceService, _clock, this);
            ServicesVM = new ServicesViewModel(_uow, _conflictEngine, _dutyService, _clock, this);
            ReportsVM = new ReportsViewModel(_uow, _strengthCalculator, _reportService, _clock, this);
            ImportExportVM = new ImportExportViewModel(_uow, _importService, this);
            HistoryVM = new HistoryViewModel(_uow, _clock, this);
            DataValidationVM = new DataValidationViewModel(_uow, _conflictEngine, _statusEngine, _clock, this);
            SettingsVM = new SettingsViewModel(_uow, _backupService, _diagnosticService, _clock, this);

            NavigateCommand = new RelayCommand(param => Navigate(param as string));

            // Load deployment settings
            var uSetting = _uow.AppSettings.Find(s => s.Key == "Deployment.UnitName").FirstOrDefault();
            if (uSetting != null && !string.IsNullOrWhiteSpace(uSetting.Value)) UnitNameHeader = uSetting.Value;

            var oSetting = _uow.AppSettings.Find(s => s.Key == "Deployment.OfficeName").FirstOrDefault();
            if (oSetting != null && !string.IsNullOrWhiteSpace(oSetting.Value)) OfficeNameHeader = oSetting.Value;

            Navigate("Dashboard");
        }

        public void UpdateDeploymentHeader(string unit, string office)
        {
            UnitNameHeader = unit;
            OfficeNameHeader = office;
        }

        public void RefreshCurrentView()
        {
            Navigate(ActiveSection);
        }

        public void Navigate(string section)
        {
            ActiveSection = section;

            switch (section)
            {
                case "Dashboard":
                    DashboardVM.LoadData();
                    CurrentViewModel = DashboardVM;
                    break;
                case "Dynamologio":
                    DynamologioVM.LoadData();
                    CurrentViewModel = DynamologioVM;
                    break;
                case "Personnel":
                    PersonnelVM.LoadData();
                    CurrentViewModel = PersonnelVM;
                    break;
                case "Absences":
                    AbsencesVM.LoadData();
                    CurrentViewModel = AbsencesVM;
                    break;
                case "Services":
                    ServicesVM.LoadData();
                    CurrentViewModel = ServicesVM;
                    break;
                case "Reports":
                    ReportsVM.LoadData();
                    CurrentViewModel = ReportsVM;
                    break;
                case "ImportExport":
                    CurrentViewModel = ImportExportVM;
                    break;
                case "DataValidation":
                    DataValidationVM.LoadData();
                    CurrentViewModel = DataValidationVM;
                    break;
                case "History":
                    HistoryVM.LoadData();
                    CurrentViewModel = HistoryVM;
                    break;
                case "Settings":
                    SettingsVM.LoadData();
                    CurrentViewModel = SettingsVM;
                    break;
            }
        }
    }
}
