using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Core.Projections;
using Dynamologio.Infrastructure.Services;
using Dynamologio.App.Services;

namespace Dynamologio.App.ViewModels
{
    public class PersonnelViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IPersonnelQueryService _personnelQueryService;
        private readonly IPersonnelService _personnelService;
        private readonly IClock _clock;
        private readonly IPersonEditorDialogService _personEditorDialogService;
        private readonly IConfirmationService _confirmationService;

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
            IPersonnelQueryService personnelQueryService,
            IPersonnelService personnelService,
            IClock clock,
            IPersonEditorDialogService personEditorDialogService,
            IConfirmationService confirmationService)
        {
            _personnelQueryService = personnelQueryService ?? throw new ArgumentNullException(nameof(personnelQueryService));
            _personnelService = personnelService;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _personEditorDialogService = personEditorDialogService ?? throw new ArgumentNullException(nameof(personEditorDialogService));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));

            AddPersonCommand = new RelayCommand(AddPerson);
            EditPersonCommand = new RelayCommand(EditPerson, () => SelectedPerson != null);
            ArchivePersonCommand = new RelayCommand(ArchivePerson, () => SelectedPerson != null);
        }

        public void Activate()
        {
            LoadData();
        }

        public void LoadData()
        {
            _allSnapshots.Clear();
            foreach (var sn in _personnelQueryService.GetAllPersonnelStatus(_clock.Now))
            {
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

            var events = _personnelQueryService.GetAbsenceHistory(SelectedPerson.Person.Id);
            foreach (var ev in events) SelectedPersonAbsenceHistory.Add(ev);

            var services = _personnelQueryService.GetServiceHistory(SelectedPerson.Person.Id);
            foreach (var sv in services) SelectedPersonServiceHistory.Add(sv);
        }

        private void AddPerson()
        {
            if (_personEditorDialogService.ShowAddDialog())
            {
                LoadData();
            }
        }

        private void EditPerson()
        {
            if (SelectedPerson?.Person == null) return;

            if (_personEditorDialogService.ShowEditDialog(SelectedPerson.Person))
            {
                LoadData();
            }
        }

        private void ArchivePerson()
        {
            if (SelectedPerson?.Person == null) return;
            var res = _confirmationService.Confirm(
                "Επιβεβαίωση Αρχειοθέτησης",
                $"Θα αρχειοθετηθεί ο:\n{SelectedPerson.Rank?.ShortName} {SelectedPerson.Person.FullName}\n\nΗ εγγραφή θα παραμείνει διαθέσιμη στο ιστορικό.\nΣυνέχεια;");

            if (res)
            {
                _personnelService.ArchivePerson(SelectedPerson.Person.Id, $"Αρχειοθέτηση {SelectedPerson.Person.FullName}");
                LoadData();
            }
        }
    }
}
