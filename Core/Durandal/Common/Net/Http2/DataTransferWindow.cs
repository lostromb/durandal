using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Durandal.Common.Net.Http2
{
    /// <summary>
    /// Represents the HTTP/2 flow control credit value for either a connection or a single stream.
    /// These credits are added using WINDOW_UPDATE commands and removed when DATA frame bytes are transmitted.
    /// </summary>
    internal class DataTransferWindow
    {
        private long _availableCredits;

        public DataTransferWindow(int initialCredits = Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE)
        {
            Reset(initialCredits);
        }

        public int AvailableCredits => (int)_availableCredits;

        /// <summary>
        /// Returns true if the available credits exceed the maximum allowable value. If so, 
        /// the peer should usually respond with a FLOW_CONTROL_ERROR.
        /// </summary>
        public bool CreditsOverflow => _availableCredits > Http2Constants.MAX_INITIAL_WINDOW_SIZE;

        public void Reset(int initialCredits)
        {
            if (initialCredits < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCredits), "HTTP2 flow control initial credit must be positive");
            }

            _availableCredits = initialCredits;
        }

        /// <summary>
        /// Alters the amount of available credits by the given amount, and returns the new amount of credits available.
        /// The returned amount could be negative if the balance is overdrawn.
        /// </summary>
        /// <param name="creditsAugment">The number of credits to add/remove</param>
        /// <returns>The new amount of credits available (positive or negative)</returns>
        public int AugmentCredits(int creditsAugment)
        {
            long returnVal = Interlocked.Add(ref _availableCredits, creditsAugment);
            return (int)returnVal;
        }
    }
}
