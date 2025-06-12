using Durandal.API;
using Durandal.Common.Audio;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class FunctionalTestTurnResult
    {
        //////////// Turn metadata ////////////
        public Guid TraceId { get; set; }

        public DateTimeOffset? TurnStartTime { get; set; }

        public DateTimeOffset? TurnEndTime { get; set; }

        //////////// Inputs ////////////
        public DialogRequest DialogRequest { get; set; }

        //////////// Outputs ////////////
        public DialogResponse DialogResponse { get; set; }

        public Dictionary<string, string> SPARequestData { get; set; }

        public ValidationResponse ValidationResult { get; set; }

        public AudioSample StreamingAudioResponsePcm { get; set; }
    }
}
