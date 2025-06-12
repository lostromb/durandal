using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Audio.Hardware;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using ManagedBass;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.BassAudio
{
    /// <summary>
    /// <see cref="IAudioDriver"/> implementation which uses multiplatform BASS.
    /// </summary>
    public class BassDeviceDriver : IAudioDriver
    {
        private static readonly string DRIVER_NAME = "Bass";
        private readonly ILogger _logger;

        /// <inheritdoc />
        public string RenderDriverName => DRIVER_NAME;

        /// <inheritdoc />
        public string CaptureDriverName => DRIVER_NAME;

        /// <summary>
        /// Constructs a new <see cref="BassDeviceDriver"/> for managing audio devices
        /// through the BASS library.
        /// </summary>
        /// <param name="logger">A logger</param>
        public BassDeviceDriver(ILogger logger)
        {
            _logger = logger.AssertNonNull(nameof(logger));

            try
            {
                if (NativePlatformUtils.PrepareNativeLibrary("bass", _logger) != NativeLibraryStatus.Available)
                {
                    _logger.Log($"BASS library may not exist on this system. Things could break!", LogLevel.Wrn);
                }

                // Throw an exception now if we can't call into this function...
                _logger.Log($"Initializing BASS audio using library version {Bass.Version}...");
            }
            catch (BadImageFormatException e)
            {
                OSAndArchitecture platform = NativePlatformUtils.GetCurrentPlatform(_logger);
                _logger.Log($"FAILED to load Bass shared library. The local binary does not match the current runtime architecture {platform.Architecture}", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
            catch (DllNotFoundException e)
            {
                _logger.Log("FAILED to load Bass shared library. The library file was not found.", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
        }

        /// <inheritdoc />
        public IEnumerable<IAudioCaptureDeviceId> ListCaptureDevices()
        {
            IList<IAudioCaptureDeviceId> returnVal = new List<IAudioCaptureDeviceId>();
            try
            {
                for (int c = 0; c < Bass.RecordingDeviceCount; c++)
                {
                    try
                    {
                        DeviceInfo info = Bass.RecordGetDeviceInfo(c);
                        returnVal.Add(new BassCaptureDeviceId()
                        {
                            InternalId = c,
                            DeviceFriendlyName = info.Name
                        });
                        //_logger.Log(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "[{0}] Name:{1} Type:{2} Driver:{3}", c, info.Name, info.Type, info.Driver);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }

            return returnVal;
        }

        /// <inheritdoc />
        public IEnumerable<IAudioRenderDeviceId> ListRenderDevices()
        {
            IList<IAudioRenderDeviceId> returnVal = new List<IAudioRenderDeviceId>();
            try
            {
                for (int c = 0; c < Bass.DeviceCount; c++)
                {
                    try
                    {
                        DeviceInfo info = Bass.GetDeviceInfo(c);
                        returnVal.Add(new BassRenderDeviceId()
                        {
                            InternalId = c,
                            DeviceFriendlyName = info.Name
                        });
                        //_logger.Log(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "[{0}] Name:{1} Type:{2} Driver:{3}", c, info.Name, info.Type, info.Driver);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }

            return returnVal;
        }

        /// <inheritdoc />
        public IAudioCaptureDeviceId ResolveCaptureDevice(string id)
        {
            int ordinal;
            if (id.Length <= DRIVER_NAME.Length + 1 ||
                !id.StartsWith(DRIVER_NAME) ||
                !int.TryParse(id.Substring(DRIVER_NAME.Length + 1), out ordinal))
            {
                throw new ArgumentException("Invalid audio device ID");
            }

            if (ordinal >= Bass.RecordingDeviceCount)
            {
                return null;
            }

            DeviceInfo info = Bass.RecordGetDeviceInfo(ordinal);
            return new BassCaptureDeviceId()
            {
                InternalId = ordinal,
                DeviceFriendlyName = info.Name
            };
        }

        /// <inheritdoc />
        public IAudioRenderDeviceId ResolveRenderDevice(string id)
        {
            int ordinal;
            if (id.Length <= DRIVER_NAME.Length + 1 ||
                !id.StartsWith(DRIVER_NAME) ||
                !int.TryParse(id.Substring(DRIVER_NAME.Length + 1), out ordinal))
            {
                throw new ArgumentException("Invalid audio device ID");
            }

            if (ordinal >= Bass.DeviceCount)
            {
                return null;
            }

            DeviceInfo info = Bass.GetDeviceInfo(ordinal);
            return new BassRenderDeviceId()
            {
                InternalId = ordinal,
                DeviceFriendlyName = info.Name
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
            if (deviceId != null && !(deviceId is BassCaptureDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            if (desiredLatency.HasValue && desiredLatency.Value <= TimeSpan.Zero)
            {
                throw new ArgumentException(nameof(desiredLatency));
            }

            int deviceOrdinal = -1;
            if (deviceId != null)
            {
                BassCaptureDeviceId castId = deviceId as BassCaptureDeviceId;

                if (castId.InternalId >= Bass.RecordingDeviceCount)
                {
                    throw new ArgumentOutOfRangeException($"{castId.Id} does not refer to an actual device on this system");
                }

                deviceOrdinal = castId.InternalId;
            }

            return new BassMicrophone(
                graph,
                desiredFormat,
                nodeCustomName,
                _logger.Clone("BassMicrophone"),
                deviceOrdinal);
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
            if (deviceId != null && !(deviceId is BassRenderDeviceId))
            {
                throw new ArgumentException($"Audio device ID is not one provided by this driver. Expected {this.RenderDriverName} but got {deviceId.DriverName}");
            }

            int deviceOrdinal = -1;
            if (deviceId != null)
            {
                BassRenderDeviceId castId = deviceId as BassRenderDeviceId;

                if (castId.InternalId >= Bass.DeviceCount)
                {
                    throw new ArgumentOutOfRangeException($"{castId.Id} does not refer to an actual device on this system");
                }

                deviceOrdinal = castId.InternalId;
            }

            return new BassAudioPlayer(
                graph,
                desiredFormat,
                nodeCustomName,
                _logger.Clone("BassSpeaker"),
                deviceOrdinal,
                desiredLatency);
        }

        /// <summary>
        /// Internal capture device ID for this driver
        /// </summary>
        private class BassCaptureDeviceId : AbstractAudioCaptureDeviceId
        {
            /// <inheritdoc />
            public override string DriverName => DRIVER_NAME;

            /// <inheritdoc />
            public override string Id => $"{DriverName}:{InternalId}";

            /// <inheritdoc />
            public override string DeviceFriendlyName { get; set; }

            public int InternalId { get; set; }
        }

        /// <summary>
        /// Internal render device ID for this driver.
        /// </summary>
        private class BassRenderDeviceId : AbstractAudioRenderDeviceId
        {
            /// <inheritdoc />
            public override string DriverName => DRIVER_NAME;

            /// <inheritdoc />
            public override string Id => $"{DriverName}:{InternalId}";

            /// <inheritdoc />
            public override string DeviceFriendlyName { get; set; }

            public int InternalId { get; set; }
        }
    }
}
