﻿using System;
using System.Runtime.InteropServices;

namespace ManagedBass
{
    /// <summary>
    /// Capture audio from Microphone.
    /// </summary>
    public sealed class Record : IAudioRecorder
    {
        readonly int _handle;

        /// <summary>
        /// Creates a new instance of <see cref="Record"/> with the Default Format and Device.
        /// </summary>
        public Record() : this(RecordDevice.Default) { }

        /// <summary>
        /// Creates a new instance of <see cref="Record"/> with the Default Format.
        /// </summary>
        /// <param name="Device">The <see cref="RecordDevice"/> to use.</param>
        public Record(RecordDevice Device) : this(Device, 44100, 2) { }

        /// <summary>
        /// Creates a new instance of <see cref="Record"/>.
        /// </summary>
        public Record(RecordDevice Device, int Frequency, int Channels, Resolution Resolution = Resolution.Short)
        {
            Bass.Init(0);
            Device.Init();
            
            Bass.CurrentRecordingDevice = Device.Index;
            
            _handle = Bass.RecordStart(Frequency, Channels, BassFlags.RecordPause | Resolution.ToBassFlag(), Processing);

            AudioFormat = WaveFormat.FromChannel(_handle);
        }

        /// <summary>
        /// Gets if Capturing is in progress.
        /// </summary>
        public bool IsRecording
        {
            get
            {
                return Bass.ChannelIsActive(_handle) == PlaybackState.Playing;
            }
        }

        /// <summary>
        /// Start Audio Capture.
        /// </summary>
        /// <returns><see langword="true"/> on success, else <see langword="false"/>.</returns>
        public bool Start()
        {
            return Bass.ChannelPlay(_handle);
        }

        /// <summary>
        /// Stop Audio Capture.
        /// </summary>
        /// <returns><see langword="true"/> on success, else <see langword="false"/>.</returns>
        public bool Stop()
        {
            return Bass.ChannelPause(_handle);
        }

        /// <summary>
        /// Gets the <see cref="WaveFormat"/> of the Recorded Audio.
        /// </summary>
        public WaveFormat AudioFormat { get; private set; }

        /// <summary>
        /// Frees all resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Bass.ChannelStop(_handle);
            Bass.StreamFree(_handle);

            _buffer = null;
        }

        #region Callback
        /// <summary>
        /// Provides the captured data.
        /// </summary>
        public event EventHandler<DataAvailableEventArgs> DataAvailable;

        byte[] _buffer;

        bool Processing(int HRecord, IntPtr Buffer, int Length, IntPtr User)
        {
            if (_buffer == null || _buffer.Length < Length)
                _buffer = new byte[Length];

            Marshal.Copy(Buffer, _buffer, 0, Length);
            if (DataAvailable != null)
            {
                DataAvailable.Invoke(this, new DataAvailableEventArgs(_buffer, Length));
            }

            return true;
        }
        #endregion
    }
}