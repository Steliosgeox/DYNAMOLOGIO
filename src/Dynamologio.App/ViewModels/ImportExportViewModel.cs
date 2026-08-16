using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Dynamologio.Core.Interfaces;
using Dynamologio.ImportExport.Excel.Import;
using Microsoft.Win32;

namespace Dynamologio.App.ViewModels
{
    public class ImportExportViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IExcelImportService _importService;
        private readonly MainViewModel _mainVM;

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

        public ImportExportViewModel(IUnitOfWork uow, IExcelImportService importService, MainViewModel mainVM)
        {
            _uow = uow;
            _importService = importService ?? new ExcelImportService();
            _mainVM = mainVM;

            BrowseFileCommand = new RelayCommand(BrowseFile);
            AnalyzeFileCommand = new RelayCommand(AnalyzeFile, () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
            CommitImportCommand = new RelayCommand(CommitImport, () => PreviewReport != null && PreviewReport.CanCommit && !IsBusy);
        }

        private void BrowseFile()
        {
            var ofd = new OpenFileDialog
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
                    StatusMessage = $"⚠️ Εντοπίστηκαν {PreviewReport.ErrorCount} σφάλματα επικύρωσης. Η εισαγωγή είναι μπλοκαρισμένη.";
                }
                else
                {
                    StatusMessage = $"Έλεγχος ολοκληρώθηκε: {PreviewReport.NewCount} νέες, {PreviewReport.UpdateCount} ενημερώσεις, {PreviewReport.SkipCount} χωρίς αλλαγή.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα ανάλυσης: {ex.Message}";
                MessageBox.Show(ex.Message, "Σφάλμα Ανάλυσης", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void CommitImport()
        {
            if (PreviewReport == null || !PreviewReport.CanCommit) return;

            var res = MessageBox.Show(
                $"Επιβεβαίωση εκτέλεσης εισαγωγής:\n• {PreviewReport.NewCount} Νέες Εγγραφές\n• {PreviewReport.UpdateCount} Ενημερώσεις Στοιχείων\n\nΣυνέχεια;",
                "Επιβεβαίωση Εισαγωγής",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                StatusMessage = "Εκτέλεση συναλλαγής εισαγωγής στη βάση δεδομένων...";

                var batch = _importService.CommitImport(PreviewReport, _uow);

                MessageBox.Show(
                    $"Η εισαγωγή ολοκληρώθηκε επιτυχώς!\nΑρ. Παρτίδας: {batch.Id}\nΝέες εγγραφές: {batch.InsertedCount}\nΕνημερώσεις: {batch.UpdatedCount}",
                    "Επιτυχής Εισαγωγή",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                StatusMessage = "Η εισαγωγή ολοκληρώθηκε επιτυχώς.";
                PreviewReport = null;
                PreviewRows.Clear();
                SelectedFilePath = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Σφάλμα εκτέλεσης εισαγωγής: {ex.Message}";
                MessageBox.Show(ex.Message, "Σφάλμα Εισαγωγής", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
