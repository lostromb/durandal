using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Extensions.NAudio.Devices;
using Durandal.Common.Audio.Hardware;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using System.Text.RegularExpressions;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.NAudio
{
    /// <summary>
    /// <see cref="IAudioDriver"/> implementation which uses the WaveIn / WaveOut APIs on Windows.
    /// </summary>
    public class WaveDeviceDriver : IAudioDriver
    {
        private static readonly string DRIVER_NAME = "NAudioWave";
        private readonly ILogger _logger;

        /// <summary>
        /// Constructs a new <see cref="WaveDeviceDriver"/> for managing audio devices
        /// through the WaveIn / WaveOut API on Windows.
        /// </summary>
        /// <param name="logger">A logger</param>
        public WaveDeviceDriver(ILogger logger)
        {
            if (NativePlatformUtils.GetCurrentPlatform(logger).OS != PlatformOperatingSystem.Windows)
            {
                throw new PlatformNotSupportedException("Wave audio is only supported on Windows OS");
            }

            _logger = logger.AssertNonNull(nameof(logger));
        }

        /// <inheritdoc />
        public string RenderDriverName => DRIVER_NAME;

        /// <inheritdoc />
        public string CaptureDriverName => DRIVER_NAME;

        /// <inheritdoc />
        public IEnumerable<IAudioCaptureDeviceId> ListCaptureDevices()
        {
            //int deviceCount = WaveInEvent.DeviceCount;
            //_logger.Log("Detected " + deviceCount + " WaveIn devices on this system");

            //for (int c = 0; c < deviceCount; c++)
            //{
            //    WaveInCapabilities dev = WaveInEvent.GetCapabilities(c);
            //    _logger.Log(string.Format("    {0}: \"{1}\" ({2} channels){3}{4}{5}{6} Khz",
            //        c,
            //        dev.ProductName,
            //        dev.Channels,
            //        dev.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_1M16) ? " 11" : string.Empty,
            //        dev.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_2M16) ? " 22" : string.Empty,
            //        dev.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_44M16) ? " 44" : string.Empty,
            //        dev.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_48M16) ? " 48" : string.Empty));

            //    yield return new WaveCaptureDeviceId()
            //    {
            //        InternalId = c,
            //        DeviceFriendlyName = dev.ProductName,
            //    };
            //}
            
            // We can't actually use device IDs because the WaveInEvent constructor doesn't even accept them
            // (even though there's code to enumerate them??)
            yield return DEFAULT_CAPTURE_DEVICE;
        }

        /// <inheritdoc />
        public IEnumerable<IAudioRenderDeviceId> ListRenderDevices()
        {
            // There's supposed to be a way to get WaveOut device count and use it, but I can't find the API
            yield return DEFAULT_RENDER_DEVICE;
        }

        /// <inheritdoc />
        public IAudioCaptureDeviceId ResolveCaptureDevice(string id)
        {
            if (!id.StartsWith(DRIVER_NAME, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Device ID {id} does not belong to this driver");
            }

            return DEFAULT_CAPTURE_DEVICE;
        }

        /// <inheritdoc />
        public IAudioRenderDeviceId ResolveRenderDevice(string id)
        {
            if (!id.StartsWith(DRIVER_NAME, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Device ID {id} does not belong to this driver");
            }

            return DEFAULT_RENDER_DEVICE;
        }

        /// <inheritdoc />
        public IAudioCaptureDevice OpenCaptureDevice(
            IAudioCaptureDeviceId deviceId,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat desiredFormat,
            string nodeCustomName,
            TimeSpan? desiredLatency = null)
        {
            graph.AssertNonNull(nameof(graph));
            desiredFormat.AssertNonNull(nameof(desiredFormat));
            if (deviceId != null && !(deviceId is WaveCaptureDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            if (desiredLatency.HasValue && desiredLatency.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(desiredLatency));
            }

            return new WaveInMicrophone(graph, desiredFormat, nodeCustomName, _logger.Clone("WaveInMicrophone"));
        }

        /// <inheritdoc />
        public IAudioRenderDevice OpenRenderDevice(
            IAudioRenderDeviceId deviceId,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat desiredFormat,
            string nodeCustomName,
            TimeSpan? desiredLatency = null)
        {
            graph.AssertNonNull(nameof(graph));
            desiredFormat.AssertNonNull(nameof(desiredFormat));
            if (deviceId != null && !(deviceId is WaveRenderDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            if (desiredLatency.HasValue && desiredLatency.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(desiredLatency));
            }

            return new WaveOutPlayer(graph, desiredFormat, nodeCustomName, _logger.Clone("WaveOutPlayer"));
        }

        private static readonly IAudioCaptureDeviceId DEFAULT_CAPTURE_DEVICE =
            new WaveCaptureDeviceId()
            {
                InternalId = $"{DRIVER_NAME}:Default",
                DeviceFriendlyName = "Default Audio In"
            };

        private static readonly IAudioRenderDeviceId DEFAULT_RENDER_DEVICE =
            new WaveRenderDeviceId()
            {
                InternalId = $"{DRIVER_NAME}:Default",
                DeviceFriendlyName = "Default Audio Out"
            };

        /// <summary>
        /// Internal capture device ID for this driver
        /// </summary>
        private class WaveCaptureDeviceId : AbstractAudioCaptureDeviceId
        {
            /// <inheritdoc />
            public override string DriverName => DRIVER_NAME;

            /// <inheritdoc />
            public override string Id => InternalId;

            /// <inheritdoc />
            public override string DeviceFriendlyName { get; set; }

            internal string InternalId { get; set; }
        }

        /// <summary>
        /// Internal render device ID for this driver
        /// </summary>
        private class WaveRenderDeviceId : AbstractAudioRenderDeviceId
        {
            /// <inheritdoc />
            public override string DriverName => DRIVER_NAME;

            /// <inheritdoc />
            public override string Id => InternalId;

            /// <inheritdoc />
            public override string DeviceFriendlyName { get; set; }

            internal string InternalId { get; set; }
        }
    }
}
