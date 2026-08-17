using System;

namespace Dynamologio.App.Navigation
{
    /// <summary>
    /// Holds the active navigation state and coordinates section transitions.
    /// Does NOT own ViewModel creation or lifecycle (MainViewModel handles activation).
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
