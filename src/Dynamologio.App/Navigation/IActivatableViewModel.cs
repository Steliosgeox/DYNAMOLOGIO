namespace Dynamologio.App.Navigation
{
    /// <summary>
    /// Contract implemented by feature ViewModels that need data loading when navigated to.
    /// </summary>
    public interface IActivatableViewModel
    {
        void Activate();
    }
}
