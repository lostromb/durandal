using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net
{
    /// <summary>
    /// Represents the direction that data is travelling on a network.
    /// </summary>
    public enum NetworkDirection
    {
        /// <summary>
        /// Unknown where request is going, or it is data at-rest
        /// </summary>
        Unknown,

        /// <summary>
        /// The data is coming in from the network (download).
        /// </summary>
        Incoming,

        /// <summary>
        /// The data is being send outwards to the network (upload).
        /// </summary>
        Outgoing,
        
        /// <summary>
        /// This is a special state which indicates that the request is simultaneously
        /// incoming (to a proxy server) and outgoing (to a client calling the proxy).
        /// When network requests are set to this state, many of their operations have
        /// to be done manually, so it should only be used in very special cases
        /// </summary>
        Proxied,
    }
}
