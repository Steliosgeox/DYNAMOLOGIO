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

            // 1. Enforce el-GR culture throughout application runtime
            var greekCulture = new CultureInfo("el-GR");
            Thread.CurrentThread.CurrentCulture = greekCulture;
            Thread.CurrentThread.CurrentUICulture = greekCulture;

            try
            {
                // 2. Initialize Database & Run Schema Migrations
                _dbContext = new LiteDbContext();
                _unitOfWork = new LiteDbUnitOfWork(_dbContext);

                var migrationRunner = new SchemaMigrationRunner(_unitOfWork);
                migrationRunner.RunMigrations();

                // 3. Composition Root - Construct domain engines and services
                IClock clock = SystemClock.Instance;
                IStatusEngine statusEngine = new StatusEngine();
                IStrengthCalculator strengthCalculator = new StrengthCalculationEngine(statusEngine);
                IConflictEngine conflictEngine = new ConflictEngine();
                IAuditService auditService = new AuditService(_unitOfWork);
                IBackupService backupService = new BackupService(_unitOfWork, _dbContext.DbFilePath);
                IDiagnosticPackageService diagnosticService = new DiagnosticPackageService(_unitOfWork, _dbContext.DbFilePath);

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
                    diagnosticService,
                    auditService,
                    clock);

                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Κρίσιμο σφάλμα κατά την εκκίνηση της εφαρμογής:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Σφάλμα Εκκίνησης ΔΥΝΑΜΟΛΟΓΙΟ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _unitOfWork?.Dispose();
            _dbContext?.Dispose();
            base.OnExit(e);
        }
    }
}
