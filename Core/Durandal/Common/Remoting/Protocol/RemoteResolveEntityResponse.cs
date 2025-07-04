﻿using Durandal.API;
using Durandal.Common.Statistics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteResolveEntityResponse
    {
        /// <summary>
        /// The output hypotheses from entity resolution, expressed as ordinals pointing into the original list of items
        /// </summary>
        public List<Hypothesis<int>> Hypotheses { get; set; }

        /// <summary>
        /// The set of log events that were generated by the remote service while processing this request
        /// </summary>
        public InstrumentationEventList LogEvents { get; set; }
    }
}
