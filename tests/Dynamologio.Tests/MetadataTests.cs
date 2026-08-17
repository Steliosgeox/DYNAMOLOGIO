using Xunit;
using Dynamologio.Infrastructure.Security;
using System;
using Dynamologio.Core.Interfaces;

namespace Dynamologio.Tests
{
    public class MetadataTests
    {
        [Fact]
        public void WindowsCurrentActor_ReturnsValidEnvironmentString()
        {
            var actor = new WindowsCurrentActor();
            var name = actor.GetActor();

            Assert.False(string.IsNullOrWhiteSpace(name));
        }

        [Fact]
        public void SystemClock_ReturnsExpectedTimes()
        {
            IClock clock = new SystemClock();
            
            var now = clock.Now;
            var utcNow = clock.UtcNow;
            var today = clock.Today;

            Assert.True(now > new DateTime(2020, 1, 1));
            Assert.True(utcNow > new DateTime(2020, 1, 1));
            Assert.Equal(today.TimeOfDay, TimeSpan.Zero);
            Assert.Equal(now.Date, today);
        }
    }
}
