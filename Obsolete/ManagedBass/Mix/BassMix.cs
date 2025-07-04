﻿using System;
using System.Runtime.InteropServices;

namespace ManagedBass.Mix
{
    /// <summary>
    /// BassMix is a BASS addon providing the ability to mix together multiple BASS channels, with resampling and matrix mixing features.
    /// </summary>
    public static class BassMix
    {
#if __IOS__
        const string DllName = "__Internal";
#else
        const string DllName = "bassmix";
#endif
        
        //static IntPtr hLib;

        /// <summary>
        /// Load this library into Memory.
        /// </summary>
        /// <param name="Folder">Directory to Load from... <see langword="null"/> (default) = Load from Current Directory.</param>
        /// <returns><see langword="true" />, if the library loaded successfully, else <see langword="false" />.</returns>
        /// <remarks>
        /// <para>
        /// An external library is loaded into memory when any of its methods are called for the first time.
        /// This results in the first method call being slower than all subsequent calls.
        /// </para>
        /// <para>
        /// Some BASS libraries and add-ons may introduce new options to the main BASS lib like new parameters.
        /// But, before using these new options the respective library must be already loaded.
        /// This method can be used to make sure, that this library has been loaded.
        /// </para>
        /// </remarks>
        //public static bool Load(string Folder = null) => (hLib = DynamicLibrary.Load(DllName, Folder)) != IntPtr.Zero;

        /// <summary>
        /// Unloads this library from Memory.
        /// </summary>
        /// <returns><see langword="true" />, if the library unloaded successfully, else <see langword="false" />.</returns>
        //public static bool Unload() => DynamicLibrary.Unload(hLib);

        #region Split
        /// <summary>
        /// Creates a splitter stream (adds a reader channel to a decoding source channel).
        /// </summary>
        /// <param name="Channel">The handle of the decoding source channel to split... a HMUSIC, HSTREAM or HRECORD.</param>
        /// <param name="Flags">The channel falgs to be used to create the reader channel.</param>
        /// <param name="ChannelMap">The target (readers) channel mapping definition, which is an array of source channel index values (0=1st channel, 1=2nd channel, 2=3rd channel, 3=4th channel etc.) ending with a final -1 element (use <see langword="null" /> to create a 1:1 reader).</param>
        /// <returns>If successful, the new reader stream's handle is returned, else 0 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// A "splitter" basically does the opposite of a mixer: it splits a single source into multiple streams rather then mixing multiple sources into a single stream.
        /// Like mixer sources, splitter sources must be decoding channels.
        /// <para>
        /// The splitter stream will have the same sample rate and resolution as its source, but it can have a different number of channels, as dictated by the mapping parameter.
        /// Even when the number of channels is different (and so the amount of data produced is different), <see cref="Bass.ChannelGetLength" /> will give the source length, and <see cref="Bass.ChannelGetPosition" /> will give the source position that is currently being output by the splitter stream.
        /// </para>
        /// <para>
        /// All splitter streams with the same source share a buffer to access its sample data.
        /// The length of the buffer is determined by the <see cref="SplitBufferLength"/> config option;
        /// the splitter streams should not be allowed to drift apart beyond that, otherwise those left behind will suffer buffer overflows. 
        /// A splitter stream's buffer state can be reset via <see cref="SplitStreamReset(int)" />;
        /// that can also be used to reset a splitter stream that has ended, so that it can be played again.
        /// </para>
        /// <para>
        /// If the <see cref="BassFlags.SplitSlave"/> flag is used, the splitter stream will only receive data from the buffer and will not request more data from the source, so it can only receive data that has already been received by another splitter stream with the same source.
        /// The <see cref="BassFlags.SplitSlave"/> flag can be toggled at any time via <see cref="Bass.ChannelFlags" />.
        /// </para>
        /// <para>
        /// When <see cref="Bass.ChannelSetPosition" /> is used on a splitter stream, its source will be set to the requested position and the splitter stream's buffer state will be reset so that it immediately receives data from the new position. 
        /// The position change will affect all of the source's splitter streams, but the others will not have their buffer state reset;
        /// they will continue to receive any buffered data before reaching the data from the new position. 
        /// <see cref="SplitStreamReset(int)" /> can be used to reset the buffer state.
        /// </para>
        /// <para>
        /// Use <see cref="Bass.StreamFree"/> with a splitter channel to remove it from the source.
        /// When a source is freed, all of its splitter streams are automatically freed.
        /// </para>
        /// <para>
        /// The <paramref name="ChannelMap" /> array defines the channel number to be created for the reader as well as which source channels should be used for each.
        /// This enables you to create a reader stream which extract certain source channels (e.g. create a mono reader based on a stereo source), remaps the channel order (e.g. swap left and right in the reader) or even contains more channels than the source (e.g. create a 5.1 reader based on a stereo source).
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Init"><see cref="Bass.Init" /> has not been successfully called.</exception>
        /// <exception cref="Errors.Handle">The <paramref name="Channel" /> is not valid.</exception>
        /// <exception cref="Errors.Decode">The <paramref name="Channel" /> is not a decoding channel.</exception>
        /// <exception cref="Errors.Parameter">The <paramref name="ChannelMap" /> contains an invalid channel index.</exception>
        /// <exception cref="Errors.NotAvailable">Only decoding streams (<see cref="BassFlags.Decode"/>) are allowed when using the <see cref="Bass.NoSoundDevice"/>. The <see cref="BassFlags.AutoFree"/> flag is also unavailable to decoding channels.</exception>
        /// <exception cref="Errors.SampleFormat">The sample format is not supported by the device/drivers. If the stream is more than stereo or the <see cref="BassFlags.Float"/> flag is used, it could be that they are not supported (ie. no WDM drivers).</exception>
        /// <exception cref="Errors.Speaker">The device/drivers do not support the requested speaker(s), or you're attempting to assign a stereo stream to a mono speaker.</exception>
        /// <exception cref="Errors.Memory">There is insufficent memory.</exception>
        /// <exception cref="Errors.No3D">Couldn't initialize 3D support for the stream.</exception>
        /// <exception cref="Errors.Unknown">Some other mystery problem!</exception>
        [DllImport(DllName, EntryPoint = "BASS_Split_StreamCreate")]
        public static extern int CreateSplitStream(int Channel, BassFlags Flags, int[] ChannelMap);

        /// <summary>
        /// Retrieves the amount of buffered data available to a splitter stream, or the amount of data in a splitter source buffer.
        /// </summary>
        /// <param name="Handle">The splitter (as obtained by <see cref="CreateSplitStream" />) or the source channel handle.</param>
        /// <returns>If successful, then the amount of buffered data (in bytes) is returned, else -1 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// With a splitter source, this function reports how much data is in the buffer that is shared by all of its splitter streams.
        /// With a splitter stream, this function reports how much data is ahead of it in the buffer, before it will receive any new data from the source.
        /// A splitter stream can be repositioned within the buffer via the <see cref="SplitStreamReset(int, int)" /> function.
        /// <para>The amount of data that can be buffered is limited by the buffer size, which is determined by the <see cref="SplitBufferLength" /> config option.</para>
        /// <para>The returned buffered byte count is always based on the source's sample format, even with splitter streams that were created with a different channel count.</para>
        /// </remarks>
        /// <exception cref="Errors.Handle">The <paramref name="Handle" /> is neither a splitter stream or source.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Split_StreamGetAvailable")]
        public static extern int SplitStreamGetAvailable(int Handle);

        /// <summary>
        /// Resets a splitter stream or all splitter streams of a source.
        /// </summary>
        /// <param name="Handle">The splitter (as obtained by <see cref="CreateSplitStream" />) or the source channel handle.</param>
        /// <returns>If successful, <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// This function resets the splitter stream's buffer state, so that the next sample data it receives will be from the source's current position. 
        /// If the stream has ended, that is reset too, so that it can be played again.
        /// Unless called from within a mixtime sync callback, the stream's output buffer (if it has one) is also flushed.
        /// </remarks>
        /// <exception cref="Errors.Handle">The <paramref name="Handle" /> is neither a splitter stream or source.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Split_StreamReset")]
        public static extern bool SplitStreamReset(int Handle);

        /// <summary>
        /// Resets a splitter stream and sets its position in the source buffer.
        /// </summary>
        /// <param name="Handle">The splitter (as obtained by <see cref="CreateSplitStream" />) or the source channel handle.</param>
        /// <param name="Offset">
        /// How far back (in bytes) to position the splitter in the source buffer.
        /// This is based on the source's sample format, which may have a different channel count to the splitter.
        /// </param>
        /// <returns>If successful, <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// This function is the same as <see cref="SplitStreamReset(int)" /> except that it also provides the ability to position the splitter stream within the buffer that is shared by all of the splitter streams of the same source.
        /// A splitter stream's buffer position determines what data it will next receive.
        /// For example, if its position is half a second back, it will receive half a second of buffered data before receiving new data from the source.
        /// Calling this function with <paramref name="Offset"/> = 0 will result in the next data that the splitter stream receives being new data from the source, and is identical to using <see cref="SplitStreamReset(int)" />.
        /// <para>
        /// <paramref name="Offset" /> is automatically limited to the amount of data that the source buffer contains, which is in turn limited to the buffer size, determined by the <see cref="SplitBufferLength" /> config option.
        /// The amount of source data buffered, as well as a splitter stream's position within it, is available from <see cref="SplitStreamGetAvailable" />.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle">The <paramref name="Handle" /> is neither a splitter stream or source.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Split_StreamResetEx")]
        public static extern bool SplitStreamReset(int Handle, int Offset);

        /// <summary>
        /// Retrieves the source of a splitter stream.
        /// </summary>
        /// <param name="Handle">The splitter stream handle (which was add via <see cref="CreateSplitStream" /> beforehand).</param>
        /// <returns>If successful, the source stream's handle is returned, else 0 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <exception cref="Errors.Handle">The <paramref name="Handle" /> is not a splitter stream.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Split_StreamGetSource")]
        public static extern int SplitStreamGetSource(int Handle);

        [DllImport(DllName)]
        static extern int BASS_Split_StreamGetSplits(int handle, [In, Out] int[] array, int length);

        /// <summary>
        /// Retrieves the channel's splitters.
        /// </summary>
        /// <param name="Handle">The handle to check.</param>
        /// <returns>The array of splitter handles (<see langword="null" /> on error, use <see cref="Bass.LastError" /> to get the error code).</returns>
        public static int[] SplitStreamGetSplits(int Handle)
        {
            var num = BASS_Split_StreamGetSplits(Handle, null, 0);

            if (num <= 0)
                return null;

            var numArray = new int[num];
            num = BASS_Split_StreamGetSplits(Handle, numArray, num);

            return num <= 0 ? null : numArray;
        }
        #endregion

        /// <summary>
        /// Creates a mixer stream.
        /// </summary>
        /// <param name="Frequency">The sample rate of the mixer output (e.g. 44100).</param>
        /// <param name="Channels">The number of channels... 1 = mono, 2 = stereo, 4 = quadraphonic, 6 = 5.1, 8 = 7.1. More than stereo requires WDM drivers (or the <see cref="BassFlags.Decode"/> flag) in Windows, and the Speaker flags are ignored.</param>
        /// <param name="Flags">A combination of <see cref="BassFlags"/>..</param>
        /// <returns>If successful, the new stream's handle is returned, else 0 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// <para>
        /// Source channels are "plugged" into a mixer using the <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> functions, and "unplugged" using the <see cref="MixerRemoveChannel" /> function.
        /// Sources can be added and removed at any time, so a mixer does not have a predetermined length and <see cref="Bass.ChannelGetLength" /> is not applicable.
        /// Likewise, seeking is not possible, except to position 0, as described below.
        /// </para>
        /// <para>
        /// If the mixer output is being played (it is not a decoding channel), then there will be some delay in the effect of adding/removing source channels or changing their attributes being heard.
        /// This latency can be reduced by making use of the <see cref="Bass.PlaybackBufferLength" /> and <see cref="Bass.UpdatePeriod" /> config options.
        /// The playback buffer can be flushed by calling <see cref="Bass.ChannelPlay" /> (Restart = true) or <see cref="Bass.ChannelSetPosition" /> (Position = 0).
        /// That can also be done to restart a mixer that has ended.
        /// </para>
        /// <para>
        /// Unless the <see cref="BassFlags.MixerEnd"/> flag is specified, a mixer stream will never end.
        /// When there are no sources (or the sources have ended/stalled), it'll produce no output until there's an active source. 
        /// That's unless the <see cref="BassFlags.MixerNonStop"/> flag is used, in which case it will produce silent output while there are no active sources.
        /// The <see cref="BassFlags.MixerEnd"/> and <see cref="BassFlags.MixerNonStop"/> flags can be toggled at any time, using <see cref="Bass.ChannelFlags" />.
        /// </para>
        /// <para>
        /// Besides mixing channels, a mixer stream can be used as a resampler.
        /// In that case the freq parameter would be set the new sample rate, and the source channel's attributes would be left at their defaults. 
        /// A mixer stream can also be used to downmix, upmix and generally rearrange channels, set using the <see cref="ChannelSetMatrix(int,float[,])"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Init"><see cref="Bass.Init" /> has not been successfully called.</exception>
        /// <exception cref="Errors.NotAvailable">Only decoding streams (<see cref="BassFlags.Decode"/>) are allowed when using the <see cref="Bass.NoSoundDevice"/>.</exception>
        /// <exception cref="Errors.SampleRate"><paramref name="Frequency"/> is out of range. See <see cref="BassInfo.MinSampleRate"/> and <see cref="BassInfo.MaxSampleRate"/> members.</exception>
        /// <exception cref="Errors.SampleFormat">The sample format is not supported by the device/drivers. If the stream is more than stereo or the <see cref="BassFlags.Float"/> flag is used, it could be that they are not supported (ie. no WDM drivers).</exception>
        /// <exception cref="Errors.Speaker">The device/drivers do not support the requested speaker(s), or you're attempting to assign a stereo stream to a mono speaker.</exception>
        /// <exception cref="Errors.Memory">There is insufficent memory.</exception>
        /// <exception cref="Errors.No3D">Couldn't initialize 3D support for the stream.</exception>
        /// <exception cref="Errors.Unknown">Some other mystery problem!</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_StreamCreate")]
        public static extern int CreateMixerStream(int Frequency, int Channels, BassFlags Flags);

        /// <summary>
        /// Plugs a channel into a mixer.
        /// </summary>
        /// <param name="Handle">The mixer handle (created with <see cref="CreateMixerStream" />).</param>
        /// <param name="Channel">The handle of the channel to plug into the mixer... a HMUSIC, HSTREAM or HRECORD.</param>
        /// <param name="Flags">A combination of <see cref="BassFlags"/>.</param>
        /// <returns>If successful, then <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// <para>
        /// Internally, a mixer will use the <see cref="Bass.ChannelGetData(int,IntPtr,int)" /> function to get data from its source channels.
        /// That means that the source channels must be decoding channels (not using a <see cref="RecordProcedure" /> in the case of a recording channel).
        /// Plugging a channel into more than one mixer at a time is not possible because the mixers would be taking data away from each other.
        /// An advantage of this is that there is no need for a mixer's handle to be provided with the channel functions.
        /// It is actually possible to plug a channel into multiple mixers via the use of splitter streams.</para>
        /// <para>
        /// Channels are 'unplugged' using the <see cref="MixerRemoveChannel" /> function.
        /// Channels are also automatically unplugged when they are freed.
        /// </para>
        /// <para>
        /// When mixing a channel, the mixer makes use of the channel's attributes (freq/volume/pan), as set with <see cref="Bass.ChannelSetAttribute(int,ChannelAttribute,float)" /> or <see cref="Bass.ChannelSlideAttribute(int,ChannelAttribute,float,int)" />.
        /// The <see cref="Bass.LogarithmicVolumeCurve"/> and <see cref="Bass.LogarithmicPanningCurve"/> config option settings are also used.
        /// </para>
        /// <para>
        /// If a multi-channel stream has more channels than the mixer output, the extra channels will be discarded.
        /// For example, if a 5.1 stream is plugged into a stereo mixer, only the front-left/right channels will be retained.
        /// That is unless matrix mixing is used.
        /// </para>
        /// <para>
        /// The mixer processing is performed in floating-point, so it makes sense (for both quality and efficiency reasons) for the source channels to be floating-point too, though they do not have to be.
        /// It is also more efficient if the source channels have the same sample rate as the mixer output because no sample rate conversion is required then.
        /// When sample rate conversion is required, windowed sinc interpolation is used and the source's <see cref="ChannelAttribute.SampleRateConversion" /> attribute determines how many points/samples are used in that, as follows:
        /// 0 (or below) = 4 points, 1 = 8 points, 2 = 16 points, 3 = 32 points, 4 = 64 points, 5 = 128 points, 6 (or above) = 256 points.
        /// 8 points are used if the <see cref="ChannelAttribute.SampleRateConversion" /> attribute is unavailable (old BASS version).
        /// A higher number of points results in better sound quality (less aliasing and smaller transition band in the low-pass filter), but also higher CPU usage.
        /// </para>
        /// <para><b>Platform-specific:</b></para>
        /// <para>
        /// The sample rate conversion processing is limited to 128 points on iOS and Android.
        /// The mixer processing is also performed in fixed-point rather than floating-point on Android.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle">At least one of <paramref name="Handle" /> and <paramref name="Channel" /> is not valid.</exception>
        /// <exception cref="Errors.Decode"><paramref name="Channel" /> is not a decoding channel.</exception>
        /// <exception cref="Errors.Already"><paramref name="Channel" /> is already plugged into a mixer. It must be unplugged first.</exception>
        /// <exception cref="Errors.Speaker">The mixer does not support the requested speaker(s), or you're attempting to assign a stereo stream to a mono speaker.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_StreamAddChannel")]
        public static extern bool MixerAddChannel(int Handle, int Channel, BassFlags Flags);

        /// <summary>
        /// Plugs a channel into a mixer, optionally delaying the start and limiting the length.
        /// </summary>
        /// <param name="Handle">The mixer handle (created with <see cref="CreateMixerStream" />).</param>
        /// <param name="Channel">The handle of the channel to plug into the mixer... a HMUSIC, HSTREAM or HRECORD.</param>
        /// <param name="Flags">A combination of <see cref="BassFlags"/>.</param>
        /// <param name="Start">Delay (in bytes) before the channel is mixed in.</param>
        /// <param name="Length">The maximum amount of data (in bytes) to mix... 0 = no limit. Once this end point is reached, the channel will be removed from the mixer.</param>
        /// <returns>If successful, then <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// This function is identical to <see cref="MixerAddChannel(int,int,BassFlags)" />, but with the additional ability to specify a delay and duration for the channel.
        /// <para>
        /// The <paramref name="Start" /> and <paramref name="Length" /> parameters relate to the mixer output.
        /// So when calculating these values, use the mixer stream's sample format rather than the source channel's. 
        /// The start parameter is automatically rounded-down to the nearest sample boundary, while the length parameter is rounded-up to the nearest sample boundary.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle">At least one of <paramref name="Handle" /> and <paramref name="Channel" /> is not valid.</exception>
        /// <exception cref="Errors.Decode"><paramref name="Channel" /> is not a decoding channel.</exception>
        /// <exception cref="Errors.Already"><paramref name="Channel" /> is already plugged into a mixer. It must be unplugged first.</exception>
        /// <exception cref="Errors.Speaker">The mixer does not support the requested speaker(s), or you're attempting to assign a stereo stream to a mono speaker.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_StreamAddChannelEx")]
        public static extern bool MixerAddChannel(int Handle, int Channel, BassFlags Flags, long Start, long Length);

        /// <summary>
        /// Unplugs a channel from a mixer.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel to unplug (which was addded via <see cref="MixerAddChannel(int, int, BassFlags)" /> or <see cref="MixerAddChannel(int, int, BassFlags, long, long)" />) beforehand).</param>
        /// <returns>If successful, then <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <exception cref="Errors.Handle">The channel is not plugged into a mixer.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelRemove")]
        public static extern bool MixerRemoveChannel(int Handle);

        #region Configuration
        /// <summary>
        /// The splitter Buffer Length in milliseconds... 100 (min) to 5000 (max).
        /// </summary>
        /// <remarks>
        /// If the value specified is outside this range, it is automatically capped.
        /// When a source has its first splitter stream created, a Buffer is allocated
        /// for its sample data, which all of its subsequently created splitter streams
        /// will share. This config option determines how big that Buffer is. The default
        /// is 2000ms.
        /// The Buffer will always be kept as empty as possible, so its size does not
        /// necessarily affect latency; it just determines how far splitter streams can
        /// drift apart before there are Buffer overflow issues for those left behind.
        /// Changes do not affect buffers that have already been allocated; any sources
        /// that have already had splitter streams created will continue to use their
        /// existing buffers.
        /// </remarks>
        public static int SplitBufferLength
        {
            get { return Bass.GetConfig(Configuration.SplitBufferLength); }
            set { Bass.Configure(Configuration.SplitBufferLength, value); }
        }

        /// <summary>
        /// The source channel Buffer size multiplier... 1 (min) to 5 (max). 
        /// </summary>
        /// <remarks>
        /// If the value specified is outside this range, it is automatically capped.
        /// When a source channel has buffering enabled, the mixer will Buffer the decoded data,
        /// so that it is available to the <see cref="ChannelGetData(int, IntPtr, int)"/> and <see cref="ChannelGetLevel(int)"/> functions.
        /// To reach the source channel's Buffer size, the multiplier (multiple) is applied to the <see cref="Bass.PlaybackBufferLength"/>
        /// setting at the time of the mixer's creation.
        /// If the source is played at it's default rate, then the Buffer only need to be as big as the mixer's Buffer.
        /// But if it's played at a faster rate, then the Buffer needs to be bigger for it to contain the data that 
        /// is currently being heard from the mixer.
        /// For example, playing a channel at 2x its normal speed would require the Buffer to be 2x the normal size (multiple = 2).
        /// Larger buffers obviously require more memory, so the multiplier should not be set higher than necessary.
        /// The default multiplier is 2x. 
        /// Changes only affect subsequently setup channel buffers.
        /// An existing channel can have its Buffer reinitilized by disabling and then re-enabling 
        /// the <see cref="BassFlags.MixerBuffer"/> flag using <see cref="ChannelFlags"/>.
        /// </remarks>
        public static int MixerBufferLength
        {
            get { return Bass.GetConfig(Configuration.MixerBufferLength); }
            set { Bass.Configure(Configuration.MixerBufferLength, value); }
        }

        /// <summary>
        /// BASSmix add-on: How far back to keep record of source positions
        /// to make available for <see cref="ChannelGetPosition(int, PositionFlags, int)"/>, in milliseconds.
        /// </summary>
        /// <remarks>
        /// If a mixer is not a decoding channel (not using the BassFlag.Decode flag),
        /// this config setting will just be a minimum and the mixer will 
        /// always have a position record at least equal to its playback Buffer Length, 
        /// as determined by the PlaybackBufferLength config option.
        /// The default setting is 2000ms.
        /// Changes only affect newly created mixers, not any that already exist.
        /// </remarks>
        public static int MixerPositionEx
        {
            get { return Bass.GetConfig(Configuration.MixerPositionEx); }
            set { Bass.Configure(Configuration.MixerPositionEx, value); }
        }
        #endregion

        #region Mixer Source Channels
        #region Channel Flags
        /// <summary>
        /// Modifies and/or retrieves a channel's mixer flags.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel to modify (which was add via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" />) beforehand).</param>
        /// <param name="Flags">A combination of <see cref="BassFlags"/>.</param>
        /// <param name="Mask">
        /// The flags (as above) to modify.
        /// Flags that are not included in this are left as they are, so it can be set to 0 (<see cref="BassFlags.Default" />) in order to just retrieve the current flags. 
        /// To modify the speaker flags, any of the Speaker flags can be used in the mask (no need to include all of them).
        /// </param>
        /// <returns>If successful, the channel's updated flags are returned, else -1 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// This function only deals with the channel's mixer related flags.
        /// The channel's standard flags, for example looping (<see cref="BassFlags.Loop"/>), are unaffected - use <see cref="Bass.ChannelFlags" /> to modify them.
        /// </remarks>
        /// <exception cref="Errors.Handle">The channel is not plugged into a mixer.</exception>
        /// <exception cref="Errors.Speaker">The mixer does not support the requested speaker(s), or the channel has matrix mixing enabled.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelFlags")]
        public static extern BassFlags ChannelFlags(int Handle, BassFlags Flags, BassFlags Mask);

        /// <summary>
        /// Gets whether a flag is present.
        /// </summary>
        public static bool ChannelHasFlag(int handle, BassFlags flag)
        {
            return ChannelFlags(handle, 0, 0).HasFlag(flag);
        }

        /// <summary>
        /// Adds a flag to Mixer.
        /// </summary>
        public static bool ChannelAddFlag(int handle, BassFlags flag)
        {
            return ChannelFlags(handle, flag, flag).HasFlag(flag);
        }

        /// <summary>
        /// Removes a flag from Mixer.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool ChannelRemoveFlag(int handle, BassFlags flag)
        {
            return !ChannelFlags(handle, 0, flag).HasFlag(flag);
        }
        #endregion

        #region Channel Get Data
        /// <summary>
        /// Retrieves the immediate sample data (or an FFT representation of it) of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand).</param>
        /// <param name="Buffer">Location to write the data as an <see cref="IntPtr" /> (can be <see cref="IntPtr.Zero" /> when handle is a recording channel (HRECORD), to discard the requested amount of data from the recording buffer).</param>
        /// <param name="Length">Number of bytes wanted, and/or <see cref="DataFlags"/>.</param>
        /// <returns>
        /// If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code. 
        /// <para>When requesting FFT data, the number of bytes read from the channel (to perform the FFT) is returned.</para>
        /// <para>When requesting sample data, the number of bytes written to buffer will be returned (not necessarily the same as the number of bytes read when using the <see cref="DataFlags.Float"/> flag).</para>
        /// <para>When using the <see cref="DataFlags.Available"/> flag, the number of bytes in the channel's buffer is returned.</para>
        /// </returns>
        /// <remarks>
        /// <para>
        /// This function is like the standard <see cref="Bass.ChannelGetData(int,IntPtr,int)" />, but it gets the data from the channel's buffer instead of decoding it from the channel, which means that the mixer doesn't miss out on any data.
        /// In order to do this, the source channel must have buffering enabled, via the <see cref="BassFlags.MixerBuffer"/> flag.
        /// </para>
        /// <para>
        /// If the mixer is a decoding channel, then the channel's most recent data will be returned.
        /// Otherwise, the data will be in sync with what is currently being heard from the mixer, unless the buffer is too small so that the currently heard data isn't in it. 
        /// The <see cref="MixerBufferLength"/> config option can be used to set the buffer size.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel does not have buffering (<see cref="BassFlags.MixerBuffer"/>) enabled.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetData")]
        public static extern int ChannelGetData(int Handle, IntPtr Buffer, int Length);

        /// <summary>
        /// Retrieves the immediate sample data (or an FFT representation of it) of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand).</param>
        /// <param name="Buffer">byte[] to write the data to.</param>
        /// <param name="Length">Number of bytes wanted, and/or <see cref="DataFlags"/>.</param>
        /// <returns>
        /// If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code. 
        /// <para>When requesting FFT data, the number of bytes read from the channel (to perform the FFT) is returned.</para>
        /// <para>When requesting sample data, the number of bytes written to buffer will be returned (not necessarily the same as the number of bytes read when using the <see cref="DataFlags.Float"/> flag).</para>
        /// <para>When using the <see cref="DataFlags.Available"/> flag, the number of bytes in the channel's buffer is returned.</para>
        /// </returns>
        /// <remarks>
        /// <para>
        /// This function is like the standard <see cref="Bass.ChannelGetData(int,byte[],int)" />, but it gets the data from the channel's buffer instead of decoding it from the channel, which means that the mixer doesn't miss out on any data.
        /// In order to do this, the source channel must have buffering enabled, via the <see cref="BassFlags.MixerBuffer"/> flag.
        /// </para>
        /// <para>
        /// If the mixer is a decoding channel, then the channel's most recent data will be returned.
        /// Otherwise, the data will be in sync with what is currently being heard from the mixer, unless the buffer is too small so that the currently heard data isn't in it. 
        /// The <see cref="MixerBufferLength"/> config option can be used to set the buffer size.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel does not have buffering (<see cref="BassFlags.MixerBuffer"/>) enabled.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetData")]
        public static extern int ChannelGetData(int Handle, [In, Out] byte[] Buffer, int Length);

        /// <summary>
        /// Retrieves the immediate sample data (or an FFT representation of it) of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand).</param>
        /// <param name="Buffer">short[] to write the data to.</param>
        /// <param name="Length">Number of bytes wanted, and/or <see cref="DataFlags"/>.</param>
        /// <returns>
        /// If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code. 
        /// <para>When requesting FFT data, the number of bytes read from the channel (to perform the FFT) is returned.</para>
        /// <para>When requesting sample data, the number of bytes written to buffer will be returned (not necessarily the same as the number of bytes read when using the <see cref="DataFlags.Float"/> flag).</para>
        /// <para>When using the <see cref="DataFlags.Available"/> flag, the number of bytes in the channel's buffer is returned.</para>
        /// </returns>
        /// <remarks>
        /// <para>
        /// This function is like the standard <see cref="Bass.ChannelGetData(int,short[],int)" />, but it gets the data from the channel's buffer instead of decoding it from the channel, which means that the mixer doesn't miss out on any data.
        /// In order to do this, the source channel must have buffering enabled, via the <see cref="BassFlags.MixerBuffer"/> flag.
        /// </para>
        /// <para>
        /// If the mixer is a decoding channel, then the channel's most recent data will be returned.
        /// Otherwise, the data will be in sync with what is currently being heard from the mixer, unless the buffer is too small so that the currently heard data isn't in it. 
        /// The <see cref="MixerBufferLength"/> config option can be used to set the buffer size.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel does not have buffering (<see cref="BassFlags.MixerBuffer"/>) enabled.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetData")]
        public static extern int ChannelGetData(int Handle, [In, Out] short[] Buffer, int Length);

        /// <summary>
        /// Retrieves the immediate sample data (or an FFT representation of it) of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand).</param>
        /// <param name="Buffer">int[] to write the data to.</param>
        /// <param name="Length">Number of bytes wanted, and/or <see cref="DataFlags"/>.</param>
        /// <returns>
        /// If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code. 
        /// <para>When requesting FFT data, the number of bytes read from the channel (to perform the FFT) is returned.</para>
        /// <para>When requesting sample data, the number of bytes written to buffer will be returned (not necessarily the same as the number of bytes read when using the <see cref="DataFlags.Float"/> flag).</para>
        /// <para>When using the <see cref="DataFlags.Available"/> flag, the number of bytes in the channel's buffer is returned.</para>
        /// </returns>
        /// <remarks>
        /// <para>
        /// This function is like the standard <see cref="Bass.ChannelGetData(int,int[],int)" />, but it gets the data from the channel's buffer instead of decoding it from the channel, which means that the mixer doesn't miss out on any data.
        /// In order to do this, the source channel must have buffering enabled, via the <see cref="BassFlags.MixerBuffer"/> flag.
        /// </para>
        /// <para>
        /// If the mixer is a decoding channel, then the channel's most recent data will be returned.
        /// Otherwise, the data will be in sync with what is currently being heard from the mixer, unless the buffer is too small so that the currently heard data isn't in it. 
        /// The <see cref="MixerBufferLength"/> config option can be used to set the buffer size.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel does not have buffering (<see cref="BassFlags.MixerBuffer"/>) enabled.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetData")]
        public static extern int ChannelGetData(int Handle, [In, Out] int[] Buffer, int Length);

        /// <summary>
        /// Retrieves the immediate sample data (or an FFT representation of it) of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand).</param>
        /// <param name="Buffer">float[] to write the data to.</param>
        /// <param name="Length">Number of bytes wanted, and/or <see cref="DataFlags"/>.</param>
        /// <returns>
        /// If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code. 
        /// <para>When requesting FFT data, the number of bytes read from the channel (to perform the FFT) is returned.</para>
        /// <para>When requesting sample data, the number of bytes written to buffer will be returned (not necessarily the same as the number of bytes read when using the <see cref="DataFlags.Float"/> flag).</para>
        /// <para>When using the <see cref="DataFlags.Available"/> flag, the number of bytes in the channel's buffer is returned.</para>
        /// </returns>
        /// <remarks>
        /// <para>
        /// This function is like the standard <see cref="Bass.ChannelGetData(int,float[],int)" />, but it gets the data from the channel's buffer instead of decoding it from the channel, which means that the mixer doesn't miss out on any data.
        /// In order to do this, the source channel must have buffering enabled, via the <see cref="BassFlags.MixerBuffer"/> flag.
        /// </para>
        /// <para>
        /// If the mixer is a decoding channel, then the channel's most recent data will be returned.
        /// Otherwise, the data will be in sync with what is currently being heard from the mixer, unless the buffer is too small so that the currently heard data isn't in it. 
        /// The <see cref="MixerBufferLength"/> config option can be used to set the buffer size.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel does not have buffering (<see cref="BassFlags.MixerBuffer"/>) enabled.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetData")]
        public static extern int ChannelGetData(int Handle, [In, Out] float[] Buffer, int Length);
        #endregion

        /// <summary>
        /// Retrieves the level (peak amplitude) of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel (which was add via <see cref="MixerAddChannel(int, int, BassFlags)" /> or <see cref="MixerAddChannel(int, int, BassFlags, long, long)" />) beforehand).</param>
        /// <returns>
        /// If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code.
        /// <para>
        /// If successful, the level of the left channel is returned in the low word (low 16-bits), and the level of the right channel is returned in the high word (high 16-bits).
        /// If the channel is mono, then the low word is duplicated in the high word. 
        /// The level ranges linearly from 0 (silent) to 32768 (max). 0 will be returned when a channel is stalled.
        /// </para>
        /// </returns>
        /// <remarks>
        /// <para>
        /// This function is like the standard <see cref="Bass.ChannelGetLevel(int)" />, but it gets the level from the channel's buffer instead of decoding data from the channel, which means that the mixer doesn't miss out on any data. 
        /// In order to do this, the source channel must have buffering enabled, via the <see cref="BassFlags.MixerBuffer"/> flag.
        /// </para>
        /// <para>
        /// If the mixer is a decoding channel, then the channel's most recent data will be used to get the level.
        /// Otherwise, the level will be in sync with what is currently being heard from the mixer, unless the buffer is too small so that the currently heard data isn't in it. 
        /// The <see cref="MixerBufferLength"/> config option can be used to set the buffer size.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel does not have buffering (<see cref="BassFlags.MixerBuffer"/>) enabled.</exception>
        /// <exception cref="Errors.NotPlaying">The mixer is not playing.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetLevel")]
        public static extern int ChannelGetLevel(int Handle);

        /// <summary>
        /// Retrieves the level of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The handle of the mixer source channel (which was add via <see cref="MixerAddChannel(int, int, BassFlags)" /> or <see cref="MixerAddChannel(int, int, BassFlags, long, long)" />) beforehand).</param>
        /// <param name="Levels">An array to receive the levels.</param>
        /// <param name="Length">The amount of data to inspect to calculate the level, in seconds. The maximum is 1 second. Less data than requested may be used if the full amount is not available, eg. if the source's buffer (determined by the <see cref="MixerBufferLength"/> config option) is shorter.</param>
        /// <param name="Flags">A combination of <see cref="LevelRetrievalFlags"/>.</param>
        /// <returns>
        /// If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code.
		/// <para>
        /// If successful, the level of the left channel is returned in the low word (low 16-bits), and the level of the right channel is returned in the high word (high 16-bits).
        /// If the channel is mono, then the low word is duplicated in the high word. 
		/// The level ranges linearly from 0 (silent) to 32768 (max). 0 will be returned when a channel is stalled.
        /// </para>
		/// </returns>
        /// <remarks>
		/// <para>
        /// This function is like the standard <see cref="Bass.ChannelGetLevel(int,float[],float,LevelRetrievalFlags)" />, but it gets the level from the channel's buffer instead of decoding data from the channel, which means that the mixer doesn't miss out on any data. 
		/// In order to do this, the source channel must have buffering enabled, via the <see cref="BassFlags.MixerBuffer"/> flag.
        /// </para>
        /// </remarks>
		/// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel does not have buffering (<see cref="BassFlags.MixerBuffer"/>) enabled.</exception>
        /// <exception cref="Errors.NotPlaying">The mixer is not playing.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetLevelEx")]
        public static extern int ChannelGetLevel(int Handle, [In, Out] float[] Levels, float Length, LevelRetrievalFlags Flags);

        /// <summary>
        /// Retrieves a channel's mixing matrix, if it has one.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (which was add via <see cref="MixerAddChannel(int, int, BassFlags)" /> or <see cref="MixerAddChannel(int, int, BassFlags, long, long)" />) beforehand).</param>
        /// <param name="Matrix">The 2-dimentional array (float[,]) where to write the matrix.</param>
        /// <returns>If successful, a <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// For more details see <see cref="ChannelSetMatrix(int, float[,])" />.
        /// The array must be big enough to get the matrix.
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel is not using matrix mixing.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetMatrix")]
        public static extern bool ChannelGetMatrix(int Handle, [In, Out] float[,] Matrix);

        /// <summary>
        /// Retrieves the mixer that a channel is plugged into.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (which was add via <see cref="MixerAddChannel(int, int, BassFlags)" /> or <see cref="MixerAddChannel(int, int, BassFlags, long, long)" /> beforehand).</param>
        /// <returns>If successful, the mixer stream's handle is returned, else 0 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <exception cref="Errors.Handle">The channel is not plugged into a mixer.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetMixer")]
        public static extern int ChannelGetMixer(int Handle);

        /// <summary>
        /// Sets a channel's mixing matrix, if it has one.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand)</param>
        /// <param name="Matrix">The 2-dimensional array (float[,]) of the mixing matrix.</param>
        /// <returns>If successful, a <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// <para>
        /// Normally when mixing channels, the source channels are sent to the output in the same order - the left input is sent to the left output, and so on.
        /// Sometimes something a bit more complex than that is required.
        /// For example, if the source has more channels than the output, you may want to "downmix" the source so that all channels are present in the output.
        /// Equally, if the source has fewer channels than the output, you may want to "upmix" it so that all output channels have sound.
        /// Or you may just want to rearrange the channels. Matrix mixing allows all of these.
        /// </para>
        /// <para>
        /// A matrix mixer is created on a per-source basis (you can mix'n'match normal and matrix mixing), by using the <see cref="BassFlags.MixerMatrix" /> and/or <see cref="BassFlags.MixerDownMix" /> flag when calling <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" />. 
        /// The matrix itself is a 2-dimensional array of floating-point mixing levels, with the source channels on one axis, and the output channels on the other.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel is not using matrix mixing.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelSetMatrix")]
        public static extern bool ChannelSetMatrix(int Handle, float[,] Matrix);

        /// <summary>
        /// Sets a channel's mixing matrix, transitioning from the current matrix.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (which was add via <see cref="MixerAddChannel(int, int, BassFlags)" /> or <see cref="MixerAddChannel(int, int, BassFlags, long, long)" /> beforehand).</param>
        /// <param name="Matrix">The 2-dimensional array (float[,]) of the new mixing matrix.</param>
        /// <param name="Time">The time to take (in seconds) to transition from the current matrix to the specified matrix.</param>
        /// <returns>If successful, a <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// This method is identical to <see cref="ChannelSetMatrix(int, float[,])" /> but with the option of transitioning over time to the specified matrix.
        /// If this function or <see cref="ChannelSetMatrix(int,float[,])"/> is called while a previous matrix transition is still in progress, then that transition will be stopped.
        /// If <see cref="ChannelGetMatrix"/> is called mid-transition, it will give the mid-transition matrix values.
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The channel is not using matrix mixing.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelSetMatrixEx")]
        public static extern bool ChannelSetMatrix(int Handle, float[,] Matrix, float Time);

        /// <summary>
        /// Retrieves the playback position of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand).</param>
        /// <param name="Mode">Position mode... default = <see cref="PositionFlags.Bytes"/>.</param>
        /// <returns>If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code. If successful, the position is returned.</returns>
        /// <remarks>
        /// This function is like the standard <see cref="Bass.ChannelGetPosition" />, but it compensates for the mixer's buffering to return the source channel position that is currently being heard.
        /// So when used with a decoding channel (eg. a mixer source channel), this method will return the current decoding position.
        /// But if the mixer output is being played, then there is a playback buffer involved.
        /// This function compensates for that, to return the position that is currently being heard. 
        /// If the mixer itself is a decoding channel, then this function is identical to using <see cref="Bass.ChannelGetPosition" />.
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The requested position is not available.</exception>
        /// <exception cref="Errors.Unknown">Some other mystery problem!</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetPosition")]
        public static extern long ChannelGetPosition(int Handle, PositionFlags Mode = PositionFlags.Bytes);

        /// <summary>
        /// Retrieves the playback position of a mixer source channel, optionally accounting for some latency.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand).</param>
        /// <param name="Mode">Position mode.</param>
        /// <param name="Delay">How far back (in bytes) in the mixer output to get the source channel's position from.</param>
        /// <returns>If an error occurs, -1 is returned, use <see cref="Bass.LastError" /> to get the error code. If successful, the channel's position is returned.</returns>
        /// <remarks>
        /// <see cref="ChannelGetPosition(int,PositionFlags)" /> compensates for the mixer's playback buffering to give the position that is currently being heard, but if the mixer is feeding some other output system, it will not know how to compensate for that.
        /// This function fills that gap by allowing the latency to be specified in the call.
        /// This functionality requires the mixer to keep a record of its sources' position going back some time, and that is enabled via the <see cref="BassFlags.MixerPositionEx" /> flag when a mixer is created, with the <see cref="MixerPositionEx" /> config option determining how far back the position record goes.
        /// If the mixer is not a decoding channel (not using the <see cref="BassFlags.Decode" /> flag), then it will automatically have a position record at least equal to its playback buffer length.
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotAvailable">The requested position is not available, or delay goes beyond where the mixer has record of the source channel's position.</exception>
        /// <exception cref="Errors.Unknown">Some other mystery problem!</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetPositionEx")]
        public static extern long ChannelGetPosition(int Handle, PositionFlags Mode, int Delay);

        /// <summary>
        /// Sets the playback position of a mixer source channel.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (which was addded via <see cref="MixerAddChannel(int,int,BassFlags)" /> or <see cref="MixerAddChannel(int,int,BassFlags,long,long)" /> beforehand).</param>
        /// <param name="Position">The position, in bytes. With MOD musics, the position can also be set in orders and rows instead of bytes.</param>
        /// <param name="Mode">Position Mode... default = <see cref="PositionFlags.Bytes"/>.</param>
        /// <returns>If successful, then <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// This function works exactly like the standard <see cref="Bass.ChannelSetPosition" />, except that it also resets things for the channel in the mixer, well as supporting the <see cref="BassFlags.MixerNoRampin"/> flag.
        /// See <see cref="ChannelGetPosition(int,PositionFlags)" /> for details.
        /// <para>For custom looping purposes (eg. in a mixtime <see cref="SyncProcedure"/>), the standard <see cref="Bass.ChannelSetPosition(int,long,PositionFlags)" /> function should be used instead of this</para>
        /// <para>The playback buffer of the mixer can be flushed by using pos = 0.</para>
        /// </remarks>
        /// <exception cref="Errors.Handle">The channel is not plugged into a mixer.</exception>
        /// <exception cref="Errors.NotFile">The stream is not a file stream.</exception>
        /// <exception cref="Errors.Position">The requested position is illegal.</exception>
        /// <exception cref="Errors.NotAvailable">The download has not yet reached the requested position.</exception>
        /// <exception cref="Errors.Unknown">Some other mystery problem!</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelSetPosition")]
        public static extern bool ChannelSetPosition(int Handle, long Position, PositionFlags Mode = PositionFlags.Bytes);

        [DllImport(DllName)]
        static extern int BASS_Mixer_ChannelSetSync(int Handle, SyncFlags Type, long Parameter, SyncProcedure Procedure, IntPtr User);

        /// <summary>
        /// Sets up a synchronizer on a mixer source channel.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle.</param>
        /// <param name="Type">The type of sync.</param>
        /// <param name="Parameter">The sync parameters, depends on the sync type.</param>
        /// <param name="Procedure">The callback function which should be invoked with the sync.</param>
        /// <param name="User">User instance data to pass to the callback function.</param>
        /// <returns>If succesful, then the new synchronizer's handle is returned, else 0 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// <para>
        /// When used on a decoding channel (eg. a mixer source channel), syncs set with <see cref="Bass.ChannelSetSync" /> are automatically <see cref="SyncFlags.Mixtime"/>, 
        /// which means that they will be triggered as soon as the sync event is encountered during decoding. 
        /// But if the mixer output is being played, then there is a playback buffer involved, which will delay the hearing of the sync event. 
        /// This function compensates for that, delaying the triggering of the sync until the event is actually heard. 
        /// If the mixer itself is a decoding channel, or the <see cref="SyncFlags.Mixtime"/> flag is used, then there is effectively no real difference between this function and <see cref="Bass.ChannelSetSync" />.
        /// One sync type that is slightly different is the <see cref="SyncFlags.Stalled"/> sync, which can be either mixtime or not.
        /// </para>
        /// <para>
        /// Sync types that would automatically be mixtime when using <see cref="Bass.ChannelSetSync" /> are not so when using this function. 
        /// The <see cref="SyncFlags.Mixtime"/> flag should be specified in those cases, or <see cref="Bass.ChannelSetSync" /> used instead.
        /// </para>
        /// <para>
        /// When a source is removed from a mixer, any syncs that have been set on it via this function are automatically removed. 
        /// If the channel is subsequently plugged back into a mixer, the previous syncs will not still be set on it.
        /// Syncs set via <see cref="Bass.ChannelSetSync" /> are unaffected.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle">The channel is not plugged into a mixer.</exception>
        /// <exception cref="Errors.Type">An illegal <paramref name="Type" /> was specified.</exception>
        /// <exception cref="Errors.Parameter">An illegal <paramref name="Parameter" /> was specified.</exception>
        public static int ChannelSetSync(int Handle, SyncFlags Type, long Parameter, SyncProcedure Procedure, IntPtr User = default(IntPtr))
        {
            // Define a dummy SyncProcedure for OneTime syncs.
            var proc = Type.HasFlag(SyncFlags.Onetime)
                ? ((I, Channel, Data, Ptr) =>
                {
                    Procedure(I, Channel, Data, Ptr);
                    Extensions.ChannelReferences.Remove(Channel, I);
                }) : Procedure;

            var h = BASS_Mixer_ChannelSetSync(Handle, Type, Parameter, proc, User);

            if (h != 0)
                Extensions.ChannelReferences.Add(Handle, h, proc);

            return h;
        }

        [DllImport(DllName)]
        static extern int BASS_Mixer_ChannelSetSync(int Handle, int Type, long Parameter, SyncProcedureEx Procedure, IntPtr User);

        /// <summary>
        /// Sets up an extended synchronizer on a mixer source channel.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle.</param>
        /// <param name="Type">The type of sync.</param>
        /// <param name="Parameter">The sync parameters, depends on the sync type.</param>
        /// <param name="Procedure">The callback function which should be invoked with the sync.</param>
        /// <param name="User">User instance data to pass to the callback function.</param>
        /// <returns>If succesful, then the new synchronizer's handle is returned, else 0 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// <para>
        /// The main difference between this method and <see cref="ChannelSetSync(int,SyncFlags,long,SyncProcedure,IntPtr)" /> is, that this method invokes the <see cref="SyncProcedureEx" /> callback.
		/// This callback contains an extra 'Offset' parameter, which defines the position of the sync occurrence within the current update cycle of the source converted to the mixer stream position.
		/// This offset might be used to calculate more accurate non-mixtime sync triggers (as with non-mixtime sync's a variable delay is to be expected, as the accuracy depends on the sync thread waking in time, and there is no guarantee when that will happen) - 
		/// as well as mixtime syncs are only accurate to the current update period, as they are triggered within such.
		/// So a mixtime sync is being triggered ahead of the actual mixer position being heard.
		/// The 'Offset' parameter might be used to compensate for that.
		/// </para>
		/// <para>
        /// When used on a decoding channel (eg. a mixer source channel), syncs set with <see cref="Bass.ChannelSetSync" /> are automatically <see cref="SyncFlags.Mixtime"/>, 
        /// which means that they will be triggered as soon as the sync event is encountered during decoding. 
        /// But if the mixer output is being played, then there is a playback buffer involved, which will delay the hearing of the sync event. 
        /// This function compensates for that, delaying the triggering of the sync until the event is actually heard. 
        /// If the mixer itself is a decoding channel, or the <see cref="SyncFlags.Mixtime"/> flag is used, then there is effectively no real difference between this function and <see cref="Bass.ChannelSetSync" />.
        /// One sync type that is slightly different is the <see cref="SyncFlags.Stalled"/> sync, which can be either mixtime or not.
        /// </para>
        /// <para>
        /// Sync types that would automatically be mixtime when using <see cref="Bass.ChannelSetSync" /> are not so when using this function. 
        /// The <see cref="SyncFlags.Mixtime"/> flag should be specified in those cases, or <see cref="Bass.ChannelSetSync" /> used instead.
        /// </para>
        /// <para>
        /// When a source is removed from a mixer, any syncs that have been set on it via this function are automatically removed. 
        /// If the channel is subsequently plugged back into a mixer, the previous syncs will not still be set on it.
        /// Syncs set via <see cref="Bass.ChannelSetSync" /> are unaffected.
        /// </para>
        /// </remarks>
        /// <exception cref="Errors.Handle">The channel is not plugged into a mixer.</exception>
        /// <exception cref="Errors.Type">An illegal <paramref name="Type" /> was specified.</exception>
        /// <exception cref="Errors.Parameter">An illegal <paramref name="Parameter" /> was specified.</exception>
        public static int ChannelSetSync(int Handle, SyncFlags Type, long Parameter, SyncProcedureEx Procedure, IntPtr User = default(IntPtr))
        {
            // Define a dummy SyncProcedureEx for OneTime syncs.
            var proc = Type.HasFlag(SyncFlags.Onetime)
                ? ((I, Channel, Data, Ptr, Offset) =>
                {
                    Procedure(I, Channel, Data, Ptr, Offset);
                    Extensions.ChannelReferences.Remove(Channel, I);
                }) : Procedure;

            var h = BASS_Mixer_ChannelSetSync(Handle, (int)Type | 0x1000000, Parameter, proc, User);

            if (h != 0)
                Extensions.ChannelReferences.Add(Handle, h, proc);

            return h;
        }

        [DllImport(DllName)]
        static extern bool BASS_Mixer_ChannelRemoveSync(int Handle, int Sync);

        /// <summary>
        /// Removes a synchronizer from a mixer source channel.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (as returned by <see cref="MixerAddChannel(int, int, BassFlags)" /> or <see cref="MixerAddChannel(int, int, BassFlags, long, long)" />).</param>
        /// <param name="Sync">Handle of the synchronizer to remove (return value of a previous <see cref="ChannelSetSync(int, SyncFlags, long, SyncProcedure, IntPtr)" /> call).</param>
        /// <returns>If succesful, <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>This function can only remove syncs that were set via <see cref="ChannelSetSync(int, SyncFlags, long, SyncProcedure, IntPtr)" />, not those that were set via <see cref="Bass.ChannelSetSync(int, SyncFlags, long, SyncProcedure, IntPtr)" />.</remarks>
        /// <exception cref="Errors.Handle">At least one of <paramref name="Handle" /> and <paramref name="Sync" /> is not valid.</exception>        
        public static bool ChannelRemoveSync(int Handle, int Sync)
        {
            var b = BASS_Mixer_ChannelRemoveSync(Handle, Sync);

            if (b)
                Extensions.ChannelReferences.Remove(Handle, Sync);

            return b;
        }

        /// <summary>
        /// Retrieves the current position and value of an envelope on a channel.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle (which was add via <see cref="MixerAddChannel(int, int, BassFlags)" /> or <see cref="MixerAddChannel(int, int, BassFlags, long, long)" />) beforehand).</param>
        /// <param name="Type">The envelope to get the position/value of.</param>
        /// <param name="Value">A reference to a variable to receive the envelope value at the current position.</param>
        /// <returns>If successful, the current position of the envelope is returned, else -1 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>The envelope's current position is not necessarily what is currently being heard, due to buffering.</remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.Type"><paramref name="Type" /> is not valid.</exception>
        /// <exception cref="Errors.NotAvailable">There is no envelope of the requested type on the channel.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelGetEnvelopePos")]
        public static extern long ChannelGetEnvelopePosition(int Handle, MixEnvelope Type, ref float Value);

        /// <summary>
        /// Sets the current position of an envelope on a channel.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle.</param>
        /// <param name="Type">The envelope to set the position/value of.</param>
        /// <param name="Position">The new envelope position, in bytes. If this is beyond the end of the envelope it will be capped or looped, depending on whether the envelope has looping enabled.</param>
        /// <returns>If successful, the current position of the envelope is returned, else -1 is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// During playback, the effect of changes are not heard instantaneously, due to buffering. To reduce the delay, use the <see cref="Bass.PlaybackBufferLength"/> config option config option to reduce the buffer length.
        /// <para>
        /// Note: Envelopes deal in mixer positions, not sources!
        /// So when you are changing the source position (e.g. via <see cref="ChannelSetPosition(int,long,PositionFlags)" /> the envelope's positions doesn't change with it.
        /// You might use this method to align the envelope position accorting to the new source position
        /// .</para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.Type"><paramref name="Type" /> is not valid.</exception>
        /// <exception cref="Errors.NotAvailable">There is no envelope of the requested type on the channel.</exception>
        [DllImport(DllName, EntryPoint = "BASS_Mixer_ChannelSetEnvelopePos")]
        public static extern bool ChannelSetEnvelopePosition(int Handle, MixEnvelope Type, long Position);

        [DllImport(DllName)]
        static extern bool BASS_Mixer_ChannelSetEnvelope(int Handle, MixEnvelope Type, MixerNode[] Nodes, int Count);

        /// <summary>
        /// Sets an envelope to modify the sample rate, volume or pan of a channel over a period of time.
        /// </summary>
        /// <param name="Handle">The mixer source channel handle.</param>
        /// <param name="Type">The envelope to get the position/value of.</param>
        /// <param name="Nodes">The array of envelope nodes, which should have sequential positions.</param>
        /// <param name="Length">The number of elements in the nodes array... 0 = no envelope.</param>
        /// <returns>If successful, <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="Bass.LastError" /> to get the error code.</returns>
        /// <remarks>
        /// <para>
        /// Envelopes are applied on top of the channel's attributes, as set via <see cref="Bass.ChannelSetAttribute(int,ChannelAttribute,float)" />. 
        /// In the case of <see cref="MixEnvelope.Frequency"/> and <see cref="MixEnvelope.Volume"/>, 
        /// the final sample rate and volume is a product of the channel attribute and the envelope. 
        /// While in the <see cref="MixEnvelope.Pan"/> case, the final panning is a sum of the channel attribute and envelope.
        /// </para>
        /// <para>
        /// <see cref="ChannelGetEnvelopePosition" /> can be used to get the current envelope position, 
        /// and a <see cref="SyncFlags.MixerEnvelope"/> sync can be set via <see cref="ChannelSetSync(int,SyncFlags,long,SyncProcedure,IntPtr)" /> to be informed of when an envelope ends.
        /// The function can be called again from such a sync, in order to set a new envelope to follow the old one.
        /// </para>
        /// <para>
        /// Any previous envelope of the same type is replaced by the new envelope.
        /// A copy is made of the nodes array, so it does not need to persist beyond this function call.
        /// </para>
        /// <para>Note: Envelopes deal in mixer positions, not sources!
        /// You might use <see cref="ChannelSetEnvelopePosition" /> to adjust the envelope to a source channel position.</para>
        /// </remarks>
        /// <exception cref="Errors.Handle"><paramref name="Handle" /> is not plugged into a mixer.</exception>
        /// <exception cref="Errors.Type"><paramref name="Type" /> is not valid.</exception>
        public static bool ChannelSetEnvelope(int Handle, MixEnvelope Type, MixerNode[] Nodes, int Length = 0)
        {
            return BASS_Mixer_ChannelSetEnvelope(Handle, Type, Nodes, Length == 0 ? Nodes.Length : Length);
        }
        #endregion
    }
}