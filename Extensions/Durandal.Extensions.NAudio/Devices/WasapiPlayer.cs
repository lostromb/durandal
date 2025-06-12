using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NAudio.Devices
{
    /// <summary>
    /// An audio output device backed by WASAPI on Windows.
    /// </summary>
    internal class WasapiPlayer : AbstractNAudioWavePlayer
    {
        private static readonly TimeSpan DEFAULT_LATENCY = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Constructs a new <see cref="WasapiPlayer"/> for audio output.
        /// </summary>
        /// <param name="graph">The graph to associated with this component.</param>
        /// <param name="hardwareFormat">The format to initialize the hardware device with.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger.</param>
        /// <param name="deviceHandle">If non-null, bind to a specific device on the system</param>
        /// <param name="latency">The desired buffer latency in milliseconds.</param>
        /// <param name="exclusiveMode">If true, use WASAPI exclusive mode. This improves performance and latency, but no other program will be able to use the device. </param>
        public WasapiPlayer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat hardwareFormat,
            string nodeCustomName,
            ILogger logger,
            MMDevice deviceHandle,
            TimeSpan? latency = null,
            bool exclusiveMode = false)
            : base(graph, hardwareFormat, nameof(WasapiPlayer), nodeCustomName, logger)
        {
            int latencyMs = ((int)latency.GetValueOrDefault(DEFAULT_LATENCY).TotalMilliseconds).AssertPositive(nameof(latency));
            WasapiOut actualDevice;
            if (deviceHandle == null)
            {
                logger.Log($"Using default WASAPI output device with latency {latencyMs}");
                actualDevice = new WasapiOut(exclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared, useEventSync: true, latency: latencyMs);
            }
            else
            {
                logger.Log($"Using WASAPI output device \"{deviceHandle.FriendlyName}\" with latency " + latencyMs);
                actualDevice = new WasapiOut(
                    deviceHandle,
                    exclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared,
                    useEventSync: true,
                    latency: latencyMs);
            }

            base.InitializeWavePlayer(actualDevice);
        }
    }
}
