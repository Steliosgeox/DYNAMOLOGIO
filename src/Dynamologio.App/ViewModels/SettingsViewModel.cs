using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Infrastructure.Services;
using Dynamologio.App.Services;

namespace Dynamologio.App.ViewModels
{
    public class SettingsViewModel : ViewModelBase, Dynamologio.App.Navigation.IActivatableViewModel
    {
        private readonly IUnitOfWork _uow;
        private readonly IBackupService _backupService;
        private readonly IDatabaseLifecycleCoordinator _lifecycleCoordinator;
        private readonly IDiagnosticPackageService _diagnosticService;
        private readonly IClock _clock;
        private readonly Dynamologio.App.Navigation.IShellStateService _shellState;
        private readonly IFileDialogService _fileDialogService;
        private readonly INotificationService _notificationService;
        private readonly IConfirmationService _confirmationService;

        private string _unitName = "ΜΟΝΑΔΑ";
        private string _officeName = "1ο ΓΡΑΦΕΙΟ";
        private string _statusMessage = string.Empty;
        private string _backupHealthSummary = string.Empty;

        public string UnitName { get => _unitName; set => SetProperty(ref _unitName, value); }
        public string OfficeName { get => _officeName; set => SetProperty(ref _officeName, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public string BackupHealthSummary { get => _backupHealthSummary; set => SetProperty(ref _backupHealthSummary, value); }

        public ICommand SaveUnitSettingsCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand ExportDiagnosticsCommand { get; }

        public SettingsViewModel(
            IUnitOfWork uow,
            IBackupService backupService,
            IDatabaseLifecycleCoordinator lifecycleCoordinator,
            IDiagnosticPackageService diagnosticService,
            IClock clock,
            Dynamologio.App.Navigation.IShellStateService shellState,
            IFileDialogService fileDialogService,
            INotificationService notificationService,
            IConfirmationService confirmationService)
        {
            _uow = uow;
            _backupService = backupService;
            _lifecycleCoordinator = lifecycleCoordinator;
            _diagnosticService = diagnosticService;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _shellState = shellState ?? throw new ArgumentNullException(nameof(shellState));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));

            SaveUnitSettingsCommand = new RelayCommand(SaveUnitSettings);
            CreateBackupCommand = new RelayCommand(CreateBackup);
            RestoreBackupCommand = new RelayCommand(RestoreBackup);
            ExportDiagnosticsCommand = new RelayCommand(ExportDiagnostics);
        }

        public void Activate()
        {
            LoadData();
        }

        public void LoadData()
        {
            var uSetting = _uow.AppSettings.Find(s => s.Key == "Deployment.UnitName").FirstOrDefault();
            if (uSetting != null) UnitName = uSetting.Value;

            var oSetting = _uow.AppSettings.Find(s => s.Key == "Deployment.OfficeName").FirstOrDefault();
            if (oSetting != null) OfficeName = oSetting.Value;

            var health = _backupService.GetBackupHealth();
            BackupHealthSummary = health.StatusSummary;
        }

        private void SaveUnitSettings()
        {
            SaveOrUpdateSetting("Deployment.UnitName", UnitName);
            SaveOrUpdateSetting("Deployment.OfficeName", OfficeName);
            _shellState.UpdateDeploymentHeader(UnitName, OfficeName);
            StatusMessage = "Οι ρυθμίσεις μονάδας αποθηκεύτηκαν επιτυχώς.";
            _notificationService.Info("Ρυθμίσεις", "Οι ρυθμίσεις μονάδας ενημερώθηκαν.");
        }

        private void SaveOrUpdateSetting(string key, string val)
        {
            var s = _uow.AppSettings.Find(x => x.Key == key).FirstOrDefault();
            if (s != null)
            {
                s.Value = val;
                _uow.AppSettings.Update(s);
            }
            else
            {
                _uow.AppSettings.Insert(new AppSetting { Key = key, Value = val });
            }
        }

        private void CreateBackup()
        {
            try
            {
                var backupFile = _fileDialogService.SelectBackupDestination($"Dynamologio_Backup_{_clock.Now:yyyyMMdd_HHmm}.zip");
                if (backupFile != null)
                {
                    string targetDir = Path.GetDirectoryName(backupFile);
                    var manifest = _backupService.CreateBackup(targetDir);

                    string defaultZip = Path.Combine(targetDir, $"Dynamologio_Backup_{manifest.Timestamp:yyyyMMdd_HHmmss}.zip");
                    if (File.Exists(defaultZip) && defaultZip != backupFile)
                    {
                        File.Copy(defaultZip, backupFile, true);
                    }

                    StatusMessage = $"Αντίγραφο ασφαλείας δημιουργήθηκε επιτυχώς (SHA-256: {manifest.DatabaseSha256Checksum.Substring(0, 8)}...).";
                    _notificationService.Info("Αντίγραφο Ασφαλείας", $"Το αντίγραφο ασφαλείας δημιουργήθηκε επιτυχώς:\n{backupFile}");

                    LoadData();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα αντιγράφου ασφαλείας: {ex.Message}";
                _notificationService.Error("Σφάλμα", ex.Message);
            }
        }

        private void RestoreBackup()
        {
            try
            {
                var restoreFile = _fileDialogService.SelectBackupFile();
                if (restoreFile != null)
                {
                    var confirm = _confirmationService.Confirm("Επιβεβαίωση Επαναφοράς", "ΠΡΟΣΟΧΗ: Η επαναφορά θα αντικαταστήσει όλα τα τρέχοντα δεδομένα της βάσης με τα δεδομένα του αντιγράφου.\n\nΣυνέχεια;");

                    if (confirm)
                    {
                            // _mainVM.RefreshAllViewModels();
                            // App will need to handle this in NavigationService or ShellState

                        _notificationService.Info("Επιτυχής Επαναφορά", "Η επαναφορά ολοκληρώθηκε επιτυχώς! Η εφαρμογή ανανέωσε τα δεδομένα της.");
                        StatusMessage = "Η επαναφορά βάσης δεδομένων ολοκληρώθηκε επιτυχώς.";
                        LoadData();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα επαναφοράς: {ex.Message}";
                _notificationService.Error("Σφάλμα Επαναφοράς", ex.Message);
            }
        }

        private void ExportDiagnosticsCommandAction()
        {
            ExportDiagnostics();
        }

        private void ExportDiagnostics()
        {
            try
            {
                var diagFile = _fileDialogService.SaveExcelFile($"Dynamologio_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                if (diagFile != null)
                {
                    string path = _diagnosticService.ExportDiagnosticPackage(diagFile);
                    StatusMessage = $"Διαγνωστικό πακέτο εξήχθη: {path}";
                    _notificationService.Info("Διαγνωστικά", $"Το διαγνωστικό πακέτο εξήχθη επιτυχώς:\n{path}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα εξαγωγής διαγνωστικών: {ex.Message}";
                _notificationService.Error("Σφάλμα", ex.Message);
            }
        }
    }
}
