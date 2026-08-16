using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Dynamologio.App.ViewModels;
using Dynamologio.App.Views;
using Dynamologio.Infrastructure.LiteDb;
using Dynamologio.Infrastructure.Migrations;
using Dynamologio.Infrastructure.Repositories;
using Dynamologio.Infrastructure.Services;

namespace Dynamologio.App
{
    public partial class App : Application
    {
        private LiteDbContext _dbContext;
        private LiteDbUnitOfWork _uow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Enforce el-GR Localization & Culture
            var elCulture = new CultureInfo("el-GR");
            Thread.CurrentThread.CurrentCulture = elCulture;
            Thread.CurrentThread.CurrentUICulture = elCulture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            try
            {
                // 2. Initialize Database & Repositories
                _dbContext = new LiteDbContext();
                _uow = new LiteDbUnitOfWork(_dbContext);

                // 3. Apply Schema Migrations and Baseline Seeds
                SchemaMigrationRunner.ApplyMigrations(_uow);

                // 4. Trigger Daily Rotating Auto-Backup
                var backupService = new BackupService(_dbContext.DatabasePath, _uow);
                backupService.PerformDailyAutoBackup();

                // 5. Bootstrap Main Window & ViewModels
                var mainVM = new MainViewModel(_uow, _dbContext.DatabasePath);
                var mainWindow = new MainWindow(mainVM);
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Σφάλμα κατά την εκκίνηση της εφαρμογής:\n{ex.Message}\n\nΗ εφαρμογή θα τερματιστεί.",
                    "Κρίσιμο Σφάλμα",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _uow?.Dispose();
            _dbContext?.Dispose();
            base.OnExit(e);
        }
    }
}
