using System;
using System.Windows.Input;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Interfaces;
using Dynamologio.ImportExport.Excel;
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
        private readonly IBackupService _backupService;
        private readonly IAuditService _auditService;
        private readonly IExcelImportService _importService;
        private readonly IReportGeneratorService _reportService;

        private object _currentView;
        private string _statusMessage = "Έτοιμο";
        private string _activeTab = "Dashboard";

        public DashboardViewModel DashboardVM { get; }
        public DynamologioViewModel DynamologioVM { get; }
        public PersonnelViewModel PersonnelVM { get; }
        public AbsencesViewModel AbsencesVM { get; }
        public ServicesViewModel ServicesVM { get; }
        public ImportExportViewModel ImportExportVM { get; }
        public SettingsViewModel SettingsVM { get; }

        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ActiveTab
        {
            get => _activeTab;
            set => SetProperty(ref _activeTab, value);
        }

        public string CurrentDateDisplay => DateTime.Now.ToString("dddd, dd MMMM yyyy");

        public ICommand NavigateCommand { get; }
        public ICommand RefreshAllCommand { get; }

        public MainViewModel(
            IUnitOfWork uow,
            string dbFilePath)
        {
            _uow = uow;
            _statusEngine = new StatusEngine();
            _strengthCalculator = new StrengthCalculationEngine(_statusEngine);
            _conflictEngine = new ConflictEngine();
            _auditService = new AuditService(_uow);
            _backupService = new BackupService(dbFilePath, _uow);
            _importService = new ExcelImportService();
            _reportService = new ReportGeneratorService(_uow, _strengthCalculator);

            DashboardVM = new DashboardViewModel(_uow, _strengthCalculator, this);
            DynamologioVM = new DynamologioViewModel(_uow, _strengthCalculator, _reportService, this);
            PersonnelVM = new PersonnelViewModel(_uow, _statusEngine, _conflictEngine, _auditService, this);
            AbsencesVM = new AbsencesViewModel(_uow, _statusEngine, _conflictEngine, _auditService, this);
            ServicesVM = new ServicesViewModel(_uow, _auditService, this);
            ImportExportVM = new ImportExportViewModel(_uow, _importService, _auditService, this);
            SettingsVM = new SettingsViewModel(_uow, _backupService, dbFilePath, this);

            NavigateCommand = new RelayCommand(param => Navigate(param?.ToString()));
            RefreshAllCommand = new RelayCommand(() => RefreshActive());

            Navigate("Dashboard");
        }

        public void Navigate(string destination)
        {
            ActiveTab = destination;
            switch (destination)
            {
                case "Dashboard":
                    DashboardVM.LoadData();
                    CurrentView = DashboardVM;
                    break;
                case "Dynamologio":
                    DynamologioVM.LoadData();
                    CurrentView = DynamologioVM;
                    break;
                case "Personnel":
                    PersonnelVM.LoadData();
                    CurrentView = PersonnelVM;
                    break;
                case "Absences":
                    AbsencesVM.LoadData();
                    CurrentView = AbsencesVM;
                    break;
                case "Services":
                    ServicesVM.LoadData();
                    CurrentView = ServicesVM;
                    break;
                case "ImportExport":
                    CurrentView = ImportExportVM;
                    break;
                case "Settings":
                    SettingsVM.LoadData();
                    CurrentView = SettingsVM;
                    break;
            }
        }

        public void RefreshActive()
        {
            Navigate(ActiveTab);
            StatusMessage = $"Ανανεώθηκε επιτυχώς στις {DateTime.Now:HH:mm:ss}";
        }
    }
}
