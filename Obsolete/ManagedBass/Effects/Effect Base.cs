﻿using System;
using System.Runtime.InteropServices;

namespace ManagedBass
{
    /// <summary>
    /// Wraps a Bass Effect such that you don't need to touch the Bass functions to Handle it.
    /// </summary>
    /// <typeparam name="T">Type of the <see cref="IEffectParameter"/></typeparam>
    public abstract class Effect<T> : INPC, IDisposable where T : class, IEffectParameter, new()
    {
        int _channel, _effectHandle, _hfsync;
        GCHandle _gch;
        bool _mediaPlayer;
        bool _wasActive;
        readonly SyncProcedure _syncProcedure;

        /// <summary>
        /// Default constructor.
        /// </summary>
        protected Effect()
        {
            _syncProcedure = (a, b, c, d) => Dispose();
        }

        /// <summary>
        /// Effect's Parameters.
        /// </summary>
        protected T Parameters = new T();
        
        /// <summary>
        /// Applies the Effect on a <paramref name="Channel"/>.
        /// </summary>
        /// <param name="Channel">The Channel to apply the Effect on.</param>
        /// <param name="Priority">Priority of the Effect in DSP chain.</param>
        public void ApplyOn(int Channel, int Priority = 0)
        {
            _channel = Channel;
            _priority = Priority;

            _gch = GCHandle.Alloc(Parameters, GCHandleType.Pinned);
            
            _hfsync = Bass.ChannelSetSync(Channel, SyncFlags.Free, 0, _syncProcedure);
        }

        /// <summary>
        /// Applies the Effect on a <see cref="MediaPlayer"/>.
        /// </summary>
        /// <param name="Player">The <see cref="MediaPlayer"/> to apply the Effect on.</param>
        /// <param name="Priority">Priority of the Effect in DSP chain.</param>
        public void ApplyOn(MediaPlayer Player, int Priority = 0)
        {
            ApplyOn(Player.Handle, Priority);

            _mediaPlayer = true;

            Player.MediaLoaded += NewHandle =>
            {
                if (_wasActive)
                    IsActive = false;

                Bass.ChannelRemoveSync(_channel, _hfsync);

                _channel = NewHandle;
                _hfsync = Bass.ChannelSetSync(NewHandle, SyncFlags.Free, 0, _syncProcedure);

                IsActive = _wasActive;
            };
        }
        
        int _priority;

        /// <summary>
        /// Priority of the Effect in DSP chain.
        /// </summary>
        public int Priority
        {
            get { return _priority; }
            set
            {
                if (IsActive && Bass.FXSetPriority(_effectHandle, value))
                    _priority = value;
            }
        }
        
        /// <summary>
        /// Removes the effect from the Channel and frees allocated memory.
        /// </summary>
        public void Dispose()
        {
            if (IsActive)
            {
                IsActive = false;

                _wasActive = true;
            }
            else _wasActive = false;

            _channel = _effectHandle = 0;

            if (!_mediaPlayer)
                _gch.Free();
        }
        
        /// <summary>
        /// Sets the effect parameters to default by initialising a new instance of <typeparamref name="T"/>.
        /// </summary>
        public void Default()
        {
            // Reallocate memory for Parameters
            _gch.Free();
            Parameters = new T();
            _gch = GCHandle.Alloc(Parameters, GCHandleType.Pinned);

            OnPreset();
        }

        /// <summary>
        /// Checks whether the effect is currently enabled and active.
        /// </summary>
        public bool IsActive
        {
            set
            {
                if (_channel == 0)
                    return;

                if (value && !IsActive)
                    _effectHandle = Bass.ChannelSetFX(_channel, Parameters.FXType, 1);
                
                else if (!value && IsActive && Bass.ChannelRemoveFX(_channel, _effectHandle))
                    _effectHandle = 0;

                OnPropertyChanged();
            }
            get { return _channel != 0 && _effectHandle != 0; }
        }
        
        /// <summary>
        /// Called after applying a Preset to notify that multiple properties have changed.
        /// </summary>
        protected void OnPreset()
        {
            OnPropertyChanged("");
        }
    }
}