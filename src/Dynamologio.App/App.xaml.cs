using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using Dynamologio.App.ViewModels;
using Dynamologio.App.Views;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Interfaces;
using Dynamologio.ImportExport.Excel;
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
                IAuditService auditService = new AuditService(_unitOfWork);
                IBackupService backupService = new BackupService(_unitOfWork, _dbContext.DbFilePath, keyProvider);
                IDatabaseLifecycleCoordinator lifecycleCoordinator = new DatabaseLifecycleCoordinator(_dbContext, backupService, keyProvider);
                IDiagnosticPackageService diagnosticService = new DiagnosticPackageService(_unitOfWork, _dbContext.DbFilePath);

                IPersonnelService personnelService = new PersonnelService(_unitOfWork, auditService);
                IAbsenceService absenceService = new AbsenceService(_unitOfWork, auditService);
                IDutyService dutyService = new DutyService(_unitOfWork, auditService);

                IExcelTemplateWriter templateWriter = new NpoiTemplateWriter();
                IReportGeneratorService reportService = new ReportGeneratorService(_unitOfWork, strengthCalculator, templateWriter);
                IExcelImportService importService = new ExcelImportService();

                // 4. Automated Daily Backup Check
                try
                {
                    backupService.PerformDailyAutoBackup();
                }
                catch
                {
                    // Non-blocking auto backup
                }

                // 5. Initialize Main View & Coordinator
                var mainViewModel = new MainViewModel(
                    _unitOfWork,
                    statusEngine,
                    strengthCalculator,
                    conflictEngine,
                    reportService,
                    importService,
                    backupService,
                    lifecycleCoordinator,
                    diagnosticService,
                    auditService,
                    clock,
                    personnelService,
                    absenceService,
                    dutyService);

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
