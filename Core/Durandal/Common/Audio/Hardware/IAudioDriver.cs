using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Hardware
{
    public interface IAudioDriver
    {
        /// <summary>
        /// The name of this driver for rendering devices
        /// </summary>
        string RenderDriverName { get; }

        /// <summary>
        /// The name of this driver for capturing devices
        /// </summary>
        string CaptureDriverName { get; }

        /// <summary>
        /// Lists all capture devices (microphones) available to this driver.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IAudioCaptureDeviceId> ListCaptureDevices();

        /// <summary>
        /// Lists all render devices (speakers) available to this driver.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IAudioRenderDeviceId> ListRenderDevices();

        /// <summary>
        /// Resolves a stored ID string (from an IAudioCaptureDeviceId.Id field) to the structured ID object.
        /// Returns null if the device was not found. Throws an exception if the ID is invalid.
        /// </summary>
        /// <param name="id">The ID string to parse</param>
        /// <returns>The structured ID for this device, or null if not found.</returns>
        IAudioCaptureDeviceId ResolveCaptureDevice(string id);

        /// <summary>
        /// Resolves a stored ID string (from an IAudioRenderDeviceId.Id field) to the structured ID object.
        /// Returns null if the device was not found. Throws an exception if the ID is invalid.
        /// </summary>
        /// <param name="id">The ID string to parse</param>
        /// <returns>The structured ID for this device, or null if not found.</returns>
        IAudioRenderDeviceId ResolveRenderDevice(string id);

        /// <summary>
        /// Opens a capture device (microphone) supported by this audio driver.
        /// </summary>
        /// <param name="deviceId">The specific device to open, or NULL to let the driver pick a default.</param>
        /// <param name="graph">The audio graph that the component will be a part of</param>
        /// <param name="desiredFormat">The desired output format of the device. This is taken only as
        /// a hint at best, the driver is free to choose a suitable format based on what is actually supported.</param>
        /// <param name="nodeCustomName">The graph node name of the component being created.</param>
        /// <param name="desiredLatency">A hint for the buffer size to use on this device. Smaller buffers
        /// have lower latency but are more prone to stutters if you're not careful.</param>
        /// <returns>An audio capture device.</returns>
        IAudioCaptureDevice OpenCaptureDevice(
            IAudioCaptureDeviceId deviceId,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat desiredFormat,
            string nodeCustomName,
            TimeSpan? desiredLatency = null);

        /// <summary>
        /// Opens a render device (speakers) supported by this audio driver.
        /// </summary>
        /// <param name="deviceId">The specific device to open, or NULL to let the driver pick a default.</param>
        /// <param name="graph">The audio graph that the component will be a part of</param>
        /// <param name="desiredFormat">The desired output format of the device. This is taken only as
        /// a hint at best, the driver is free to choose a suitable format based on what is actually supported.</param>
        /// <param name="nodeCustomName">The graph node name of the component being created.</param>
        /// <param name="desiredLatency">A hint for the buffer size to use on this device. Smaller buffers
        /// have lower latency but are more prone to stutters if you're not careful.</param>
        /// <returns>An audio render device.</returns>
        IAudioRenderDevice OpenRenderDevice(
            IAudioRenderDeviceId deviceId,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat desiredFormat,
            string nodeCustomName,
            TimeSpan? desiredLatency = null);
    }
}
