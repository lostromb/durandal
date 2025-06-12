using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NAudio.Devices
{
    /// <summary>
    /// A simple audio player device which uses the standard output device on the system.
    /// </summary>
    internal class WaveOutPlayer : AbstractNAudioWavePlayer
    {
        /// <summary>
        /// Constructs a new <see cref="WaveOutPlayer"/>
        /// </summary>
        /// <param name="graph">The graph to associated with this component.</param>
        /// <param name="hardwareFormat">The format to initialize the hardware device with.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger for errors in the audio graph</param>
        public WaveOutPlayer(WeakPointer<IAudioGraph> graph, AudioSampleFormat hardwareFormat, string nodeCustomName, ILogger logger)
            : base(graph, hardwareFormat, nameof(WaveOutPlayer), nodeCustomName, logger)
        {
            base.InitializeWavePlayer(new WaveOutEvent());
        }
    }
}