using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Time.Scheduling
{
    /// <summary>
    /// Represents an event or object which has a recurring period and phase
    /// </summary>
    /// <typeparam name="T">The type of object to be associated with</typeparam>
    public class PeriodicEvent<T>
    {
        public T Object { get; set; }

        private TimeSpan _period;
        private TimeSpan _offset;

        public TimeSpan Period
        {
            get
            {
                return _period;
            }
            set
            {
                if (value == TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("Period cannot be zero");
                }

                _period = value;
            }
        }

        public TimeSpan Offset
        {
            get
            {
                return _offset;
            }
            set
            {
                // do the modulo operation here so offset is always less than period
                if (_period != TimeSpan.Zero)
                {
                    _offset = new TimeSpan(value.Ticks % _period.Ticks);
                }
                else
                {
                    _offset = TimeSpan.Zero;
                }
            }
        }

        public override string ToString()
        {
            return "Period " + _period + "\tOffset " + _offset + " " + Object.ToString();
        }
    }
}
