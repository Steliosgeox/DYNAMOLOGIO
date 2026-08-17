using System;

namespace Dynamologio.Core.Interfaces
{
    /// <summary>
    /// Testable clock abstraction for deterministic date & timestamp operations
    /// </summary>
    public interface IClock
    {
        DateTime Now { get; }
        DateTime UtcNow { get; }
        DateTime Today { get; }
    }

    public class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime Today => DateTime.Today;
    }

    public class FixedClock : IClock
    {
        private DateTime _current;

        public FixedClock(DateTime fixedDateTime)
        {
            _current = fixedDateTime;
        }

        public DateTime Now => _current;
        public DateTime UtcNow => _current.ToUniversalTime();
        public DateTime Today => _current.Date;

        public void SetTime(DateTime newDateTime)
        {
            _current = newDateTime;
        }

        public void AdvanceDays(int days)
        {
            _current = _current.AddDays(days);
        }

        public void AdvanceHours(int hours)
        {
            _current = _current.AddHours(hours);
        }
    }
}
