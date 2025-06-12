namespace Durandal.Common.Client
{
    using System;

    public class SpeechCaptureEventArgs : EventArgs
    {
        public SpeechCaptureEventArgs(string mostLikelyTranscript, bool succeeded)
        {
            this.Transcript = mostLikelyTranscript;
            this.Success = succeeded;
        }

        public string Transcript { get; set; }
        public bool Success { get; set; }
    }
}
