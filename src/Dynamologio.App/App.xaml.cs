using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using Dynamologio.App.ViewModels;
using Dynamologio.App.Views;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Interfaces;
using Dynamologio.ImportExport.Excel;
using Dynamologio.App.Navigation;
using Dynamologio.App.Services;
using Dynamologio.ImportExport.Excel.Import;
using Dynamologio.Infrastructure.LiteDb;
using Dynamologio.Infrastructure.Migrations;
using Dynamologio.Infrastructure.Repositories;
using Dynamologio.Infrastructure.Security;
using Dynamologio.Infrastructure.Services;
using Dynamologio.Reporting.Services;

namespace Dynamologio.App
{
    public partial class App : Application
    {
        private LiteDbContext _dbContext;
        private LiteDbUnitOfWork _unitOfWork;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Hook Unhandled Exception Handlers
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogUnhandledException(args.ExceptionObject as Exception, "AppDomain_Fatal");
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogUnhandledException(args.Exception, "Dispatcher_Fatal");
                
                // Do not blindly swallow unknown critical integrity errors
                MessageBox.Show(
                    $"Παρουσιάστηκε κρίσιμο μη αναμενόμενο σφάλμα:\n\n{args.Exception.Message}\n\nΓια λόγους ασφαλείας και ακεραιότητας των δεδομένων, η εφαρμογή θα τερματιστεί.",
                    "Κρίσιμο Σφάλμα Συστήματος",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true;
                Shutdown(1);
            };

            // 1. Enforce el-GR culture throughout application runtime
            var greekCulture = new CultureInfo("el-GR");
            Thread.CurrentThread.CurrentCulture = greekCulture;
            Thread.CurrentThread.CurrentUICulture = greekCulture;

            try
            {
                // 2. Initialize Database & Run Schema Migrations
                IKeyProtectionProvider keyProvider = new DpapiProtectionProvider();
                _dbContext = new LiteDbContext(keyProvider: keyProvider);
                _unitOfWork = new LiteDbUnitOfWork(_dbContext);

                var migrationRunner = new SchemaMigrationRunner(_unitOfWork);
                migrationRunner.RunMigrations();

                // 3. Composition Root - Construct domain engines and services
                IClock clock = SystemClock.Instance;
                IStatusEngine statusEngine = new StatusEngine();
                IStrengthCalculator strengthCalculator = new StrengthCalculationEngine(statusEngine);
                IConflictEngine conflictEngine = new ConflictEngine();
                IAuditEventSink auditSink = new LiteDbAuditSink(_unitOfWork);
                ICurrentActor currentActor = new WindowsCurrentActor();
                IAuditEventPublisher auditService = new AuditEventPublisher(new[] { auditSink }, currentActor, clock);
                IBackupService backupService = new BackupService(_unitOfWork, _dbContext.DbFilePath, keyProvider);
                IDatabaseLifecycleCoordinator lifecycleCoordinator = new DatabaseLifecycleCoordinator(_dbContext, backupService, keyProvider);
                IDiagnosticPackageService diagnosticService = new DiagnosticPackageService(_unitOfWork, _dbContext.DbFilePath);

                ITransactionRunner transactionRunner = new LiteDbTransactionRunner(_unitOfWork);

                IPersonnelService personnelService = new PersonnelService(_unitOfWork, auditService, transactionRunner, clock, currentActor);
                IAbsenceService absenceService = new AbsenceService(_unitOfWork, auditService, transactionRunner, clock, currentActor);
                IDutyService dutyService = new DutyService(_unitOfWork, auditService, transactionRunner, clock, currentActor);

                IExcelTemplateWriter templateWriter = new NpoiTemplateWriter();
                IReportGeneratorService reportService = new ReportGeneratorService(_unitOfWork, strengthCalculator, templateWriter);
                IExcelImportService importService = new ExcelImportService();

                IStrengthQueryService strengthQueryService = new StrengthQueryService(_unitOfWork, strengthCalculator);
                IPersonnelQueryService personnelQueryService = new PersonnelQueryService(_unitOfWork, statusEngine);
                IAbsenceQueryService absenceQueryService = new AbsenceQueryService(_unitOfWork);
                IServiceRosterQueryService serviceQueryService = new ServiceRosterQueryService(_unitOfWork);

                // 4. Automated Daily Backup Check
                try
                {
                    backupService.PerformDailyAutoBackup();
                }
                catch
                {
                    // Non-blocking auto backup
                }

                // 5. Initialize UI Services
                IFileDialogService fileDialogService = new WpfFileDialogService();
                INotificationService notificationService = new WpfNotificationService();
                IConfirmationService confirmationService = new WpfConfirmationService();
                IPrintService printService = new WpfPrintService();
                IPersonEditorDialogService personEditorDialogService = new WpfPersonEditorDialogService(_unitOfWork, conflictEngine, personnelService);

                // 6. Initialize Navigation & Shell State
                INavigationService navigationService = new Dynamologio.App.Navigation.NavigationService();
                IShellStateService shellStateService = new Dynamologio.App.Navigation.ShellStateService();

                // 6a. Load deployment settings
                var uSetting = _unitOfWork.AppSettings.Find(s => s.Key == "Deployment.UnitName").FirstOrDefault();
                var oSetting = _unitOfWork.AppSettings.Find(s => s.Key == "Deployment.OfficeName").FirstOrDefault();
                string unitName = (uSetting != null && !string.IsNullOrWhiteSpace(uSetting.Value)) ? uSetting.Value : "ΜΟΝΑΔΑ";
                string officeName = (oSetting != null && !string.IsNullOrWhiteSpace(oSetting.Value)) ? oSetting.Value : "ΓΡΑΦΕΙΟ / ΤΜΗΜΑ";
                shellStateService.UpdateDeploymentHeader(unitName, officeName);

                // 6b. Construct ViewModelFactory with a dictionary of delegates
                var factories = new Dictionary<NavigationSection, Func<ViewModelBase>>
                {
                    { NavigationSection.Dashboard, () => new DashboardViewModel(strengthQueryService, clock, navigationService) },
                    { NavigationSection.Dynamologio, () => new DynamologioViewModel(strengthQueryService, personnelQueryService, reportService, clock, shellStateService, printService, fileDialogService, notificationService) },
                    { NavigationSection.Personnel, () => new PersonnelViewModel(personnelQueryService, personnelService, clock, personEditorDialogService, confirmationService) },
                    { NavigationSection.Absences, () => new AbsencesViewModel(personnelQueryService, absenceQueryService, conflictEngine, absenceService, clock, notificationService, confirmationService) },
                    { NavigationSection.Services, () => new ServicesViewModel(personnelQueryService, serviceQueryService, absenceQueryService, conflictEngine, dutyService, clock, notificationService, confirmationService) },
                    { NavigationSection.Reports, () => new ReportsViewModel(personnelQueryService, strengthQueryService, reportService, clock, shellStateService, printService, fileDialogService, notificationService) },
                    { NavigationSection.ImportExport, () => new ImportExportViewModel(_unitOfWork, importService, fileDialogService, notificationService, confirmationService) },
                    { NavigationSection.DataValidation, () => new DataValidationViewModel(_unitOfWork, conflictEngine, statusEngine, clock) },
                    { NavigationSection.History, () => new HistoryViewModel(_unitOfWork, clock) },
                    { NavigationSection.Settings, () => new SettingsViewModel(_unitOfWork, backupService, lifecycleCoordinator, diagnosticService, clock, shellStateService, fileDialogService, notificationService, confirmationService) }
                };

                IViewModelFactory viewModelFactory = new Dynamologio.App.Navigation.ViewModelFactory(factories);

                // 7. Initialize Main View & Coordinator
                var mainViewModel = new MainViewModel(
                    navigationService,
                    viewModelFactory,
                    shellStateService);

                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                mainWindow.Show();
            }
            catch (Exception ex)
            {
                LogUnhandledException(ex, "Startup_Fatal");
                MessageBox.Show(
                    $"Κρίσιμο σφάλμα κατά την εκκίνηση της εφαρμογής:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Σφάλμα Εκκίνησης ΔΥΝΑΜΟΛΟΓΙΟ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(1);
            }
        }

        private static void LogUnhandledException(Exception ex, string source)
        {
            if (ex == null) return;
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (string.IsNullOrEmpty(appData))
                {
                    appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }

                string logFolder = Path.Combine(appData, "Dynamologio", "Logs");
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                string logFile = Path.Combine(logFolder, "app_crash.log");
                string logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}] [{source}] {ex}\n----------------------------------------\n";
                File.AppendAllText(logFile, logEntry);
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _dbContext?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }
    }
}
