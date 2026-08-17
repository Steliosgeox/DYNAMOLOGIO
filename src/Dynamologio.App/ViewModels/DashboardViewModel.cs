using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Projections;

namespace Dynamologio.App.ViewModels
{
    public class DashboardViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly IStrengthQueryService _strengthQueryService;
        private readonly IClock _clock;
        private readonly INavigationService _navigationService;

        public UnitStrengthSnapshot Snapshot { get; private set; }

        public int TotalActive => Snapshot?.TotalActiveStrength ?? 0;
        public int TotalPresent => Snapshot?.TotalPresent ?? 0;
        public int TotalAbsent => Snapshot?.TotalAbsent ?? 0;
        public int OfficersActive => Snapshot?.OfficersAndNcosActive ?? 0;
        public int ConscriptsActive => Snapshot?.ConscriptsActive ?? 0;
        public int ReturningToday => Snapshot?.ReturningTodayCount ?? 0;
        public int ReturningTomorrow => Snapshot?.ReturningTomorrowCount ?? 0;

        public ObservableCollection<PersonnelStatusSnapshot> AbsentPersonnelList { get; } = new ObservableCollection<PersonnelStatusSnapshot>();

        public ICommand NavigateToDynamologioCommand { get; }
        public ICommand NavigateToAbsencesCommand { get; }
        public ICommand NavigateToPersonnelCommand { get; }
        public ICommand NavigateToServicesCommand { get; }

        public DashboardViewModel(IStrengthQueryService strengthQueryService, IClock clock, INavigationService navigationService)
        {
            _strengthQueryService = strengthQueryService ?? throw new ArgumentNullException(nameof(strengthQueryService));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            NavigateToDynamologioCommand = new RelayCommand(() => _navigationService.Navigate(NavigationSection.Dynamologio));
            NavigateToAbsencesCommand = new RelayCommand(() => _navigationService.Navigate(NavigationSection.Absences));
            NavigateToPersonnelCommand = new RelayCommand(() => _navigationService.Navigate(NavigationSection.Personnel));
            NavigateToServicesCommand = new RelayCommand(() => _navigationService.Navigate(NavigationSection.Services));
        }

        public void Activate()
        {
            LoadData();
        }

        public void LoadData()
        {
            Snapshot = _strengthQueryService.GetCurrentStrengthSnapshot(_clock.Now);

            AbsentPersonnelList.Clear();
            foreach (var a in Snapshot.AbsentPersonnel.OrderBy(x => x.Rank?.SortOrder ?? 99))
            {
                AbsentPersonnelList.Add(a);
            }

            OnPropertyChanged(nameof(TotalActive));
            OnPropertyChanged(nameof(TotalPresent));
            OnPropertyChanged(nameof(TotalAbsent));
            OnPropertyChanged(nameof(OfficersActive));
            OnPropertyChanged(nameof(ConscriptsActive));
            OnPropertyChanged(nameof(ReturningToday));
            OnPropertyChanged(nameof(ReturningTomorrow));
        }
    }
}
