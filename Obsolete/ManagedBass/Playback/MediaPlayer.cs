﻿using ManagedBass.Tags;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ManagedBass
{
    /// <summary>
    /// A Reusable Channel which can Load files like a Player.
    /// <para><see cref="MediaPlayer"/> is perfect for UIs, as it implements <see cref="INotifyPropertyChanged"/>.</para>
    /// <para>Also, unlike normal, Properties/Effects/DSP set on a <see cref="MediaPlayer"/> persist through subsequent loads.</para>
    /// </summary>
    public class MediaPlayer : INotifyPropertyChanged, IDisposable
    {
        #region Fields
        readonly SynchronizationContext _syncContext;
        int _handle;
        
        internal int Handle
        {
            get { return _handle; }
            private set
            {
                ChannelInfo info;

                if (!Bass.ChannelGetInfo(value, out info))
                    throw new ArgumentException("Invalid Channel Handle: " + value);

                _handle = value;

                // Init Events
                Bass.ChannelSetSync(Handle, SyncFlags.Free, 0, GetSyncProcedure(() => 
                    {
                        if (Disposed != null)
                            Disposed.Invoke(this, EventArgs.Empty);
                    }));
                Bass.ChannelSetSync(Handle, SyncFlags.Stop, 0, GetSyncProcedure(() =>
                    {
                        if (MediaFailed != null)
                            MediaFailed.Invoke(this, EventArgs.Empty);
                    }));
                Bass.ChannelSetSync(Handle, SyncFlags.End, 0, GetSyncProcedure(() =>
                {
                    try
                    {
                        if (!Bass.ChannelHasFlag(Handle, BassFlags.Loop) && MediaEnded != null)
                        {
                            MediaEnded.Invoke(this, EventArgs.Empty);
                        }
                    }
                    finally { OnStateChanged(); }
                }));
            }
        }

        bool _restartOnNextPlayback;
        #endregion

        SyncProcedure GetSyncProcedure(Action Handler)
        {
            return (SyncHandle, Channel, Data, User) =>
            {
                if (Handler == null)
                    return;

                if (_syncContext == null)
                    Handler();
                else _syncContext.Post(S => Handler(), null);
            };
        }

        static MediaPlayer()
        {
            var currentDev = Bass.CurrentDevice;

            if (currentDev == -1 || !Bass.GetDeviceInfo(Bass.CurrentDevice).IsInitialized)
                Bass.Init(currentDev);
        }

        /// <summary>
        /// Creates a new instance of <see cref="MediaPlayer"/>.
        /// </summary>
        public MediaPlayer() { _syncContext = SynchronizationContext.Current; }

        #region Events
        /// <summary>
        /// Fired when this Channel is Disposed.
        /// </summary>
        public event EventHandler Disposed;

        /// <summary>
        /// Fired when the Media Playback Ends
        /// </summary>
        public event EventHandler MediaEnded;

        /// <summary>
        /// Fired when the Playback fails
        /// </summary>
        public event EventHandler MediaFailed;
        #endregion

        #region Frequency
        double _freq = 44100;
        
        /// <summary>
        /// Gets or Sets the Playback Frequency in Hertz.
        /// Default is 44100 Hz.
        /// </summary>
        public double Frequency
        {
            get { return _freq; }
            set
            {
                if (!Bass.ChannelSetAttribute(Handle, ChannelAttribute.Frequency, value))
                    return;

                _freq = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Balance
        double _pan;
        
        /// <summary>
        /// Gets or Sets Balance (Panning) (-1 ... 0 ... 1).
        /// -1 Represents Completely Left.
        ///  1 Represents Completely Right.
        /// Default is 0.
        /// </summary>
        public double Balance
        {
            get { return _pan; }
            set
            {
                if (!Bass.ChannelSetAttribute(Handle, ChannelAttribute.Pan, value))
                    return;

                _pan = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Device
        PlaybackDevice _dev;

        /// <summary>
        /// Gets or Sets the Playback Device used.
        /// </summary>
        public PlaybackDevice Device
        {
            get { return _dev ?? PlaybackDevice.GetByIndex(Bass.ChannelGetDevice(Handle)); }
            set
            {
                if (!value.Info.IsInitialized)
                    if (!value.Init())
                        return;

                if (!Bass.ChannelSetDevice(Handle, value.Index))
                    return;

                _dev = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Volume
        double _vol = 0.5;

        /// <summary>
        /// Gets or Sets the Playback Volume.
        /// </summary>
        public double Volume
        {
            get { return _vol; }
            set
            {
                if (!Bass.ChannelSetAttribute(Handle, ChannelAttribute.Volume, value))
                    return;

                _vol = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Loop
        bool _loop;

        /// <summary>
        /// Gets or Sets whether the Playback is looped.
        /// </summary>
        public bool Loop
        {
            get { return _loop; }
            set
            {
                if (value ? !Bass.ChannelAddFlag(Handle, BassFlags.Loop) : !Bass.ChannelRemoveFlag(Handle, BassFlags.Loop))
                    return;

                _loop = value;
                OnPropertyChanged();
            }
        }
        #endregion
        
        /// <summary>
        /// Override this method for custom loading procedure.
        /// </summary>
        /// <param name="FileName">Path to the File to Load.</param>
        /// <returns><see langword="true"/> on Success, <see langword="false"/> on failure</returns>
        protected virtual int OnLoad(string FileName)
        {
            return Bass.CreateStream(FileName);
        }

        #region Tags
        string _title = "", _artist = "", _album = "";

        /// <summary>
        /// Title of the Loaded Media.
        /// </summary>
        public string Title 
        {
            get { return _title; }
            private set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Artist of the Loaded Media.
        /// </summary>
        public string Artist
        {
            get { return _artist; }
            private set
            {
                _artist = value;
                OnPropertyChanged();
            }
        }
        
        /// <summary>
        /// Album of the Loaded Media.
        /// </summary>
        public string Album
        {
            get { return _album; }
            private set
            {
                _album = value;
                OnPropertyChanged();
            }
        }
        #endregion
        
        /// <summary>
        /// Gets the Playback State of the Channel.
        /// </summary>
        public PlaybackState State
        {
            get
            {
                return Handle == 0 ? PlaybackState.Stopped : Bass.ChannelIsActive(Handle);
            }
        }

        #region Playback
        /// <summary>
        /// Starts the Channel Playback.
        /// </summary>
        public bool Play()
        {
            try
            {
                var result = Bass.ChannelPlay(Handle, _restartOnNextPlayback);

                if (result)
                    _restartOnNextPlayback = false;

                return result;
            }
            finally { OnStateChanged(); }
        }

        /// <summary>
        /// Pauses the Channel Playback.
        /// </summary>
        public bool Pause()
        {
            try { return Bass.ChannelPause(Handle); }
            finally { OnStateChanged(); }
        }

        /// <summary>
        /// Stops the Channel Playback.
        /// </summary>
        /// <remarks>Difference from <see cref="Bass.ChannelStop"/>: Playback is restarted when <see cref="Play"/> is called.</remarks>
        public bool Stop()
        {
            try
            {
                _restartOnNextPlayback = true;
                return Bass.ChannelStop(Handle);
            }
            finally { OnStateChanged(); }
        }
        #endregion

        /// <summary>
        /// Gets the Playback Duration.
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                return TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(Handle, Bass.ChannelGetLength(Handle)));
            }
        }

        /// <summary>
        /// Gets or Sets the Playback Position.
        /// </summary>
        public TimeSpan Position
        {
            get { return TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(Handle, Bass.ChannelGetPosition(Handle))); }
            set { Bass.ChannelSetPosition(Handle, Bass.ChannelSeconds2Bytes(Handle, value.TotalSeconds)); }
        }

        /// <summary>
        /// Loads a file into the player.
        /// </summary>
        /// <param name="FileName">Path to the file to Load.</param>
        /// <returns><see langword="true"/> on succes, <see langword="false"/> on failure.</returns>
        public bool Load(string FileName)
        {
            try
            {
                if (Handle != 0)
                    Bass.StreamFree(Handle);
            }
            catch { }

            if (_dev != null)
                PlaybackDevice.Current = _dev;

            var currentDev = Bass.CurrentDevice;

            if (currentDev == -1 || !Bass.GetDeviceInfo(Bass.CurrentDevice).IsInitialized)
                Bass.Init(currentDev);

            var h = OnLoad(FileName);

            if (h == 0)
                return false;

            Handle = h;

            var tags = TagReader.Read(Handle);

            Title = !string.IsNullOrWhiteSpace(tags.Title) ? tags.Title 
                                                           : Path.GetFileNameWithoutExtension(FileName);
            Artist = tags.Artist;
            Album = tags.Album;
            
            InitProperties();

            if (MediaLoaded != null)
            {
                MediaLoaded.Invoke(h);
            }

            OnPropertyChanged("");

            return true;
        }

        /// <summary>
        /// Fired when a Media is Loaded.
        /// </summary>
        public event Action<int> MediaLoaded;

        /// <summary>
        /// Frees all resources used by the player.
        /// </summary>
        public virtual void Dispose()
        {
            try
            {
                if (Bass.StreamFree(Handle))
                    _handle = 0;
            }
            finally { OnStateChanged(); }
        }

        /// <summary>
        /// Initializes Properties on every call to <see cref="Load"/>.
        /// </summary>
        protected virtual void InitProperties()
        {
            Frequency = _freq;
            Balance = _pan;
            Volume = _vol;
            Loop = _loop;
        }
        
        void OnStateChanged()
        {
            OnPropertyChanged("State");
        }

        /// <summary>
        /// Fired when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Fires the <see cref="PropertyChanged"/> event.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            if (PropertyChanged == null)
                return;

            Action f = () => PropertyChanged.Invoke(this, new PropertyChangedEventArgs(PropertyName));

            if (_syncContext == null)
                f();
            else _syncContext.Post(S => f(), null);
        }
    }
}