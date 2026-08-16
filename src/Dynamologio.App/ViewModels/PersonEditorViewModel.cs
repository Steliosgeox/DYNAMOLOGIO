using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;

namespace Dynamologio.App.ViewModels
{
    public class PersonEditorViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IConflictEngine _conflictEngine;
        private readonly Personnel _editingPerson;
        private readonly bool _isNew;

        public string DialogTitle => _isNew ? "Προσθήκη Νέου Προσωπικού" : "Επεξεργασία Στοιχείων Προσωπικού";

        private string _militaryServiceNumber = string.Empty;
        private string _lastName = string.Empty;
        private string _firstName = string.Empty;
        private string _fatherName = string.Empty;
        private Rank _selectedRank;
        private OrganisationUnit _selectedUnit;
        private string _companyOrSection = string.Empty;
        private string _specialty = string.Empty;
        private DateTime _strengthStartDate = DateTime.Today;
        private string _notes = string.Empty;
        private string _errorMessage = string.Empty;

        public string MilitaryServiceNumber { get => _militaryServiceNumber; set => SetProperty(ref _militaryServiceNumber, value); }
        public string LastName { get => _lastName; set => SetProperty(ref _lastName, value); }
        public string FirstName { get => _firstName; set => SetProperty(ref _firstName, value); }
        public string FatherName { get => _fatherName; set => SetProperty(ref _fatherName, value); }
        public Rank SelectedRank { get => _selectedRank; set => SetProperty(ref _selectedRank, value); }
        public OrganisationUnit SelectedUnit { get => _selectedUnit; set => SetProperty(ref _selectedUnit, value); }
        public string CompanyOrSection { get => _companyOrSection; set => SetProperty(ref _companyOrSection, value); }
        public string Specialty { get => _specialty; set => SetProperty(ref _specialty, value); }
        public DateTime StrengthStartDate { get => _strengthStartDate; set => SetProperty(ref _strengthStartDate, value); }
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        public ObservableCollection<Rank> RanksList { get; } = new ObservableCollection<Rank>();
        public ObservableCollection<OrganisationUnit> UnitsList { get; } = new ObservableCollection<OrganisationUnit>();

        public bool? DialogResult { get; set; }
        public Action CloseAction { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public PersonEditorViewModel(IUnitOfWork uow, IConflictEngine conflictEngine, Personnel existingPerson = null)
        {
            _uow = uow;
            _conflictEngine = conflictEngine;
            _editingPerson = existingPerson;
            _isNew = existingPerson == null;

            foreach (var r in _uow.Ranks.GetAll().OrderBy(x => x.SortOrder)) RanksList.Add(r);
            foreach (var u in _uow.OrganisationUnits.GetAll().OrderBy(x => x.SortOrder)) UnitsList.Add(u);

            if (_editingPerson != null)
            {
                MilitaryServiceNumber = _editingPerson.MilitaryServiceNumber;
                LastName = _editingPerson.LastName;
                FirstName = _editingPerson.FirstName;
                FatherName = _editingPerson.FatherName;
                SelectedRank = RanksList.FirstOrDefault(r => r.Id == _editingPerson.RankId) ?? RanksList.FirstOrDefault();
                SelectedUnit = UnitsList.FirstOrDefault(u => u.Id == _editingPerson.OrganisationUnitId) ?? UnitsList.FirstOrDefault();
                CompanyOrSection = _editingPerson.CompanyOrSection;
                Specialty = _editingPerson.Specialty;
                StrengthStartDate = _editingPerson.StrengthStartDate;
                Notes = _editingPerson.Notes;
            }
            else
            {
                SelectedRank = RanksList.FirstOrDefault();
                SelectedUnit = UnitsList.FirstOrDefault();
            }

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save()
        {
            ErrorMessage = string.Empty;

            var target = _editingPerson ?? new Personnel();
            target.MilitaryServiceNumber = (MilitaryServiceNumber ?? "").Trim();
            target.LastName = (LastName ?? "").Trim().ToUpperInvariant();
            target.FirstName = (FirstName ?? "").Trim().ToUpperInvariant();
            target.FatherName = (FatherName ?? "").Trim().ToUpperInvariant();
            target.RankId = SelectedRank?.Id ?? Guid.Empty;
            target.Category = SelectedRank?.Category ?? PersonnelCategory.Conscript;
            target.OrganisationUnitId = SelectedUnit?.Id ?? Guid.Empty;
            target.CompanyOrSection = (CompanyOrSection ?? "").Trim();
            target.Specialty = (Specialty ?? "").Trim();
            target.StrengthStartDate = StrengthStartDate;
            target.Notes = (Notes ?? "").Trim();

            var existing = _uow.Personnel.GetAll();
            var conflicts = _conflictEngine.ValidatePersonnel(target, existing);

            var errors = conflicts.Where(c => c.Severity == ConflictSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                ErrorMessage = string.Join("\n", errors.Select(e => "• " + e.Message));
                return;
            }

            if (_isNew)
            {
                _uow.Personnel.Insert(target);
            }
            else
            {
                _uow.Personnel.Update(target);
            }

            DialogResult = true;
            CloseAction?.Invoke();
        }

        private void Cancel()
        {
            DialogResult = false;
            CloseAction?.Invoke();
        }
    }
}
