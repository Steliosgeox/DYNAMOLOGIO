using Dynamologio.App.ViewModels;

namespace Dynamologio.App.Navigation
{
    /// <summary>
    /// Creates feature ViewModels on demand. No reflection. No service locator.
    /// </summary>
    public interface IViewModelFactory
    {
        ViewModelBase Create(NavigationSection section);
    }
}
