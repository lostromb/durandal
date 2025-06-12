
namespace Durandal.Common.Audio
{
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Meta-component which can report the cumulative delay of several chained audio graph components as a single value.
    /// </summary>
    public class MultiAudioDelayReporter : IAudioDelayingFilter
    {
        private IAudioDelayingFilter[] _subFilters;

        /// <summary>
        /// Constructs a new <see cref="MultiAudioDelayReporter"/>.
        /// </summary>
        /// <param name="filters">The list of sub filters that this component will summarize.</param>
        public MultiAudioDelayReporter(params IAudioDelayingFilter[] filters)
        {
            foreach (IAudioDelayingFilter filter in filters)
            {
                if (filter == null)
                {
                    throw new ArgumentNullException("One or more filters are null");
                }
            }

            _subFilters = filters;
        }

        /// <inheritdoc />
        public TimeSpan AlgorithmicDelay
        {
            get
            {
                TimeSpan returnVal = TimeSpan.Zero;

                foreach (IAudioDelayingFilter filter in _subFilters)
                {
                    returnVal += filter.AlgorithmicDelay;
                }

                return returnVal;
            }
        }
    }
}
