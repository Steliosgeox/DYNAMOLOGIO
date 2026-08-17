using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Dynamologio.Reporting.Services;
using Dynamologio.App.Services;

namespace Dynamologio.App.ViewModels
{
    public class ReportCatalogItem
    {
        public ReportType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string TargetScope { get; set; }
    }

    public class ReportsViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IPersonnelQueryService _personnelQueryService;
        private readonly IStrengthQueryService _strengthQueryService;
        private readonly IReportGeneratorService _reportService;
        private readonly IClock _clock;
        private readonly IShellStateService _shellState;
        private readonly IPrintService _printService;
        private readonly IFileDialogService _fileDialogService;
        private readonly INotificationService _notificationService;

        public ObservableCollection<ReportCatalogItem> AvailableReports { get; } = new ObservableCollection<ReportCatalogItem>();
        public ObservableCollection<OrganisationUnit> UnitsList { get; } = new ObservableCollection<OrganisationUnit>();

        private ReportCatalogItem _selectedReport;
        private DateTime _selectedDate;
        private OrganisationUnit _selectedUnit;
        private UnitStrengthSnapshot _previewSnapshot;
        private string _statusMessage = string.Empty;

        public ReportCatalogItem SelectedReport
        {
            get => _selectedReport;
            set
            {
                if (SetProperty(ref _selectedReport, value))
                {
                    GeneratePreview();
                }
            }
        }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    GeneratePreview();
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
                    GeneratePreview();
                }
            }
        }

        public UnitStrengthSnapshot PreviewSnapshot
        {
            get => _previewSnapshot;
            set => SetProperty(ref _previewSnapshot, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand ExportExcelCommand { get; }
        public ICommand PrintReportCommand { get; }
        public ICommand RefreshPreviewCommand { get; }

        public ReportsViewModel(
            IPersonnelQueryService personnelQueryService,
            IStrengthQueryService strengthQueryService,
            IReportGeneratorService reportService,
            IClock clock,
            IShellStateService shellState,
            IPrintService printService,
            IFileDialogService fileDialogService,
            INotificationService notificationService)
        {
            _personnelQueryService = personnelQueryService ?? throw new ArgumentNullException(nameof(personnelQueryService));
            _strengthQueryService = strengthQueryService ?? throw new ArgumentNullException(nameof(strengthQueryService));
            _reportService = reportService;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _shellState = shellState ?? throw new ArgumentNullException(nameof(shellState));
            _printService = printService ?? throw new ArgumentNullException(nameof(printService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

            _selectedDate = _clock.Today;

            AvailableReports.Add(new ReportCatalogItem
            {
                Type = ReportType.DailyDynamologio,
                Title = "Ημερήσιο Δυναμολόγιο Μονάδος",
                Description = "Επίσημη αναφορά υπαρχούσης δύναμης, παρόντων, απόντων και αιτιολογιών απουσίας.",
                TargetScope = "Συνολικό / Ανά Λόχο"
            });

            AvailableReports.Add(new ReportCatalogItem
            {
                Type = ReportType.AbsentPersonnel,
                Title = "Ονομαστική Κατάσταση Απόντων",
                Description = "Αναλυτικός πίνακας στελεχών και οπλιτών που απουσιάζουν (ΚΑ, ΑΑ, ΦΠ, Νοσηλεία κ.λπ.) με ημερομηνίες επιστροφής.",
                TargetScope = "Μόνο Απόντες"
            });

            AvailableReports.Add(new ReportCatalogItem
            {
                Type = ReportType.PresentPersonnel,
                Title = "Ονομαστική Κατάσταση Παρόντων",
                Description = "Πίνακας διαθέσιμων στελεχών και οπλιτών για ανάληψη υπηρεσιών και επιχειρησιακών καθηκόντων.",
                TargetScope = "Μόνο Παρόντες"
            });

            AvailableReports.Add(new ReportCatalogItem
            {
                Type = ReportType.ServiceRoster,
                Title = "Ημερήσια Διαταγή & Πρόγραμμα Υπηρεσιών",
                Description = "Πρόγραμμα εκτέλεσης υπηρεσιών (ΑΥ, ΒΑΥ, Επόπτες, Σκοποί, Περίπολοι) για την επιλεγμένη ημερομηνία.",
                TargetScope = "Υπηρεσίες Ημέρας"
            });

            _selectedReport = AvailableReports.First();

            ExportExcelCommand = new RelayCommand(ExportExcel);
            PrintReportCommand = new RelayCommand(PrintReport);
            RefreshPreviewCommand = new RelayCommand(GeneratePreview);
        }

        public void Activate()
        {
            LoadData();
        }

        public void LoadData()
        {
            UnitsList.Clear();
            UnitsList.Add(new OrganisationUnit { Name = "Όλη η Μονάδα (Όλοι οι Λόχοι)", Id = Guid.Empty });
            foreach (var u in _personnelQueryService.GetAllUnits().OrderBy(x => x.SortOrder))
            {
                UnitsList.Add(u);
            }
            if (SelectedUnit == null) SelectedUnit = UnitsList.First();

            GeneratePreview();
        }

        public void GeneratePreview()
        {
            if (SelectedReport == null) return;

            Guid? filterUnitId = SelectedUnit != null && SelectedUnit.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null;

            PreviewSnapshot = _strengthQueryService.GetStrengthSnapshot(SelectedDate, filterUnitId);
            StatusMessage = $"Προεπισκόπηση ενημερώθηκε για: {SelectedReport.Title} ({SelectedDate:dd/MM/yyyy})";
        }

        private void ExportExcel()
        {
            try
            {
                var sfd = _fileDialogService.SaveExcelFile($"{SelectedReport.Type}_{SelectedDate:yyyyMMdd}.xlsx");

                if (sfd != null)
                {
                    var req = new ReportGenerationRequest
                    {
                        Type = SelectedReport.Type,
                        AsOfTimestamp = SelectedDate,
                        OrganisationUnitId = SelectedUnit?.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null,
                        UnitTitle = _shellState.UnitName
                    };

                    _reportService.ExportToExcel(req, sfd);
                    _notificationService.Info("Εξαγωγή Αναφοράς", $"Το αρχείο Excel δημιουργήθηκε επιτυχώς:\n{sfd}");
                }
            }
            catch (Exception ex)
            {
                _notificationService.Error("Σφάλμα Εξαγωγής", $"Σφάλμα εξαγωγής αναφοράς: {ex.Message}");
            }
        }

        private void PrintReport()
        {
            try
            {
                var req = new ReportGenerationRequest
                {
                    Type = SelectedReport.Type,
                    AsOfTimestamp = SelectedDate,
                    OrganisationUnitId = SelectedUnit?.Id != Guid.Empty ? (Guid?)SelectedUnit.Id : null,
                    UnitTitle = _shellState.UnitName
                };

                var doc = _reportService.GeneratePrintableDocument(req);
                _printService.PrintDocument(doc, SelectedReport.Title);
            }
            catch (Exception ex)
            {
                _notificationService.Error("Σφάλμα Εκτύπωσης", $"Σφάλμα εκτύπωσης: {ex.Message}");
            }
        }
    }
}
