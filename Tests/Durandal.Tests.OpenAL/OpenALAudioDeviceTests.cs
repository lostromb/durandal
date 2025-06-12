using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Extensions.OpenAL;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.OpenAL
{
    [TestClass]
    [DoNotParallelize]
    public class OpenALDeviceTests
    {
        private static IHighPrecisionWaitProvider _mediaThreadTimer;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            _mediaThreadTimer = new Win32HighPrecisionWaitProvider();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            NativePlatformUtils.SetGlobalResolver(null);
            _mediaThreadTimer.Dispose();
        }

        private static void ValidateTestSetupIsOk()
        {
            try
            {
                ALC.GetError(ALDevice.Null);
            }
            catch (DllNotFoundException)
            {
                Assert.Inconclusive("OpenAL libraries are not available");
            }
        }

        [TestMethod]
        public void TestOpenALDriverProperties()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            Assert.AreEqual("OpenAL", driver.RenderDriverName);
            Assert.AreEqual("OpenAL", driver.CaptureDriverName);
        }

        [TestMethod]
        public void TestOpenALListCaptureDevices()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            var deviceList = driver.ListCaptureDevices().ToList();
            if (deviceList.Count == 0)
            {
                Assert.Inconclusive("No OpenAL capture devices present");
            }

            foreach (var device in deviceList)
            {
                Assert.AreEqual(driver.RenderDriverName, device.DriverName);
                Assert.IsFalse(string.IsNullOrEmpty(device.DeviceFriendlyName));
            }
        }

        [TestMethod]
        public void TestOpenALListRenderDevices()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            var deviceList = driver.ListRenderDevices().ToList();
            if (deviceList.Count == 0)
            {
                Assert.Inconclusive("No OpenAL render devices present");
            }

            foreach (var device in deviceList)
            {
                Assert.AreEqual(driver.RenderDriverName, device.DriverName);
                Assert.IsFalse(string.IsNullOrEmpty(device.DeviceFriendlyName));
            }
        }

        [TestMethod]
        public void TestOpenALResolveDefaultCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No OpenAL capture devices present");
            }

            IAudioCaptureDeviceId resolvedId = driver.ResolveCaptureDevice(id.Id);
            Assert.AreEqual(id, resolvedId);
        }

        [TestMethod]
        public void TestOpenALResolveDefaultRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            IAudioRenderDeviceId id = driver.ListRenderDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No OpenAL render devices present");
            }

            IAudioRenderDeviceId resolvedId = driver.ResolveRenderDevice(id.Id);
            Assert.AreEqual(id, resolvedId);
        }

        [TestMethod]
        public void TestOpenALResolveUnknownCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            IAudioCaptureDeviceId id = new BogusDeviceId()
            {
                Id = "SpoonAudio:0",
                DeviceFriendlyName = "Literally a spoon",
                DriverName = "SpoonAudio"
            };

            try
            {
                driver.ResolveCaptureDevice(id.Id);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestOpenALResolveUnknownRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            IAudioCaptureDeviceId id = new BogusDeviceId()
            {
                Id = "SpoonAudio:0",
                DeviceFriendlyName = "Literally a spoon",
                DriverName = "SpoonAudio"
            };

            try
            {
                driver.ResolveRenderDevice(id.Id);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestOpenALOpenWrongDriverCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            {
                try
                {
                    driver.OpenCaptureDevice(
                        new BogusDeviceId()
                        {
                            Id = "SpoonAudio:0",
                            DeviceFriendlyName = "Literally a spoon",
                            DriverName = "SpoonAudio"
                        },
                        new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }
            }
        }

        [TestMethod]
        public void TestOpenALOpenUnknownCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            {
                IAudioCaptureDeviceId actualId = driver.ListCaptureDevices().FirstOrDefault();
                if (actualId == null)
                {
                    Assert.Inconclusive("No capture devices on this system");
                }

                // Reach in and mess up the internal ID for the actual device so it no longer matches
                actualId.GetType().GetProperty("InternalId", BindingFlags.Instance | BindingFlags.Public).SetValue(actualId, "NotExistDevice");

                try
                {
                    driver.OpenCaptureDevice(actualId, new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }
            }
        }

        [TestMethod]
        public void TestOpenALOpenWrongDriverRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            {
                IAudioRenderDeviceId actualId = driver.ListRenderDevices().FirstOrDefault();
                if (actualId == null)
                {
                    Assert.Inconclusive("No render devices on this system");
                }

                // Reach in and mess up the internal ID for the actual device so it no longer matches
                actualId.GetType().GetProperty("InternalId", BindingFlags.Instance | BindingFlags.Public).SetValue(actualId, "NotExistDevice");

                try
                {
                    driver.OpenRenderDevice(actualId, new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }
            }
        }

        [TestMethod]
        public void TestOpenALOpenUnknownRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            {
                try
                {
                    driver.OpenRenderDevice(
                        new BogusDeviceId()
                        {
                            Id = "SpoonAudio:0",
                            DeviceFriendlyName = "Literally a spoon",
                            DriverName = "SpoonAudio"
                        },
                        new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }

                try
                {
                    driver.OpenRenderDevice(
                        new BogusDeviceId()
                        {
                            Id = "NAudioOpenAL:NotExistDevice",
                            DeviceFriendlyName = "Literally a spoon",
                            DriverName = "NAudioOpenAL"
                        },
                        new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }
            }
        }

        [TestMethod]
        public async Task TestOpenALOpenDefaultRenderDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            IAudioRenderDeviceId id = driver.ListRenderDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No OpenAL render devices present");
            }

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioRenderDevice device = driver.OpenRenderDevice(id, new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(44100), null))
            using (PositionMeasuringAudioPipe positionMeasure = new PositionMeasuringAudioPipe(new WeakPointer<IAudioGraph>(graph), device.InputFormat, null))
            using (SilenceAudioSampleSource source = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), device.InputFormat, null))
            {
                source.ConnectOutput(positionMeasure);
                positionMeasure.ConnectOutput(device);
                await device.StartPlayback(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                await device.StartPlayback(DefaultRealTimeProvider.Singleton).ConfigureAwait(false); // just to test redundant state on the device handler...
                Assert.IsTrue(device.IsActiveNode);
                Stopwatch testTimer = Stopwatch.StartNew();
                while (positionMeasure.Position == TimeSpan.Zero)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    Assert.IsTrue(testTimer.Elapsed < MAX_TEST_TIME, "Too much time passed before speakers pulled any samples");
                }

                await device.StopPlayback().ConfigureAwait(false);
                await device.StopPlayback().ConfigureAwait(false); // just to test redundant state on the device handler...

                try
                {
                    // assert that the device is an active node
                    await device.WriteAsync(new float[1], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task TestOpenALOpenNullRenderDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioRenderDevice device = driver.OpenRenderDevice(null, new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(44100), null))
            using (PositionMeasuringAudioPipe positionMeasure = new PositionMeasuringAudioPipe(new WeakPointer<IAudioGraph>(graph), device.InputFormat, null))
            using (SilenceAudioSampleSource source = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), device.InputFormat, null))
            {
                source.ConnectOutput(positionMeasure);
                positionMeasure.ConnectOutput(device);
                await device.StartPlayback(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Stopwatch testTimer = Stopwatch.StartNew();
                while (positionMeasure.Position == TimeSpan.Zero)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    Assert.IsTrue(testTimer.Elapsed < MAX_TEST_TIME, "Too much time passed before speakers pulled any samples");
                }

                await device.StopPlayback().ConfigureAwait(false);
            }
        }

        [TestMethod]
        public void TestOpenALOpenRenderDeviceInvalidSampleFormats()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            IAudioRenderDeviceId id = driver.ListRenderDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No OpenAL render devices present");
            }

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                try
                {
                    driver.OpenRenderDevice(id, new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, MultiChannelMapping.Surround_7_1ch), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }

                try
                {
                    driver.OpenRenderDevice(id, new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, MultiChannelMapping.Stereo_R_L), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }

                try
                {
                    driver.OpenRenderDevice(id, new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, MultiChannelMapping.Monaural), null, TimeSpan.Zero);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public async Task TestOpenALOpenDefaultCaptureDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No OpenAL capture devices present");
            }

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioCaptureDevice device = driver.OpenCaptureDevice(id, new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(44100), null))
            using (PositionMeasuringAudioPipe positionMeasure = new PositionMeasuringAudioPipe(new WeakPointer<IAudioGraph>(graph), device.OutputFormat, null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), device.OutputFormat, null))
            {
                device.ConnectOutput(positionMeasure);
                positionMeasure.ConnectOutput(target);
                await device.StartCapture(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                await device.StartCapture(DefaultRealTimeProvider.Singleton).ConfigureAwait(false); // just to test redundant state on the device handler...
                Assert.IsTrue(device.IsActiveNode);
                Stopwatch testTimer = Stopwatch.StartNew();
                while (positionMeasure.Position == TimeSpan.Zero)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    Assert.IsTrue(testTimer.Elapsed < MAX_TEST_TIME, "Too much time passed before getting any mic input");
                }

                await device.StopCapture().ConfigureAwait(false);
                await device.StopCapture().ConfigureAwait(false); // just to test redundant state on the device handler...

                try
                {
                    // assert that the device is an active node
                    await device.ReadAsync(new float[1], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task TestOpenALOpenNullCaptureDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioCaptureDevice device = driver.OpenCaptureDevice(null, new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(44100), null))
            using (PositionMeasuringAudioPipe positionMeasure = new PositionMeasuringAudioPipe(new WeakPointer<IAudioGraph>(graph), device.OutputFormat, null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), device.OutputFormat, null))
            {
                device.ConnectOutput(positionMeasure);
                positionMeasure.ConnectOutput(target);
                await device.StartCapture(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Stopwatch testTimer = Stopwatch.StartNew();
                while (positionMeasure.Position == TimeSpan.Zero)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    Assert.IsTrue(testTimer.Elapsed < MAX_TEST_TIME, "Too much time passed before getting any mic input");
                }

                await device.StopCapture().ConfigureAwait(false);
            }
        }

        [TestMethod]
        public void TestOpenALOpenCaptureDeviceInvalidArguments()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new OpenALDeviceDriver(logger.Clone("Driver"), new WeakPointer<IHighPrecisionWaitProvider>(_mediaThreadTimer));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No OpenAL capture devices present");
            }

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                try
                {
                    driver.OpenCaptureDevice(id, new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, MultiChannelMapping.Stereo_R_L), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }

                try
                {
                    driver.OpenCaptureDevice(id, new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, MultiChannelMapping.Monaural), null, TimeSpan.Zero);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }
            }
        }

        private class BogusDeviceId : IAudioCaptureDeviceId, IAudioRenderDeviceId
        {
            public string Id { get; set; }

            public string DriverName { get; set; }

            public string DeviceFriendlyName { get; set; }

            public bool Equals(IAudioCaptureDeviceId other)
            {
                return string.Equals(this.Id, other.Id, StringComparison.Ordinal);
            }

            public bool Equals(IAudioRenderDeviceId other)
            {
                return string.Equals(this.Id, other.Id, StringComparison.Ordinal);
            }
        }
    }
}
