using System;

namespace Dynamologio.App.Navigation
{
    /// <summary>
    /// Typed navigation service. Feature ViewModels use this instead of MainViewModel.Navigate("string").
    /// </summary>
    public interface INavigationService
    {
        NavigationSection ActiveSection { get; }
        void Navigate(NavigationSection section);
        event Action<NavigationSection> Navigated;
    }
}
