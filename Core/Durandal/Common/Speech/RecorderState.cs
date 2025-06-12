using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech
{
    public enum RecorderState
    {
        /// <summary>
        /// An error occurred during recording
        /// </summary>
        Error = 0,

        /// <summary>
        /// Recording has finished and an utterance was successfully captured
        /// </summary>
        Finished = 1,
        
        /// <summary>
        /// Recording has finished but nothing was captured
        /// </summary>
        FinishedNothingRecorded = 2,
    }
}
