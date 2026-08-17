using Dynamologio.App.Navigation;

namespace Dynamologio.App.ViewModels
{
    public class NavigationItemViewModel : ViewModelBase
    {
        private NavigationSection _section;
        private string _label;
        private string _icon;
        private bool _isSelected;

        public NavigationSection Section
        {
            get => _section;
            set => SetProperty(ref _section, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
