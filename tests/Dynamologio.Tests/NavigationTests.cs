using Xunit;
using Dynamologio.App.Navigation;
using Dynamologio.App.ViewModels;
using Moq;
using Dynamologio.Core.Interfaces;

namespace Dynamologio.Tests
{
    public class NavigationTests
    {
        [Fact]
        public void NavigationItemViewModel_Initialization_Works()
        {
            var item = new NavigationItemViewModel
            {
                Section = NavigationSection.Dashboard,
                Label = "Dashboard",
                Icon = "Icon"
            };
            
            Assert.Equal(NavigationSection.Dashboard, item.Section);
            Assert.Equal("Dashboard", item.Label);
            Assert.Equal("Icon", item.Icon);
        }

        [Fact]
        public void MainViewModel_NavigateCommand_RequiresNavigationSection()
        {
            var mvm = new MainViewModel(
                new Mock<INavigationService>().Object,
                new Mock<IViewModelFactory>().Object,
                new Mock<IShellStateService>().Object
            );
            
            mvm.NavigateCommand.Execute(NavigationSection.Absences);
            
            // Just verifying no crash on strongly-typed enum parameter
        }
    }
}
