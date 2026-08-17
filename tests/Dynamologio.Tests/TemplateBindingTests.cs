using Xunit;
using Dynamologio.App.ViewModels;
using Moq;
using Dynamologio.Core.Interfaces;
using Dynamologio.Reporting.Services;
using Dynamologio.App.Services;
using Dynamologio.App.Navigation;

namespace Dynamologio.Tests
{
    public class TemplateBindingTests
    {
        [Fact]
        public void DynamologioViewModel_ExposesTemplateVerificationStateEnum()
        {
            var dvm = new DynamologioViewModel(
                new Mock<IStrengthQueryService>().Object,
                new Mock<IPersonnelQueryService>().Object,
                new Mock<IReportGeneratorService>().Object,
                new Mock<IClock>().Object,
                new Mock<IShellStateService>().Object,
                new Mock<IPrintService>().Object,
                new Mock<IFileDialogService>().Object,
                new Mock<INotificationService>().Object
            );

            Assert.IsType<TemplateVerificationState>(dvm.TemplateStatus);
        }
    }
}
