using System;
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
                else if (param is string sectionName && Enum.TryParse(sectionName, out NavigationSection parsed))
                {
                    _navigationService.Navigate(parsed);
                }
            });

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
    }
}
