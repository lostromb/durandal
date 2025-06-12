using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Speech.UserID
{
    public class SpeakerIdentificationProfile
    {
        public string SpeakerId { get; set; }
        public string Locale { get; set; }
        public TimeSpan EnrollmentDataAccumulated { get; set; }
        public TimeSpan EnrollmentDataStillRequired { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset LastUsageTime { get; set; }
        public SpeakerEnrollmentStatus EnrollmentStatus { get; set; }
    }
}
