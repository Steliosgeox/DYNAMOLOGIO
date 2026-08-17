using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;

namespace Dynamologio.App.ViewModels
{
    public class HistoryViewModel : ViewModelBase, Dynamologio.App.Navigation.IActivatableViewModel
    {
        private readonly IUnitOfWork _uow;
        private readonly IClock _clock;

        private string _searchText = string.Empty;
        private string _selectedEntityType = "Όλα";

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterHistory();
                }
            }
        }

        public string SelectedEntityType
        {
            get => _selectedEntityType;
            set
            {
                if (SetProperty(ref _selectedEntityType, value))
                {
                    FilterHistory();
                }
            }
        }

        public ObservableCollection<AuditEvent> FilteredEventsList { get; } = new ObservableCollection<AuditEvent>();
        public ICommand RefreshCommand { get; }

        public HistoryViewModel(IUnitOfWork uow, IClock clock)
        {
            _uow = uow;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            RefreshCommand = new RelayCommand(LoadData);
        }

        public void Activate()
        {
            FilterHistory();
        }

        public void LoadData()
        {
            FilterHistory();
        }

        private void FilterHistory()
        {
            FilteredEventsList.Clear();
            var all = _uow.AuditEvents.GetAll().OrderByDescending(x => x.CreatedAt).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string s = SearchText.Trim();
                all = all.Where(x =>
                    (x.Summary?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (x.Username?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (x.EntityId?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
            }

            if (SelectedEntityType != "Όλα")
            {
                all = all.Where(x => string.Equals(x.EntityType, SelectedEntityType, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in all.Take(300))
            {
                FilteredEventsList.Add(item);
            }
        }
    }
}
