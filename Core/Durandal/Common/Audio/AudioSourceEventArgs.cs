namespace Durandal.Common.Audio
{
    using System;

    public class AudioSourceEventArgs : EventArgs
    {
        public AudioSourceEventArgs(IAudioSampleSource stream)
        {
            Stream = stream;
        }

        public IAudioSampleSource Stream { get; private set; }
    }
}
