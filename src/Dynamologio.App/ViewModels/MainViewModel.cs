using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Interfaces;

namespace Dynamologio.App.ViewModels
{
    /// <summary>
    /// Shell-only coordinator. Owns current ViewModel, active section, and deployment header.
    /// Does NOT construct child ViewModels. Does NOT own the service graph.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IViewModelFactory _viewModelFactory;
        private readonly IShellStateService _shellState;

        private object _currentViewModel;
        private NavigationSection _activeSection = NavigationSection.Dashboard;
        private string _unitNameHeader = "ΜΟΝΑΔΑ";
        private string _officeNameHeader = "1ο ΓΡΑΦΕΙΟ";

        public object CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public NavigationSection ActiveSection
        {
            get => _activeSection;
            set => SetProperty(ref _activeSection, value);
        }

        public string UnitNameHeader
        {
            get => _unitNameHeader;
            set => SetProperty(ref _unitNameHeader, value);
        }

        public string OfficeNameHeader
        {
            get => _officeNameHeader;
            set => SetProperty(ref _officeNameHeader, value);
        }

        public ObservableCollection<NavigationItemViewModel> NavigationItems { get; } = new ObservableCollection<NavigationItemViewModel>();

        public ICommand NavigateCommand { get; }

        public MainViewModel(
            INavigationService navigationService,
            IViewModelFactory viewModelFactory,
            IShellStateService shellState,
            IUnitOfWork uow)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
            _shellState = shellState ?? throw new ArgumentNullException(nameof(shellState));

            // Subscribe to navigation events
            _navigationService.Navigated += OnNavigated;

            // Subscribe to shell state changes
            _shellState.DeploymentHeaderChanged += OnDeploymentHeaderChanged;
            _shellState.FullRefreshRequested += OnFullRefreshRequested;

            NavigateCommand = new RelayCommand(param =>
            {
                if (param is NavigationSection section)
                {
                    _navigationService.Navigate(section);
                }
                else if (param is NavigationItemViewModel item)
                {
                    _navigationService.Navigate(item.Section);
                }
            });

            InitializeNavigationItems();

            // Load deployment settings from database
            if (uow != null)
            {
                var uSetting = uow.AppSettings.Find(s => s.Key == "Deployment.UnitName").FirstOrDefault();
                if (uSetting != null && !string.IsNullOrWhiteSpace(uSetting.Value))
                {
                    UnitNameHeader = uSetting.Value;
                    _shellState.UpdateDeploymentHeader(uSetting.Value, _shellState.OfficeName);
                }

                var oSetting = uow.AppSettings.Find(s => s.Key == "Deployment.OfficeName").FirstOrDefault();
                if (oSetting != null && !string.IsNullOrWhiteSpace(oSetting.Value))
                {
                    OfficeNameHeader = oSetting.Value;
                    _shellState.UpdateDeploymentHeader(_shellState.UnitName, oSetting.Value);
                }
            }

            // Navigate to Dashboard on startup
            _navigationService.Navigate(NavigationSection.Dashboard);
        }

        private void OnNavigated(NavigationSection section)
        {
            ActiveSection = section;
            foreach (var item in NavigationItems)
            {
                item.IsSelected = item.Section == section;
            }

            var vm = _viewModelFactory.Create(section);

            if (vm is IActivatableViewModel activatable)
            {
                activatable.Activate();
            }

            CurrentViewModel = vm;
        }

        private void OnDeploymentHeaderChanged()
        {
            UnitNameHeader = _shellState.UnitName;
            OfficeNameHeader = _shellState.OfficeName;
        }

        private void OnFullRefreshRequested()
        {
            // Re-navigate to the current section to reload data
            _navigationService.Navigate(ActiveSection);
        }

        private void InitializeNavigationItems()
        {
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.Dashboard, Label = "Αρχική", Icon = "\uE80F" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.Dynamologio, Label = "Δυναμολόγιο", Icon = "\uE811" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.Personnel, Label = "Προσωπικό", Icon = "\uE716" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.Absences, Label = "Απουσίες", Icon = "\uE72B" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.Services, Label = "Υπηρεσίες", Icon = "\uE713" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.Reports, Label = "Αναφορές", Icon = "\uE71D" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.ImportExport, Label = "Εισαγωγή / Εξαγωγή", Icon = "\uE753" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.DataValidation, Label = "Έλεγχος Δεδομένων", Icon = "\uE734" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.History, Label = "Ιστορικό", Icon = "\uE81C" });
            NavigationItems.Add(new NavigationItemViewModel { Section = NavigationSection.Settings, Label = "Ρυθμίσεις", Icon = "\uE713" });
        }
    }
}
