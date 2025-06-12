using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Represents the result of initializing an audio data source or codec.
    /// Follows the C convention of 0 = success, negative = failure, positive = information.
    /// Using this you can check for generic failure using failed = result &lt; AudioInitializationResult.Success;
    /// </summary>
    public enum AudioInitializationResult
    {
        /// <summary>
        /// Initialization failed because of badly-formatted data (file data, codec parameters, etc.).
        /// </summary>
        Failure_BadFormat = -3,

        /// <summary>
        /// Initialization failed because the stream ended prematurely.
        /// </summary>
        Failure_StreamEnded = -2,

        /// <summary>
        /// Initialization failed for an unknown reason.
        /// </summary>
        Failure_Unspecified = -1,

        /// <summary>
        /// Initialization succeeded.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Initialization has already been done.
        /// </summary>
        Already_Initialized = 1,
    }
}
