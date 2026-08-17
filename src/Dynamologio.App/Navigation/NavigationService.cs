using System;

namespace Dynamologio.App.Navigation
{
    /// <summary>
    /// Owns the current ViewModel lifecycle: creates VMs via factory, activates them, tracks the active section.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private NavigationSection _activeSection;

        public NavigationSection ActiveSection => _activeSection;
        public event Action<NavigationSection> Navigated;

        public NavigationService()
        {
        }

        public void Navigate(NavigationSection section)
        {
            _activeSection = section;
            Navigated?.Invoke(section);
        }
    }
}
