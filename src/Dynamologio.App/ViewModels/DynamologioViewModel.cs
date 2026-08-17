using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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

        public ObservableCollection<OrganisationUnit> OrganisationUnitsList { get; } = new ObservableCollection<OrganisationUnit>();

        private DateTime _selectedDate;
        private OrganisationUnit _selectedUnit;
        private UnitStrengthSnapshot _currentSnapshot;
        private string _templateStatusText = "Πρότυπο: Επαληθευμένο";
        private Brush _templateStatusBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105));
        private Brush _templateStatusBgBrush = new SolidColorBrush(Color.FromRgb(236, 253, 245));
        private Brush _templateStatusBorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208));

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    RefreshCalculations();
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
                    RefreshCalculations();
                }
            }
        }

        public UnitStrengthSnapshot CurrentSnapshot
        {
            get => _currentSnapshot;
            set => SetProperty(ref _currentSnapshot, value);
        }

        public string TemplateStatusText
        {
            get => _templateStatusText;
            set => SetProperty(ref _templateStatusText, value);
        }

        public Brush TemplateStatusBrush
        {
            get => _templateStatusBrush;
            set => SetProperty(ref _templateStatusBrush, value);
        }

        public Brush TemplateStatusBgBrush
        {
            get => _templateStatusBgBrush;
            set => SetProperty(ref _templateStatusBgBrush, value);
        }

        public Brush TemplateStatusBorderBrush
        {
            get => _templateStatusBorderBrush;
            set => SetProperty(ref _templateStatusBorderBrush, value);
        }

        public ICommand SetTodayCommand { get; }
        public ICommand SetTomorrowCommand { get; }
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

            SetTodayCommand = new RelayCommand(_ => SelectedDate = _clock.Today);
            SetTomorrowCommand = new RelayCommand(_ => SelectedDate = _clock.Today.AddDays(1));
            ExportExcelCommand = new RelayCommand(ExportToExcel);
            PrintReportCommand = new RelayCommand(PrintReport);
        }

        public void LoadData()
        {
            OrganisationUnitsList.Clear();
            OrganisationUnitsList.Add(new OrganisationUnit { Name = "Όλη η Μονάδα (Συνολικό)", Id = Guid.Empty });
            foreach (var u in _uow.OrganisationUnits.GetAll().OrderBy(x => x.SortOrder))
            {
                OrganisationUnitsList.Add(u);
            }
            if (SelectedUnit == null) SelectedUnit = OrganisationUnitsList.First();

            CheckTemplateStatus();
            RefreshCalculations();
        }

        private void CheckTemplateStatus()
        {
            var status = _reportService.CheckTemplateStatus(out _, out _, out _);
            switch (status)
            {
                case TemplateVerificationStatus.Verified:
                    TemplateStatusText = "Πρότυπο: Επαληθευμένο (SHA-256)";
                    TemplateStatusBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105)); // Green
                    TemplateStatusBgBrush = new SolidColorBrush(Color.FromRgb(236, 253, 245));
                    TemplateStatusBorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208));
                    break;
                case TemplateVerificationStatus.ShaMismatch:
                    TemplateStatusText = "Πρότυπο: Ασυμφωνία SHA-256";
                    TemplateStatusBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red
                    TemplateStatusBgBrush = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                    TemplateStatusBorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));
                    break;
                case TemplateVerificationStatus.Missing:
                default:
                    TemplateStatusText = "Πρότυπο: Μη Διαθέσιμο";
                    TemplateStatusBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // Amber
                    TemplateStatusBgBrush = new SolidColorBrush(Color.FromRgb(255, 251, 235));
                    TemplateStatusBorderBrush = new SolidColorBrush(Color.FromRgb(253, 230, 138));
                    break;
            }
        }

        private void RefreshCalculations()
        {
            Guid? filterUnitId = SelectedUnit != null && SelectedUnit.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null;

            var p = _uow.Personnel.GetAll();
            var ev = _uow.StatusEvents.GetAll();
            var st = _uow.StatusTypes.GetAll();
            var rk = _uow.Ranks.GetAll();
            var un = _uow.OrganisationUnits.GetAll();
            var sa = _uow.ServiceAssignments.GetAll();
            var sv = _uow.ServiceTypes.GetAll();

            CurrentSnapshot = _strengthCalculator.CalculateSnapshot(p, ev, st, rk, un, sa, sv, SelectedDate, filterUnitId);
        }

        private void ExportToExcel()
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
                        Type = ReportType.DailyDynamologio,
                        AsOfTimestamp = SelectedDate,
                        OrganisationUnitId = SelectedUnit?.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null,
                        UnitTitle = _mainVM.UnitNameHeader
                    };

                    _reportService.ExportToExcel(req, sfd.FileName);
                    MessageBox.Show($"Το αρχείο Excel εξήχθη επιτυχώς:\n{sfd.FileName}", "Εξαγωγή Δυναμολογίου", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα εξαγωγής: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        Type = ReportType.DailyDynamologio,
                        AsOfTimestamp = SelectedDate,
                        OrganisationUnitId = SelectedUnit?.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null,
                        UnitTitle = _mainVM.UnitNameHeader
                    };

                    var doc = _reportService.GeneratePrintableDocument(req);
                    IDocumentPaginatorSource dps = doc;
                    printDialog.PrintDocument(dps.DocumentPaginator, "Ημερήσιο Δυναμολόγιο");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα εκτύπωσης: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
