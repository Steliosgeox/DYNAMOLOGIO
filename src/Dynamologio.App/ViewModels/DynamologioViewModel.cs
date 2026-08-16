using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Dynamologio.Reporting.Services;
using Microsoft.Win32;

namespace Dynamologio.App.ViewModels
{
    public class DynamologioViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IStrengthCalculator _strengthCalculator;
        private readonly IReportGeneratorService _reportService;
        private readonly IClock _clock;
        private readonly MainViewModel _mainVM;

        private DateTime _selectedDate;
        private OrganisationUnit _selectedUnit;

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    LoadData();
                }
            }
        }

        public OrganisationUnit SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (SetProperty(ref _selectedUnit, value))
                {
                    LoadData();
                }
            }
        }

        public ObservableCollection<OrganisationUnit> UnitsList { get; } = new ObservableCollection<OrganisationUnit>();
        public ObservableCollection<PersonnelStatusSnapshot> PresentList { get; } = new ObservableCollection<PersonnelStatusSnapshot>();
        public ObservableCollection<PersonnelStatusSnapshot> AbsentList { get; } = new ObservableCollection<PersonnelStatusSnapshot>();

        public UnitStrengthSnapshot CurrentSnapshot { get; private set; }

        public ICommand ExportExcelCommand { get; }
        public ICommand PrintReportCommand { get; }

        public DynamologioViewModel(
            IUnitOfWork uow,
            IStrengthCalculator strengthCalculator,
            IReportGeneratorService reportService,
            IClock clock,
            MainViewModel mainVM)
        {
            _uow = uow;
            _strengthCalculator = strengthCalculator;
            _reportService = reportService;
            _clock = clock ?? SystemClock.Instance;
            _mainVM = mainVM;

            _selectedDate = _clock.Today;

            ExportExcelCommand = new RelayCommand(ExportExcel);
            PrintReportCommand = new RelayCommand(PrintReport);
        }

        public void LoadData()
        {
            if (UnitsList.Count == 0)
            {
                UnitsList.Add(new OrganisationUnit { Name = "Όλη η Μονάδα (Όλοι οι Λόχοι)", Id = Guid.Empty });
                foreach (var u in _uow.OrganisationUnits.GetAll().OrderBy(x => x.SortOrder))
                {
                    UnitsList.Add(u);
                }
                _selectedUnit = UnitsList.First();
            }

            Guid? filterUnitId = _selectedUnit != null && _selectedUnit.Id != Guid.Empty ? (Guid?)_selectedUnit.Id : null;

            var p = _uow.Personnel.GetAll();
            var ev = _uow.StatusEvents.GetAll();
            var st = _uow.StatusTypes.GetAll();
            var rk = _uow.Ranks.GetAll();
            var un = _uow.OrganisationUnits.GetAll();
            var sa = _uow.ServiceAssignments.GetAll();
            var sv = _uow.ServiceTypes.GetAll();

            CurrentSnapshot = _strengthCalculator.CalculateSnapshot(p, ev, st, rk, un, sa, sv, SelectedDate, filterUnitId);

            PresentList.Clear();
            foreach (var item in CurrentSnapshot.PresentPersonnel.OrderBy(x => x.Rank?.SortOrder ?? 99))
            {
                PresentList.Add(item);
            }

            AbsentList.Clear();
            foreach (var item in CurrentSnapshot.AbsentPersonnel.OrderBy(x => x.Rank?.SortOrder ?? 99))
            {
                AbsentList.Add(item);
            }

            OnPropertyChanged(nameof(CurrentSnapshot));
        }

        private void ExportExcel()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"ΔΥΝΑΜΟΛΟΓΙΟ_{SelectedDate:yyyyMMdd}.xlsx"
                };

                if (sfd.ShowDialog() == true)
                {
                    var req = new ReportGenerationRequest
                    {
                        AsOfTimestamp = SelectedDate,
                        OrganisationUnitId = _selectedUnit?.Id != Guid.Empty ? (Guid?)_selectedUnit.Id : null,
                        UnitTitle = _selectedUnit?.Name ?? "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ"
                    };

                    _reportService.ExportToExcel(req, sfd.FileName);
                    MessageBox.Show($"Το αρχείο Excel δημιουργήθηκε επιτυχώς:\n{sfd.FileName}", "Εξαγωγή Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα εξαγωγής Excel: {ex.Message}", "Σφάλμα Εξαγωγής", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReport()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var req = new ReportGenerationRequest
                    {
                        AsOfTimestamp = SelectedDate,
                        OrganisationUnitId = _selectedUnit?.Id != Guid.Empty ? (Guid?)_selectedUnit.Id : null,
                        UnitTitle = _selectedUnit?.Name ?? "123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ"
                    };

                    var doc = _reportService.GeneratePrintableDocument(req);
                    IDocumentPaginatorSource dps = doc;
                    printDialog.PrintDocument(dps.DocumentPaginator, "Ημερήσιο Δυναμολόγιο");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα εκτύπωσης: {ex.Message}", "Σφάλμα Εκτύπωσης", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
