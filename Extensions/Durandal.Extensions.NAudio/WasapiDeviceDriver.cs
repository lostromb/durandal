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
using NAudio.CoreAudioApi;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.NAudio
{
    /// <summary>
    /// <see cref="IAudioDriver"/> implementation which uses WASAPI on Windows.
    /// </summary>
    public class WasapiDeviceDriver : IAudioDriver
    {
        private static readonly WasapiCaptureDeviceId DEFAULT_LOOPBACK_DEVICE = new WasapiCaptureDeviceId()
        {
            InternalId = "Loopback:default",
            DeviceFriendlyName = "Default Loopback Audio Capture Device"
        };

        private static readonly string DRIVER_NAME = "NAudioWasapi";
        private readonly ILogger _logger;
        private readonly bool _useExclusiveMode;

        /// <inheritdoc />
        public string RenderDriverName => DRIVER_NAME;

        /// <inheritdoc />
        public string CaptureDriverName => DRIVER_NAME;

        /// <summary>
        /// Constructs a new <see cref="WasapiDeviceDriver"/> for managing audio devices
        /// through the DirectSound API on Windows.
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <param name="useExclusiveMode">If true, use WASAPI exclusive mode for lower latency</param>
        public WasapiDeviceDriver(ILogger logger, bool useExclusiveMode = false)
        {
            if (NativePlatformUtils.GetCurrentPlatform(logger).OS != PlatformOperatingSystem.Windows)
            {
                throw new PlatformNotSupportedException("WASAPI audio is only supported on Windows OS");
            }

            _logger = logger.AssertNonNull(nameof(logger));
            _useExclusiveMode = useExclusiveMode;
        }

        /// <summary>
        /// Gets the static device ID for the WASAPI audio loopback microphone (that is, a
        /// virtual microphone which captures whatever audio is currently playing on the system).
        /// </summary>
        public static IAudioCaptureDeviceId DefaultLoopbackCaptureDevice => DEFAULT_LOOPBACK_DEVICE;

        /// <inheritdoc />
        public IEnumerable<IAudioCaptureDeviceId> ListCaptureDevices()
        {
            using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
            {
                MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                foreach (MMDevice device in devices)
                {
                    using (device)
                    {
                        yield return new WasapiCaptureDeviceId()
                        {
                            InternalId = device.ID,
                            DeviceFriendlyName = device.DeviceFriendlyName,
                        };
                    }
                }

                yield return DEFAULT_LOOPBACK_DEVICE;

                devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (MMDevice device in devices)
                {
                    using (device)
                    {
                        yield return new WasapiCaptureDeviceId()
                        {
                            InternalId = "Loopback:" + device.ID,
                            DeviceFriendlyName = "(Loopback) " + device.DeviceFriendlyName,
                        };
                    }
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<IAudioRenderDeviceId> ListRenderDevices()
        {
            using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
            {
                MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (MMDevice device in devices)
                {
                    using (device)
                    {
                        yield return new WasapiRenderDeviceId()
                        {
                            InternalId = device.ID,
                            DeviceFriendlyName = device.DeviceFriendlyName,
                        };
                    }
                }
            }
        }

        /// <inheritdoc />
        public IAudioCaptureDeviceId ResolveCaptureDevice(string id)
        {
            if (id.Length <= DRIVER_NAME.Length + 1 ||
                !id.StartsWith(DRIVER_NAME))
            {
                throw new ArgumentException($"Invalid audio device ID {id}");
            }

            if (string.Equals(id, DEFAULT_LOOPBACK_DEVICE.Id, StringComparison.Ordinal))
            {
                return DEFAULT_LOOPBACK_DEVICE;
            }

            IAudioCaptureDeviceId returnVal = null;
            string internalId = id.Substring(DRIVER_NAME.Length + 1);
            using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
            {
                if (internalId.StartsWith("Loopback:", StringComparison.Ordinal))
                {
                    MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    foreach (MMDevice device in devices)
                    {
                        using (device)
                        {
                            string loopbackId = "Loopback:" + device.ID;
                            if (returnVal == null && string.Equals(loopbackId, internalId, StringComparison.Ordinal))
                            {
                                returnVal = new WasapiCaptureDeviceId()
                                {
                                    InternalId = loopbackId,
                                    DeviceFriendlyName = "(Loopback) " + device.DeviceFriendlyName,
                                };
                            }
                        }
                    }
                }
                else
                {
                    MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                    foreach (MMDevice device in devices)
                    {
                        using (device)
                        {
                            if (returnVal == null && string.Equals(device.ID, internalId, StringComparison.Ordinal))
                            {
                                returnVal = new WasapiCaptureDeviceId()
                                {
                                    InternalId = device.ID,
                                    DeviceFriendlyName = device.DeviceFriendlyName,
                                };
                            }
                        }
                    }
                }
            }

            return returnVal;
        }

        /// <inheritdoc />
        public IAudioRenderDeviceId ResolveRenderDevice(string id)
        {
            if (id.Length <= DRIVER_NAME.Length + 1 ||
                !id.StartsWith(DRIVER_NAME))
            {
                throw new ArgumentException($"Invalid audio device ID {id}");
            }

            IAudioRenderDeviceId returnVal = null;
            string internalId = id.Substring(DRIVER_NAME.Length + 1);
            using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
            {
                MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (MMDevice device in devices)
                {
                    using (device)
                    {
                        if (returnVal == null && string.Equals(device.ID, internalId, StringComparison.Ordinal))
                        {
                            returnVal = new WasapiRenderDeviceId()
                            {
                                InternalId = device.ID,
                                DeviceFriendlyName = device.DeviceFriendlyName,
                            };
                        };
                    }
                }
            }

            return returnVal;
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
            if (deviceId != null && !(deviceId is WasapiCaptureDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            if (desiredFormat.ChannelMapping == MultiChannelMapping.Stereo_R_L)
            {
                throw new ArgumentException("Stereo audio in NAudio must be L-R");
            }

            WasapiCaptureDeviceId castDeviceId = deviceId as WasapiCaptureDeviceId;
            MMDevice actualDevice = null;
            bool isLoopback = false;
            if (castDeviceId != null)
            {
                using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
                {
                    if (castDeviceId.InternalId.StartsWith("Loopback:", StringComparison.Ordinal))
                    {
                        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                        {
                            string loopbackId = "Loopback:" + device.ID;
                            if (string.Equals(castDeviceId.InternalId, loopbackId, StringComparison.Ordinal))
                            {
                                actualDevice = device;
                                isLoopback = true;
                                _logger.Log($"Using WASAPI loopback capture on output device {device.DeviceFriendlyName}");
                            }
                            else
                            {
                                device.Dispose();
                            }
                        }
                    }
                    else
                    {
                        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                        {
                            if (string.Equals(castDeviceId.InternalId, device.ID, StringComparison.Ordinal))
                            {
                                actualDevice = device;
                                _logger.Log($"Using WASAPI capture device {device.DeviceFriendlyName}");
                            }
                            else
                            {
                                device.Dispose();
                            }
                        }
                    }
                }
            }

            if (castDeviceId != null && string.Equals(castDeviceId.Id, DEFAULT_LOOPBACK_DEVICE.Id))
            {
                actualDevice = WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice();
                isLoopback = true;
                _logger.Log($"Using WASAPI loopback capture on default output device");
            }

            if (deviceId != null && actualDevice == null)
            {
                throw new ArgumentException($"Device ID {deviceId.Id} did not resolve to an actual audio device on this system");
            }

            return new WasapiMicrophone(
                graph,
                nodeCustomName,
                _logger.Clone("WasapiMic"),
                actualDevice,
                desiredLatency,
                isLoopback);
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
            if (deviceId != null && !(deviceId is WasapiRenderDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            WasapiRenderDeviceId castDeviceId = deviceId as WasapiRenderDeviceId;
            MMDevice actualDevice = null;
            if (castDeviceId != null)
            {
                using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
                {
                    foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                    {
                        if (string.Equals(castDeviceId.InternalId, device.ID, StringComparison.Ordinal))
                        {
                            actualDevice = device;
                        }
                        else
                        {
                            device.Dispose();
                        }
                    }
                }
            }

            if (deviceId != null && actualDevice == null)
            {
                throw new ArgumentException($"Device ID {deviceId.Id} did not resolve to an actual audio device on this system");
            }

            return new WasapiPlayer(
                graph,
                desiredFormat,
                nodeCustomName,
                _logger.Clone("WasapiSpeaker"),
                actualDevice,
                desiredLatency,
                _useExclusiveMode);
        }

        /// <summary>
        /// Internal capture device ID for this driver
        /// It'll look something like "{0.0.0.00000000}.{a1ff59ad-083a-4306-9660-d839daf37975}"
        /// </summary>
        private class WasapiCaptureDeviceId : AbstractAudioCaptureDeviceId
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
        /// It'll look something like "{0.0.0.00000000}.{a1ff59ad-083a-4306-9660-d839daf37975}"
        /// </summary>
        private class WasapiRenderDeviceId : AbstractAudioRenderDeviceId
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
