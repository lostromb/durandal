using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Audio.Hardware;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Extensions.SDL;
using static SDL2.SDL;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.SDL2Audio
{
    /// <summary>
    /// <see cref="IAudioDriver"/> implementation which uses SDL2.
    /// </summary>
    public class SDL2DeviceDriver : IAudioDriver
    {
        private static readonly string DRIVER_NAME = "SDL2";
        private readonly ILogger _logger;

        /// <inheritdoc />
        public string RenderDriverName => DRIVER_NAME;

        /// <inheritdoc />
        public string CaptureDriverName => DRIVER_NAME;

        /// <summary>
        /// Constructs a new <see cref="SDL2DeviceDriver"/> for managing audio devices
        /// through the SDL2 library.
        /// </summary>
        /// <param name="logger">A logger</param>
        public SDL2DeviceDriver(ILogger logger)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            GlobalSDLState.Initialize();
        }

        /// <inheritdoc />
        public IEnumerable<IAudioCaptureDeviceId> ListCaptureDevices()
        {
            int deviceCount = SDL_GetNumAudioDevices(iscapture: 1);
            for (int dev = 0; dev < deviceCount; dev++)
            {
                string name = SDL_GetAudioDeviceName(dev, iscapture: 1);
                yield return new SDL2CaptureDeviceId()
                {
                    DeviceFriendlyName = name,
                    InternalId = name
                };
            }
        }

        /// <inheritdoc />
        public IEnumerable<IAudioRenderDeviceId> ListRenderDevices()
        {
            int deviceCount = SDL_GetNumAudioDevices(iscapture: 0);
            for (int dev = 0; dev < deviceCount; dev++)
            {
                string name = SDL_GetAudioDeviceName(dev, iscapture: 0);
                yield return new SDL2RenderDeviceId()
                {
                    DeviceFriendlyName = name,
                    InternalId = name
                };
            }
        }

        /// <inheritdoc />
        public IAudioCaptureDeviceId ResolveCaptureDevice(string id)
        {
            if (id.Length <= DRIVER_NAME.Length + 1 ||
                !id.StartsWith(DRIVER_NAME))
            {
                throw new ArgumentException("Invalid audio device ID");
            }

            string name = id.Substring(DRIVER_NAME.Length + 1);

            return new SDL2CaptureDeviceId()
            {
                InternalId = name,
                DeviceFriendlyName = name,
            };
        }

        /// <inheritdoc />
        public IAudioRenderDeviceId ResolveRenderDevice(string id)
        {
            if (id.Length <= DRIVER_NAME.Length + 1 ||
                !id.StartsWith(DRIVER_NAME))
            {
                throw new ArgumentException("Invalid audio device ID");
            }

            string name = id.Substring(DRIVER_NAME.Length + 1);

            return new SDL2RenderDeviceId()
            {
                InternalId = name,
                DeviceFriendlyName = name,
            };
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
            if (deviceId != null && !(deviceId is SDL2CaptureDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            if (desiredLatency.HasValue && desiredLatency.Value <= TimeSpan.Zero)
            {
                throw new ArgumentException(nameof(desiredLatency));
            }

            string deviceName = null;
            if (deviceId != null)
            {
                SDL2CaptureDeviceId castId = deviceId as SDL2CaptureDeviceId;
                deviceName = castId.InternalId;
            }

            return new SDL2Microphone(
                graph,
                desiredFormat,
                nodeCustomName,
                _logger.Clone("SDL2Microphone"),
                deviceName);
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
            if (deviceId != null && !(deviceId is SDL2RenderDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            if (desiredLatency.HasValue && desiredLatency.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(desiredLatency));
            }

            string deviceName = null;
            if (deviceId != null)
            {
                SDL2RenderDeviceId castId = deviceId as SDL2RenderDeviceId;
                deviceName = castId.InternalId;
            }

            return new SDL2AudioPlayer(
                graph,
                desiredFormat,
                nodeCustomName,
                _logger.Clone("SDL2Speaker"),
                deviceName,
                desiredLatency);
        }

        /// <summary>
        /// Internal capture device ID for this driver
        /// </summary>
        private class SDL2CaptureDeviceId : AbstractAudioCaptureDeviceId
        {
            /// <inheritdoc />
            public override string DriverName => DRIVER_NAME;

            /// <inheritdoc />
            public override string Id => $"{DriverName}:{InternalId}";

            /// <inheritdoc />
            public override string DeviceFriendlyName { get; set; }

            public string InternalId { get; set; }
        }

        /// <summary>
        /// Internal render device ID for this driver.
        /// </summary>
        private class SDL2RenderDeviceId : AbstractAudioRenderDeviceId
        {
            /// <inheritdoc />
            public override string DriverName => DRIVER_NAME;

            /// <inheritdoc />
            public override string Id => $"{DriverName}:{InternalId}";

            /// <inheritdoc />
            public override string DeviceFriendlyName { get; set; }

            public string InternalId { get; set; }
        }
    }
}
