
namespace Durandal.Common.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Represents a component in an audio graph which introduces algorithmic delay that may need to be reported
    /// for specific timing-sensitive operations to work properly.
    /// The definition of "real-time delay" does NOT necessarily mean that a component will pad with zeroes before
    /// returning processed audio. It means that a read from the component will return audio that has a "timestamp"
    /// of some amount of delay in the past, or the component will read 0 until it has samples available.
    /// Thus, if you process delayed audio in a non-realtime scenario
    /// (e.g. file transcoding), the final output should be identical to the non-delayed output.
    /// </summary>
    public interface IAudioDelayingFilter
    {
        /// <summary>
        /// Gets the algorithmic delay introduced by this filter (or potentially a set of filters)
        /// </summary>
        TimeSpan AlgorithmicDelay { get; }
    }
}
