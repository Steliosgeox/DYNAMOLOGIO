using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Infrastructure.Services;
using Microsoft.Win32;

namespace Dynamologio.App.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IBackupService _backupService;
        private readonly IDiagnosticPackageService _diagnosticService;
        private readonly IClock _clock;
        private readonly MainViewModel _mainVM;

        private string _unitName = "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ";
        private string _officeName = "1ο ΓΡΑΦΕΙΟ";
        private string _statusMessage = string.Empty;

        public string UnitName { get => _unitName; set => SetProperty(ref _unitName, value); }
        public string OfficeName { get => _officeName; set => SetProperty(ref _officeName, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public ICommand SaveUnitSettingsCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand ExportDiagnosticsCommand { get; }

        public SettingsViewModel(
            IUnitOfWork uow,
            IBackupService backupService,
            IDiagnosticPackageService diagnosticService,
            IClock clock,
            MainViewModel mainVM)
        {
            _uow = uow;
            _backupService = backupService;
            _diagnosticService = diagnosticService;
            _clock = clock ?? SystemClock.Instance;
            _mainVM = mainVM;

            SaveUnitSettingsCommand = new RelayCommand(SaveUnitSettings);
            CreateBackupCommand = new RelayCommand(CreateBackup);
            RestoreBackupCommand = new RelayCommand(RestoreBackup);
            ExportDiagnosticsCommand = new RelayCommand(ExportDiagnostics);
        }

        public void LoadData()
        {
            var uSetting = _uow.AppSettings.Find(s => s.Key == "Deployment.UnitName").FirstOrDefault();
            if (uSetting != null) UnitName = uSetting.Value;

            var oSetting = _uow.AppSettings.Find(s => s.Key == "Deployment.OfficeName").FirstOrDefault();
            if (oSetting != null) OfficeName = oSetting.Value;
        }

        private void SaveUnitSettings()
        {
            SaveOrUpdateSetting("Deployment.UnitName", UnitName);
            SaveOrUpdateSetting("Deployment.OfficeName", OfficeName);

            _mainVM.UpdateDeploymentHeader(UnitName, OfficeName);
            StatusMessage = "Οι ρυθμίσεις μονάδας αποθηκεύτηκαν επιτυχώς.";
            MessageBox.Show("Οι ρυθμίσεις μονάδας ενημερώθηκαν.", "Ρυθμίσεις", MessageBoxButton.OK, MessageBoxImage.Information);
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
                var sfd = new SaveFileDialog
                {
                    Filter = "Dynamologio Backup (*.zip)|*.zip",
                    FileName = $"Dynamologio_Backup_{_clock.Now:yyyyMMdd_HHmm}.zip"
                };

                if (sfd.ShowDialog() == true)
                {
                    string targetDir = Path.GetDirectoryName(sfd.FileName);
                    var manifest = _backupService.CreateBackup(targetDir);

                    // Move/copy to selected file if name differs
                    string defaultZip = Path.Combine(targetDir, $"Dynamologio_Backup_{manifest.Timestamp:yyyyMMdd_HHmmss}.zip");
                    if (File.Exists(defaultZip) && defaultZip != sfd.FileName)
                    {
                        File.Copy(defaultZip, sfd.FileName, true);
                    }

                    StatusMessage = $"Αντίγραφο ασφαλείας δημιουργήθηκε επιτυχώς (SHA-256: {manifest.DatabaseSha256.Substring(0, 8)}...).";
                    MessageBox.Show($"Το αντίγραφο ασφαλείας δημιουργήθηκε επιτυχώς:\n{sfd.FileName}", "Αντίγραφο Ασφαλείας", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα αντιγράφου ασφαλείας: {ex.Message}";
                MessageBox.Show(ex.Message, "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreBackup()
        {
            try
            {
                var ofd = new OpenFileDialog
                {
                    Filter = "Dynamologio Backup (*.zip)|*.zip",
                    Title = "Επιλογή Αντιγράφου Ασφαλείας προς Επαναφορά"
                };

                if (ofd.ShowDialog() == true)
                {
                    var confirm = MessageBox.Show(
                        "ΠΡΟΣΟΧΗ: Η επαναφορά θα αντικαταστήσει όλα τα τρέχοντα δεδομένα της βάσης με τα δεδομένα του αντιγράφου.\n\nΣυνέχεια;",
                        "Επιβεβαίωση Επαναφοράς",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirm == MessageBoxResult.Yes)
                    {
                        _backupService.RestoreBackup(ofd.FileName);
                        MessageBox.Show("Η επαναφορά ολοκληρώθηκε επιτυχώς! Η εφαρμογή θα ανανεώσει τα δεδομένα της.", "Επιτυχής Επαναφορά", MessageBoxButton.OK, MessageBoxImage.Information);
                        _mainVM.RefreshCurrentView();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα επαναφοράς: {ex.Message}";
                MessageBox.Show(ex.Message, "Σφάλμα Επαναφοράς", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportDiagnostics()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "ZIP Archive (*.zip)|*.zip",
                    FileName = $"Dynamologio_Diagnostics_{_clock.Now:yyyyMMdd}.zip"
                };

                if (sfd.ShowDialog() == true)
                {
                    _diagnosticService.ExportDiagnosticPackage(sfd.FileName);
                    MessageBox.Show($"Το πακέτο διαγνωστικών εξήχθη επιτυχώς:\n{sfd.FileName}", "Διαγνωστικά", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα εξαγωγής διαγνωστικών: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
