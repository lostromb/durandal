using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Audio.Hardware;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using OpenTK.Audio.OpenAL;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;

namespace Durandal.Extensions.OpenAL
{
    /// <summary>
    /// <see cref="IAudioDriver"/> implementation which uses OpenAL.
    /// </summary>
    public class OpenALDeviceDriver : IAudioDriver
    {
        private static readonly string DRIVER_NAME = "OpenAL";
        private readonly WeakPointer<IHighPrecisionWaitProvider> _mediaThreadTimer;
        private readonly ILogger _logger;

        /// <inheritdoc />
        public string RenderDriverName => DRIVER_NAME;

        /// <inheritdoc />
        public string CaptureDriverName => DRIVER_NAME;

        /// <summary>
        /// Constructs a new <see cref="OpenALDeviceDriver"/> for managing audio devices
        /// through the OpenAL library.
        /// </summary>
        /// <param name="logger">A logger</param>
        public OpenALDeviceDriver(
            ILogger logger,
            WeakPointer<IHighPrecisionWaitProvider> mediaThreadTimer)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _mediaThreadTimer = mediaThreadTimer.AssertNonNull(nameof(mediaThreadTimer));
        }

        /// <inheritdoc />
        public IEnumerable<IAudioCaptureDeviceId> ListCaptureDevices()
        {
            if (!ALC.IsExtensionPresent(ALDevice.Null, "ALC_EXT_CAPTURE"))
            {
                throw new PlatformNotSupportedException("The current OpenAL driver does not support capture extension");
            }

            if (ALC.IsExtensionPresent(ALDevice.Null, "ALC_ENUMERATE_ALL_EXT"))
            {
                int dev = 0;
                foreach (string deviceName in ALC.GetStringList(GetEnumerationStringList.CaptureDeviceSpecifier))
                {
                    yield return new OpenALCaptureDeviceId()
                    {
                        DeviceFriendlyName = deviceName,
                        InternalId = deviceName
                    };
                    dev++;
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<IAudioRenderDeviceId> ListRenderDevices()
        {
            if (ALC.IsExtensionPresent(ALDevice.Null, "ALC_ENUMERATE_ALL_EXT"))
            {
                int dev = 0;
                foreach (string deviceName in ALC.GetStringList(GetEnumerationStringList.DeviceSpecifier))
                {
                    yield return new OpenALRenderDeviceId()
                    {
                        DeviceFriendlyName = deviceName,
                        InternalId = deviceName
                    };

                    dev++;
                }
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

            return new OpenALCaptureDeviceId()
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

            return new OpenALRenderDeviceId()
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
            if (deviceId != null && !(deviceId is OpenALCaptureDeviceId))
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
                OpenALCaptureDeviceId castId = deviceId as OpenALCaptureDeviceId;
                deviceName = castId.InternalId;
            }

            return new OpenALMicrophone(
                graph,
                desiredFormat,
                nodeCustomName,
                _logger.Clone("OpenALMicrophone"),
                _mediaThreadTimer,
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
            if (deviceId != null && !(deviceId is OpenALRenderDeviceId))
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
                OpenALRenderDeviceId castId = deviceId as OpenALRenderDeviceId;
                deviceName = castId.InternalId;
            }

            return new OpenALAudioPlayer(
                graph,
                desiredFormat,
                nodeCustomName,
                _logger.Clone("OpenALSpeaker"),
                _mediaThreadTimer,
                deviceName,
                desiredLatency);
        }

        /// <summary>
        /// Internal capture device ID for this driver
        /// </summary>
        private class OpenALCaptureDeviceId : AbstractAudioCaptureDeviceId
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
        private class OpenALRenderDeviceId : AbstractAudioRenderDeviceId
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
