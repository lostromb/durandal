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
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.NAudio
{
    /// <summary>
    /// <see cref="IAudioDriver"/> implementation which uses the DirectSound API on Windows.
    /// </summary>
    public class DirectSoundDeviceDriver : IAudioDriver
    {
        private static readonly string DRIVER_NAME = "NAudioDSound";
        private readonly ILogger _logger;

        /// <inheritdoc />
        public string RenderDriverName => DRIVER_NAME;
        /// <inheritdoc />
        public string CaptureDriverName => "null";

        /// <summary>
        /// Constructs a new <see cref="DirectSoundDeviceDriver"/> for managing audio devices
        /// through the DirectSound API on Windows.
        /// </summary>
        /// <param name="logger">A logger</param>
        public DirectSoundDeviceDriver(ILogger logger)
        {
            if (NativePlatformUtils.GetCurrentPlatform(logger).OS != PlatformOperatingSystem.Windows)
            {
                throw new PlatformNotSupportedException("DirectSound audio is only supported on Windows OS");
            }

            _logger = logger.AssertNonNull(nameof(logger));
        }

        /// <inheritdoc />
        public IEnumerable<IAudioCaptureDeviceId> ListCaptureDevices()
        {
            yield break;
        }

        /// <inheritdoc />
        public IEnumerable<IAudioRenderDeviceId> ListRenderDevices()
        {
            foreach (DirectSoundDeviceInfo dev in DirectSoundOut.Devices)
            {
                yield return new DSoundRenderDeviceId()
                {
                    InternalId = dev.Guid,
                    DeviceFriendlyName = dev.Description
                };
            }
        }

        /// <inheritdoc />
        public IAudioCaptureDeviceId ResolveCaptureDevice(string id)
        {
            throw new NotSupportedException("Audio capture with DirectSound is not implemented");
        }

        /// <inheritdoc />
        public IAudioRenderDeviceId ResolveRenderDevice(string id)
        {
            if (id.Length < (DRIVER_NAME.Length + 1) ||
                !id.StartsWith(DRIVER_NAME, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Invalid audio device ID {id}");
            }

            Guid guid;
            if (!Guid.TryParse(id.Substring(DRIVER_NAME.Length + 1), out guid))
            {
                throw new ArgumentException($"Invalid audio device ID {id}");
            }

            foreach (DirectSoundDeviceInfo dev in DirectSoundOut.Devices)
            {
                if (guid == dev.Guid)
                {
                    return new DSoundRenderDeviceId()
                    {
                        InternalId = dev.Guid,
                        DeviceFriendlyName = dev.Description
                    };
                }
            }

            return null;
        }

        /// <inheritdoc />
        public IAudioCaptureDevice OpenCaptureDevice(
            IAudioCaptureDeviceId deviceId,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat desiredFormat,
            string nodeCustomName,
            TimeSpan? desiredLatency = null)
        {
            throw new NotSupportedException("Audio capture with DirectSound is not implemented");
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
            if (deviceId != null && !(deviceId is DSoundRenderDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            DSoundRenderDeviceId castDeviceId = deviceId as DSoundRenderDeviceId;
            DirectSoundDeviceInfo actualDeviceInfo = null;
            if (castDeviceId != null)
            {
                foreach (DirectSoundDeviceInfo dev in DirectSoundOut.Devices)
                {
                    if (dev.Guid == castDeviceId.InternalId)
                    {
                        actualDeviceInfo = dev;
                        break;
                    }
                }

                if (actualDeviceInfo == null)
                {
                    throw new ArgumentException($"No audio output device exists with guid \"{deviceId.Id}\".");
                }
            }

            return new DirectSoundPlayer(
                graph,
                desiredFormat,
                nodeCustomName,
                _logger.Clone("DSoundPlayer"),
                actualDeviceInfo,
                desiredLatency);
        }

        /// <summary>
        /// Internal render device ID for this driver.
        /// ID will look like a regular GUID
        /// </summary>
        private class DSoundRenderDeviceId : AbstractAudioRenderDeviceId
        {
            /// <inheritdoc />
            public override string DriverName => DRIVER_NAME;

            /// <inheritdoc />
            public override string Id => $"{DRIVER_NAME}:{InternalId}";

            /// <inheritdoc />
            public override string DeviceFriendlyName { get; set; }

            public Guid InternalId { get; set; }
        }
    }
}
