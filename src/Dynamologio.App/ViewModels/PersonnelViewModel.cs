using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dynamologio.App.Views;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Dynamologio.Infrastructure.Services;

namespace Dynamologio.App.ViewModels
{
    public class PersonnelViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IStatusEngine _statusEngine;
        private readonly IConflictEngine _conflictEngine;
        private readonly IPersonnelService _personnelService;
        private readonly IClock _clock;
        private readonly MainViewModel _mainVM;

        private string _searchText = string.Empty;
        private string _selectedCategoryFilter = "Όλοι";
        private PersonnelStatusSnapshot _selectedPerson;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterPersonnel();
                }
            }
        }

        public string SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (SetProperty(ref _selectedCategoryFilter, value))
                {
                    FilterPersonnel();
                }
            }
        }

        public PersonnelStatusSnapshot SelectedPerson
        {
            get => _selectedPerson;
            set
            {
                if (SetProperty(ref _selectedPerson, value))
                {
                    LoadSelectedPersonDetails();
                }
            }
        }

        public ObservableCollection<PersonnelStatusSnapshot> DisplayPersonnel { get; } = new ObservableCollection<PersonnelStatusSnapshot>();
        public ObservableCollection<StatusEvent> SelectedPersonAbsenceHistory { get; } = new ObservableCollection<StatusEvent>();
        public ObservableCollection<ServiceAssignment> SelectedPersonServiceHistory { get; } = new ObservableCollection<ServiceAssignment>();

        private List<PersonnelStatusSnapshot> _allSnapshots = new List<PersonnelStatusSnapshot>();

        public ICommand AddPersonCommand { get; }
        public ICommand EditPersonCommand { get; }
        public ICommand ArchivePersonCommand { get; }

        public PersonnelViewModel(
            IUnitOfWork uow,
            IStatusEngine statusEngine,
            IConflictEngine conflictEngine,
            IPersonnelService personnelService,
            IClock clock,
            MainViewModel mainVM)
        {
            _uow = uow;
            _statusEngine = statusEngine;
            _conflictEngine = conflictEngine;
            _personnelService = personnelService;
            _clock = clock ?? SystemClock.Instance;
            _mainVM = mainVM;

            AddPersonCommand = new RelayCommand(AddPerson);
            EditPersonCommand = new RelayCommand(EditPerson, () => SelectedPerson != null);
            ArchivePersonCommand = new RelayCommand(ArchivePerson, () => SelectedPerson != null);
        }

        public void LoadData()
        {
            var personnel = _uow.Personnel.GetAll().ToList();
            var ranks = _uow.Ranks.GetAll().ToDictionary(r => r.Id);
            var units = _uow.OrganisationUnits.GetAll().ToDictionary(u => u.Id);
            var events = _uow.StatusEvents.GetAll().GroupBy(e => e.PersonnelId).ToDictionary(g => g.Key, g => g.ToList());
            var statusTypes = _uow.StatusTypes.GetAll().ToList();
            var services = _uow.ServiceAssignments.GetAll().GroupBy(s => s.PersonnelId).ToDictionary(g => g.Key, g => g.ToList());
            var serviceTypes = _uow.ServiceTypes.GetAll().ToList();

            _allSnapshots.Clear();
            foreach (var p in personnel)
            {
                ranks.TryGetValue(p.RankId, out var rank);
                units.TryGetValue(p.OrganisationUnitId, out var unit);
                events.TryGetValue(p.Id, out var pEvents);
                services.TryGetValue(p.Id, out var pServices);

                var sn = _statusEngine.CalculatePersonStatus(p, pEvents, statusTypes, rank, unit, pServices, serviceTypes, _clock.Now);
                _allSnapshots.Add(sn);
            }

            FilterPersonnel();
            if (SelectedPerson == null && DisplayPersonnel.Count > 0)
            {
                SelectedPerson = DisplayPersonnel.First();
            }
        }

        private void FilterPersonnel()
        {
            DisplayPersonnel.Clear();
            var query = _allSnapshots.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string s = SearchText.Trim();
                query = query.Where(x =>
                    (x.Person.LastName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (x.Person.FirstName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (x.Person.MilitaryServiceNumber?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (x.Rank?.ShortName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
            }

            if (SelectedCategoryFilter == "Στελέχη")
            {
                query = query.Where(x => x.Person.Category == PersonnelCategory.OfficerOrNco);
            }
            else if (SelectedCategoryFilter == "Οπλίτες")
            {
                query = query.Where(x => x.Person.Category == PersonnelCategory.Conscript);
            }

            foreach (var item in query.OrderBy(x => x.Rank?.SortOrder ?? 99).ThenBy(x => x.Person.LastName))
            {
                DisplayPersonnel.Add(item);
            }
        }

        private void LoadSelectedPersonDetails()
        {
            SelectedPersonAbsenceHistory.Clear();
            SelectedPersonServiceHistory.Clear();

            if (SelectedPerson?.Person == null) return;

            var events = _uow.StatusEvents.Find(e => e.PersonnelId == SelectedPerson.Person.Id).OrderByDescending(e => e.StartAt);
            foreach (var ev in events) SelectedPersonAbsenceHistory.Add(ev);

            var services = _uow.ServiceAssignments.Find(s => s.PersonnelId == SelectedPerson.Person.Id).OrderByDescending(s => s.ServiceDate);
            foreach (var sv in services) SelectedPersonServiceHistory.Add(sv);
        }

        private void AddPerson()
        {
            var editorVM = new PersonEditorViewModel(_uow, _conflictEngine, _personnelService);
            var dialog = new PersonEditorDialog(editorVM);
            if (dialog.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void EditPerson()
        {
            if (SelectedPerson?.Person == null) return;

            var editorVM = new PersonEditorViewModel(_uow, _conflictEngine, _personnelService, SelectedPerson.Person);
            var dialog = new PersonEditorDialog(editorVM);
            if (dialog.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void ArchivePerson()
        {
            if (SelectedPerson?.Person == null) return;
            var res = MessageBox.Show(
                $"Θα αρχειοθετηθεί ο:\n{SelectedPerson.Rank?.ShortName} {SelectedPerson.Person.FullName}\n\nΗ εγγραφή θα παραμείνει διαθέσιμη στο ιστορικό.\nΣυνέχεια;",
                "Επιβεβαίωση Αρχειοθέτησης",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                _personnelService.ArchivePerson(SelectedPerson.Person.Id, $"Αρχειοθέτηση {SelectedPerson.Person.FullName}");
                LoadData();
            }
        }
    }
}
