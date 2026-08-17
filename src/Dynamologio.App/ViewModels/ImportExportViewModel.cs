using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Interfaces;
using Dynamologio.ImportExport.Excel.Import;

namespace Dynamologio.App.ViewModels
{
    public class ImportExportViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IUnitOfWork _uow;
        private readonly IExcelImportService _importService;

        private string _selectedFilePath = string.Empty;
        private ImportPreviewReport _previewReport;
        private string _statusMessage = string.Empty;
        private bool _isBusy;

        public string SelectedFilePath { get => _selectedFilePath; set => SetProperty(ref _selectedFilePath, value); }
        public ImportPreviewReport PreviewReport { get => _previewReport; set => SetProperty(ref _previewReport, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ObservableCollection<ImportRowPreview> PreviewRows { get; } = new ObservableCollection<ImportRowPreview>();

        public ICommand BrowseFileCommand { get; }
        public ICommand AnalyzeFileCommand { get; }
        public ICommand CommitImportCommand { get; }

        public ImportExportViewModel(IUnitOfWork uow, IExcelImportService importService)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));

            BrowseFileCommand = new RelayCommand(BrowseFile);
            AnalyzeFileCommand = new RelayCommand(AnalyzeFile, () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
            CommitImportCommand = new RelayCommand(CommitImport, () => PreviewReport != null && PreviewReport.CanCommit && !IsBusy);
        }

        public void Activate()
        {
            // ImportExport doesn't need auto-reload on navigation
        }

        private void BrowseFile()
        {
            // Will be moved to IFileDialogService in Commit 2
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls",
                Title = "Επιλογή Αρχείου Excel Προσωπικού"
            };

            if (ofd.ShowDialog() == true)
            {
                SelectedFilePath = ofd.FileName;
                AnalyzeFile();
            }
        }

        public void AnalyzeFile()
        {
            if (string.IsNullOrWhiteSpace(SelectedFilePath)) return;

            try
            {
                IsBusy = true;
                StatusMessage = "Ανάλυση αρχείου Excel και έλεγχος αντιστοιχιών...";

                PreviewReport = _importService.AnalyzeAndPreviewImport(SelectedFilePath, _uow);
                PreviewRows.Clear();
                foreach (var row in PreviewReport.Rows)
                {
                    PreviewRows.Add(row);
                }

                if (PreviewReport.ErrorCount > 0)
                {
                    StatusMessage = $"Εντοπίστηκαν {PreviewReport.ErrorCount} σφάλματα επικύρωσης. Η εισαγωγή είναι μπλοκαρισμένη.";
                }
                else
                {
                    StatusMessage = $"Έλεγχος ολοκληρώθηκε: {PreviewReport.NewCount} νέες, {PreviewReport.UpdateCount} ενημερώσεις, {PreviewReport.SkipCount} χωρίς αλλαγή.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα ανάλυσης: {ex.Message}";
                // Will be moved to INotificationService in Commit 2
                System.Windows.MessageBox.Show(ex.Message, "Σφάλμα Ανάλυσης", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void CommitImport()
        {
            if (PreviewReport == null || !PreviewReport.CanCommit) return;

            // Will be moved to IConfirmationService in Commit 2
            var res = System.Windows.MessageBox.Show(
                $"Επιβεβαίωση εκτέλεσης εισαγωγής:\n• {PreviewReport.NewCount} Νέες Εγγραφές\n• {PreviewReport.UpdateCount} Ενημερώσεις Στοιχείων\n\nΣυνέχεια;",
                "Επιβεβαίωση Εισαγωγής",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (res != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                StatusMessage = "Εκτέλεση συναλλαγής εισαγωγής στη βάση δεδομένων...";

                var batch = _importService.CommitImport(PreviewReport, _uow);

                System.Windows.MessageBox.Show(
                    $"Η εισαγωγή ολοκληρώθηκε επιτυχώς!\nΑρ. Παρτίδας: {batch.Id}\nΝέες εγγραφές: {batch.InsertedCount}\nΕνημερώσεις: {batch.UpdatedCount}",
                    "Επιτυχής Εισαγωγή",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                StatusMessage = "Η εισαγωγή ολοκληρώθηκε επιτυχώς.";
                PreviewReport = null;
                PreviewRows.Clear();
                SelectedFilePath = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα εκτέλεσης εισαγωγής: {ex.Message}";
                System.Windows.MessageBox.Show(ex.Message, "Σφάλμα Εισαγωγής", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
