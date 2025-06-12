using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NAudio.Devices
{
    /// <summary>
    /// An audio output device backed by DirectSound on Windows.
    /// </summary>
    internal class DirectSoundPlayer : AbstractNAudioWavePlayer
    {
        private static readonly TimeSpan DEFAULT_LATENCY = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Constructs a new <see cref="DirectSoundPlayer"/> for audio output.
        /// </summary>
        /// <param name="graph">The graph to associated with this component.</param>
        /// <param name="hardwareFormat">The format to initialize the hardware device with.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger</param>
        /// <param name="deviceInfo">The handle to the specific device to use</param>
        /// <param name="desiredLatency">The desired buffer latency</param>
        public DirectSoundPlayer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat hardwareFormat,
            string nodeCustomName,
            ILogger logger,
            DirectSoundDeviceInfo deviceInfo,
            TimeSpan? desiredLatency = null)
            : base(graph, hardwareFormat, nameof(DirectSoundPlayer), nodeCustomName, logger) 
        {
            int latencyMs = ((int)desiredLatency.GetValueOrDefault(DEFAULT_LATENCY).TotalMilliseconds).AssertPositive(nameof(desiredLatency));
            if (deviceInfo == null)
            {
                logger.Log($"Using default DirectSound output device with latency {latencyMs}.");
                DirectSoundOut actualDevice = new DirectSoundOut(DirectSoundOut.DSDEVID_DefaultPlayback, latencyMs);
                base.InitializeWavePlayer(actualDevice);
            }
            else
            {
                logger.Log($"Using DirectSound output device \"{deviceInfo.Description}\" with latency {latencyMs}.");
                DirectSoundOut actualDevice = new DirectSoundOut(deviceInfo.Guid, latencyMs);
                base.InitializeWavePlayer(actualDevice);
            }
        }
    }
}
