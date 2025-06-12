using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Hardware
{
    /// <summary>
    /// Defines an audio driver where the recording and capture drivers are separate drivers.
    /// </summary>
    public class HybridAudioDriver : IAudioDriver
    {
        private readonly IAudioDriver _render;
        private readonly IAudioDriver _capture;

        public HybridAudioDriver(IAudioDriver renderDriver, IAudioDriver captureDriver)
        {
            _render = renderDriver.AssertNonNull(nameof(renderDriver));
            _capture = captureDriver.AssertNonNull(nameof(captureDriver));
        }

        /// <inheritdoc />
        public string RenderDriverName => _render.RenderDriverName;

        /// <inheritdoc />
        public string CaptureDriverName => _capture.CaptureDriverName;

        /// <inheritdoc />
        public IEnumerable<IAudioCaptureDeviceId> ListCaptureDevices()
        {
            return _capture.ListCaptureDevices();
        }

        /// <inheritdoc />
        public IEnumerable<IAudioRenderDeviceId> ListRenderDevices()
        {
            return _render.ListRenderDevices();
        }

        /// <inheritdoc />
        public IAudioCaptureDevice OpenCaptureDevice(IAudioCaptureDeviceId deviceId, WeakPointer<IAudioGraph> graph, AudioSampleFormat desiredFormat, string nodeCustomName, TimeSpan? desiredLatency = null)
        {
            return _capture.OpenCaptureDevice(deviceId, graph, desiredFormat, nodeCustomName, desiredLatency);
        }

        /// <inheritdoc />
        public IAudioRenderDevice OpenRenderDevice(IAudioRenderDeviceId deviceId, WeakPointer<IAudioGraph> graph, AudioSampleFormat desiredFormat, string nodeCustomName, TimeSpan? desiredLatency = null)
        {
            return _render.OpenRenderDevice(deviceId, graph, desiredFormat, nodeCustomName, desiredLatency);
        }

        /// <inheritdoc />
        public IAudioCaptureDeviceId ResolveCaptureDevice(string id)
        {
            return _capture.ResolveCaptureDevice(id);
        }

        /// <inheritdoc />
        public IAudioRenderDeviceId ResolveRenderDevice(string id)
        {
            return _render.ResolveRenderDevice(id);
        }
    }
}
