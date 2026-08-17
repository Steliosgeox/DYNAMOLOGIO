using Xunit;
using Dynamologio.App.Navigation;
using Dynamologio.Core.Enums;
using Dynamologio.App.ViewModels;
using System.Collections.Generic;
using System;
using Moq;
using Dynamologio.Core.Interfaces;
using Dynamologio.Infrastructure.Services;
using Dynamologio.App.Services;

namespace Dynamologio.Tests
{
    public class ViewModelArchitectureTests
    {
        [Fact]
        public void ViewModelFactory_UsesDelegateMap_AndNoGodDependencies()
        {
            var map = new Dictionary<NavigationSection, Func<ViewModelBase>>();
            var factory = new ViewModelFactory(map);

            Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(NavigationSection.Dashboard));

            map[NavigationSection.Dashboard] = () => new DashboardViewModel(
                new Mock<IStrengthQueryService>().Object,
                new Mock<IClock>().Object,
                new Mock<INavigationService>().Object
            );

            var vm = factory.Create(NavigationSection.Dashboard);
            Assert.NotNull(vm);
            Assert.IsType<DashboardViewModel>(vm);
        }

        [Fact]
        public void PersonnelViewModel_UsesIPersonEditorDialogService_InsteadOfWpfViews()
        {
            var pvm = new PersonnelViewModel(
                new Mock<IPersonnelQueryService>().Object,
                new Mock<IPersonnelService>().Object,
                new Mock<IClock>().Object,
                new Mock<IPersonEditorDialogService>().Object,
                new Mock<IConfirmationService>().Object
            );

            Assert.NotNull(pvm);
            Assert.True(typeof(PersonnelViewModel).GetConstructors()[0].GetParameters().Length == 5);
        }
    }
}
