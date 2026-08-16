using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.Core.Engines;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;

namespace Dynamologio.App.ViewModels
{
    public class ValidationIssueItem
    {
        public ConflictSeverity Severity { get; set; }
        public string EntityCategory { get; set; }
        public string EntityName { get; set; }
        public string Description { get; set; }
        public string ActionRecommendation { get; set; }

        public string SeverityLabel => Severity == ConflictSeverity.Error ? "ΣΦΑΛΜΑ" : "ΠΡΟΕΙΔΟΠΟΙΗΣΗ";
    }

    public class DataValidationViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IConflictEngine _conflictEngine;
        private readonly IStatusEngine _statusEngine;
        private readonly IClock _clock;
        private readonly MainViewModel _mainVM;

        public ObservableCollection<ValidationIssueItem> IssuesList { get; } = new ObservableCollection<ValidationIssueItem>();

        public int TotalErrors => IssuesList.Count(i => i.Severity == ConflictSeverity.Error);
        public int TotalWarnings => IssuesList.Count(i => i.Severity == ConflictSeverity.Warning);

        public ICommand ScanDataCommand { get; }

        public DataValidationViewModel(
            IUnitOfWork uow,
            IConflictEngine conflictEngine,
            IStatusEngine statusEngine,
            IClock clock,
            MainViewModel mainVM)
        {
            _uow = uow;
            _conflictEngine = conflictEngine;
            _statusEngine = statusEngine;
            _clock = clock ?? SystemClock.Instance;
            _mainVM = mainVM;

            ScanDataCommand = new RelayCommand(ScanData);
        }

        public void ScanData()
        {
            IssuesList.Clear();

            var personnel = _uow.Personnel.GetAll().ToList();
            var ranks = _uow.Ranks.GetAll().ToDictionary(r => r.Id);
            var units = _uow.OrganisationUnits.GetAll().ToDictionary(u => u.Id);
            var events = _uow.StatusEvents.GetAll().ToList();
            var statusTypes = _uow.StatusTypes.GetAll().ToList();
            var services = _uow.ServiceAssignments.GetAll().ToList();

            // 1. Scan Personnel Integrity
            foreach (var p in personnel)
            {
                if (!ranks.ContainsKey(p.RankId))
                {
                    IssuesList.Add(new ValidationIssueItem
                    {
                        Severity = ConflictSeverity.Error,
                        EntityCategory = "Προσωπικό",
                        EntityName = p.FullName,
                        Description = "Μη έγκυρος ή ορφανός κωδικός βαθμού.",
                        ActionRecommendation = "Επεξεργαστείτε το πρόσωπο και επιλέξτε έγκυρο βαθμό."
                    });
                }

                if (!units.ContainsKey(p.OrganisationUnitId))
                {
                    IssuesList.Add(new ValidationIssueItem
                    {
                        Severity = ConflictSeverity.Warning,
                        EntityCategory = "Προσωπικό",
                        EntityName = p.FullName,
                        Description = "Μη έγκυρος ή ορφανός κωδικός μονάδας/λόχου.",
                        ActionRecommendation = "Επεξεργαστείτε το πρόσωπο και αντιστοιχίστε λόχο."
                    });
                }
            }

            // 2. Scan Duplicate ASMs
            var asmGroups = personnel.Where(p => !string.IsNullOrWhiteSpace(p.MilitaryServiceNumber))
                .GroupBy(p => p.MilitaryServiceNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var grp in asmGroups)
            {
                IssuesList.Add(new ValidationIssueItem
                {
                    Severity = ConflictSeverity.Error,
                    EntityCategory = "Προσωπικό",
                    EntityName = $"ΑΣΜ: {grp.Key}",
                    Description = $"Διπλότυπος ΑΣΜ σε {grp.Count()} άτομα ({string.Join(", ", grp.Select(x => x.FullName))}).",
                    ActionRecommendation = "Διορθώστε τον ΑΣΜ στο μητρώο προσωπικού."
                });
            }

            // 3. Scan Overlapping Absences
            var eventsByPerson = events.Where(e => !e.IsCancelled).GroupBy(e => e.PersonnelId);
            foreach (var pEvents in eventsByPerson)
            {
                var p = personnel.FirstOrDefault(x => x.Id == pEvents.Key);
                var evList = pEvents.ToList();

                for (int i = 0; i < evList.Count; i++)
                {
                    for (int j = i + 1; j < evList.Count; j++)
                    {
                        if (StatusIntervalMath.DoIntervalsOverlap(evList[i].StartAt, evList[i].EndAtExclusive, evList[j].StartAt, evList[j].EndAtExclusive))
                        {
                            IssuesList.Add(new ValidationIssueItem
                            {
                                Severity = ConflictSeverity.Error,
                                EntityCategory = "Απουσίες",
                                EntityName = p?.FullName ?? "Άγνωστος",
                                Description = $"Επικάλυψη απουσιών: [{evList[i].StartAt:dd/MM} - {evList[i].EndAtExclusive:dd/MM}] και [{evList[j].StartAt:dd/MM} - {evList[j].EndAtExclusive:dd/MM}].",
                                ActionRecommendation = "Ακυρώστε ή τροποποιήστε το επικαλυπτόμενο διάστημα."
                            });
                        }
                    }
                }
            }

            OnPropertyChanged(nameof(TotalErrors));
            OnPropertyChanged(nameof(TotalWarnings));
        }
    }
}
