using System;
using Dynamologio.App.ViewModels;
using Dynamologio.Core.Interfaces;
using Dynamologio.ImportExport.Excel.Import;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Reporting.Services;
using Dynamologio.App.Services;

namespace Dynamologio.App.Navigation
{
    /// <summary>
    /// Explicit constructor-injection factory. No reflection. No service locator.
    /// Creates feature ViewModels on demand with their specific dependencies.
    /// </summary>
    public class ViewModelFactory : IViewModelFactory
    {
        private readonly IUnitOfWork _uow;
        private readonly IStatusEngine _statusEngine;
        private readonly IStrengthCalculator _strengthCalculator;
        private readonly IConflictEngine _conflictEngine;
        private readonly IReportGeneratorService _reportService;
        private readonly IExcelImportService _importService;
        private readonly IBackupService _backupService;
        private readonly IDatabaseLifecycleCoordinator _lifecycleCoordinator;
        private readonly IDiagnosticPackageService _diagnosticService;
        private readonly IAuditService _auditService;
        private readonly IPersonnelService _personnelService;
        private readonly IAbsenceService _absenceService;
        private readonly IDutyService _dutyService;
        private readonly IClock _clock;
        private readonly INavigationService _navigationService;
        private readonly IShellStateService _shellState;
        private readonly IFileDialogService _fileDialogService;
        private readonly INotificationService _notificationService;
        private readonly IConfirmationService _confirmationService;
        private readonly IPrintService _printService;

        public ViewModelFactory(
            IUnitOfWork uow,
            IStatusEngine statusEngine,
            IStrengthCalculator strengthCalculator,
            IConflictEngine conflictEngine,
            IReportGeneratorService reportService,
            IExcelImportService importService,
            IBackupService backupService,
            IDatabaseLifecycleCoordinator lifecycleCoordinator,
            IDiagnosticPackageService diagnosticService,
            IAuditService auditService,
            IPersonnelService personnelService,
            IAbsenceService absenceService,
            IDutyService dutyService,
            IClock clock,
            INavigationService navigationService,
            IShellStateService shellState,
            IFileDialogService fileDialogService,
            INotificationService notificationService,
            IConfirmationService confirmationService,
            IPrintService printService)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _statusEngine = statusEngine ?? throw new ArgumentNullException(nameof(statusEngine));
            _strengthCalculator = strengthCalculator ?? throw new ArgumentNullException(nameof(strengthCalculator));
            _conflictEngine = conflictEngine ?? throw new ArgumentNullException(nameof(conflictEngine));
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _lifecycleCoordinator = lifecycleCoordinator;
            _diagnosticService = diagnosticService ?? throw new ArgumentNullException(nameof(diagnosticService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _personnelService = personnelService ?? throw new ArgumentNullException(nameof(personnelService));
            _absenceService = absenceService ?? throw new ArgumentNullException(nameof(absenceService));
            _dutyService = dutyService ?? throw new ArgumentNullException(nameof(dutyService));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _shellState = shellState ?? throw new ArgumentNullException(nameof(shellState));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
            _printService = printService ?? throw new ArgumentNullException(nameof(printService));
        }

        public ViewModelBase Create(NavigationSection section)
        {
            switch (section)
            {
                case NavigationSection.Dashboard:
                    return new DashboardViewModel(_uow, _strengthCalculator, _clock, _navigationService);
                case NavigationSection.Dynamologio:
                    return new DynamologioViewModel(_uow, _strengthCalculator, _reportService, _clock, _shellState, _printService, _fileDialogService, _notificationService);
                case NavigationSection.Personnel:
                    return new PersonnelViewModel(_uow, _statusEngine, _conflictEngine, _personnelService, _clock, _notificationService, _confirmationService);
                case NavigationSection.Absences:
                    return new AbsencesViewModel(_uow, _statusEngine, _conflictEngine, _absenceService, _clock, _notificationService, _confirmationService);
                case NavigationSection.Services:
                    return new ServicesViewModel(_uow, _conflictEngine, _dutyService, _clock, _notificationService, _confirmationService);
                case NavigationSection.Reports:
                    return new ReportsViewModel(_uow, _strengthCalculator, _reportService, _clock, _shellState, _printService, _fileDialogService, _notificationService);
                case NavigationSection.ImportExport:
                    return new ImportExportViewModel(_uow, _importService, _fileDialogService, _notificationService, _confirmationService);
                case NavigationSection.DataValidation:
                    return new DataValidationViewModel(_uow, _conflictEngine, _statusEngine, _clock);
                case NavigationSection.History:
                    return new HistoryViewModel(_uow, _clock);
                case NavigationSection.Settings:
                    return new SettingsViewModel(_uow, _backupService, _lifecycleCoordinator, _diagnosticService, _clock, _shellState, _fileDialogService, _notificationService, _confirmationService);
                default:
                    throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown navigation section.");
            }
        }
    }
}
