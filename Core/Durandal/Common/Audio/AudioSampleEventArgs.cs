namespace Durandal.Common.Audio
{
    using System;

    public class AudioSampleEventArgs : EventArgs
    {
        public AudioSampleEventArgs(AudioSample audio)
        {
            Audio = audio;
        }

        public AudioSample Audio { get; private set; }
    }
}
