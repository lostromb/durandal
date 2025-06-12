using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Extensions.NAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.NAudio
{
    [TestClass]
    [DoNotParallelize]
    public class WasapiDeviceTests
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            NativePlatformUtils.SetGlobalResolver(null);
        }

        private static void ValidateTestSetupIsOk()
        {
            if (NativePlatformUtils.GetCurrentPlatform(DebugLogger.Default).OS != PlatformOperatingSystem.Windows)
            {
                Assert.Inconclusive("WASAPI audio driver is only supported on Windows");
            }
        }

        [TestMethod]
        public void TestWasapiDriverProperties()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            Assert.AreEqual("NAudioWasapi", driver.RenderDriverName);
            Assert.AreEqual("NAudioWasapi", driver.CaptureDriverName);
        }

        [TestMethod]
        public void TestWasapiDriverDoesntWorkOutsideOfWindows()
        {
            FakeNativeLibraryResolver fakeLibraryResolver = new FakeNativeLibraryResolver();
            fakeLibraryResolver.SetFakeMachinePlatformForTest(new OSAndArchitecture(PlatformOperatingSystem.Unix, PlatformArchitecture.Mips64));
            NativePlatformUtils.SetGlobalResolver(fakeLibraryResolver);
            try
            {
                ILogger logger = new ConsoleLogger();
                try
                {
                    IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
                    Assert.Fail("Should have thrown a PlatformNotSupportedException");
                }
                catch (PlatformNotSupportedException) { }
            }
            finally
            {
                /// BLAHHH messing with global state within this test case - bad code smell!!!
                NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            }
        }

        [TestMethod]
        public void TestWasapiListCaptureDevices()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            var deviceList = driver.ListCaptureDevices().ToList();
            if (deviceList.Count == 0)
            {
                Assert.Inconclusive("No WASAPI capture devices present");
            }

            foreach (var device in deviceList)
            {
                Assert.AreEqual(driver.RenderDriverName, device.DriverName);
                Assert.IsFalse(string.IsNullOrEmpty(device.DeviceFriendlyName));
            }
        }

        [TestMethod]
        public void TestWasapiListRenderDevices()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            var deviceList = driver.ListRenderDevices().ToList();
            if (deviceList.Count == 0)
            {
                Assert.Inconclusive("No WASAPI render devices present");
            }

            foreach (var device in deviceList)
            {
                Assert.AreEqual(driver.RenderDriverName, device.DriverName);
                Assert.IsFalse(string.IsNullOrEmpty(device.DeviceFriendlyName));
            }
        }

        [TestMethod]
        public void TestWasapiResolveDefaultCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No WASAPI capture devices present");
            }

            IAudioCaptureDeviceId resolvedId = driver.ResolveCaptureDevice(id.Id);
            Assert.AreEqual(id, resolvedId);
        }

        [TestMethod]
        public void TestWasapiResolveDefaultRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            IAudioRenderDeviceId id = driver.ListRenderDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No WASAPI render devices present");
            }

            IAudioRenderDeviceId resolvedId = driver.ResolveRenderDevice(id.Id);
            Assert.AreEqual(id, resolvedId);
        }

        [TestMethod]
        public void TestWasapiResolveUnknownCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
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
        public void TestWasapiResolveUnknownRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
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
        public void TestWasapiResolveUnknownRenderDevice2()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            Assert.IsNull(driver.ResolveRenderDevice("NAudioWasapi:notexistdevice"));
        }

        [TestMethod]
        public void TestWasapiOpenWrongDriverCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
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
        public void TestWasapiOpenUnknownCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
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
        public void TestWasapiOpenWrongDriverRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
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
        public void TestWasapiOpenUnknownRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
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
                            Id = "NAudioWasapi:NotExistDevice",
                            DeviceFriendlyName = "Literally a spoon",
                            DriverName = "NAudioWasapi"
                        },
                        new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }
            }
        }

        [TestMethod]
        public async Task TestWasapiOpenDefaultRenderDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            IAudioRenderDeviceId id = driver.ListRenderDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No WASAPI render devices present");
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
        public async Task TestWasapiOpenNullRenderDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
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
        public void TestWasapiOpenRenderDeviceInvalidSampleFormats()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            IAudioRenderDeviceId id = driver.ListRenderDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No WASAPI render devices present");
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
                catch (ArgumentException) { }
            }
        }

        [TestMethod]
        public async Task TestWasapiOpenDefaultCaptureDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No WASAPI capture devices present");
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
        public async Task TestWasapiOpenDefaultLoopbackCaptureDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));

            // If the default audio output device is not active, the mic won't record anything.
            // So create a dummy renderer here pushing silence to ensure we're good.
            using (IAudioGraph renderGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph captureGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioRenderDevice renderer = driver.OpenRenderDevice(null, renderGraph.AsWeakPointer(), AudioSampleFormat.Mono(44100), null))
            using (IAudioCaptureDevice loopback = driver.OpenCaptureDevice(WasapiDeviceDriver.DefaultLoopbackCaptureDevice, captureGraph.AsWeakPointer(), AudioSampleFormat.Mono(44100), null))
            using (PositionMeasuringAudioPipe positionMeasure = new PositionMeasuringAudioPipe(captureGraph.AsWeakPointer(), loopback.OutputFormat, null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(captureGraph.AsWeakPointer(), loopback.OutputFormat, null))
            using (SilenceAudioSampleSource silenceSource = new SilenceAudioSampleSource(renderGraph.AsWeakPointer(), renderer.InputFormat, null))
            {
                silenceSource.ConnectOutput(renderer);
                await renderer.StartPlayback(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                loopback.ConnectOutput(positionMeasure);
                positionMeasure.ConnectOutput(target);
                await loopback.StartCapture(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                await loopback.StartCapture(DefaultRealTimeProvider.Singleton).ConfigureAwait(false); // just to test redundant state on the device handler...
                Assert.IsTrue(loopback.IsActiveNode);
                Stopwatch testTimer = Stopwatch.StartNew();
                while (positionMeasure.Position == TimeSpan.Zero)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    Assert.IsTrue(testTimer.Elapsed < MAX_TEST_TIME, "Too much time passed before getting any mic input");
                }

                await loopback.StopCapture().ConfigureAwait(false);
                await loopback.StopCapture().ConfigureAwait(false); // just to test redundant state on the device handler...
                await renderer.StopPlayback().ConfigureAwait(false);
                renderer.DisconnectInput();

                try
                {
                    // assert that the device is an active node
                    await loopback.ReadAsync(new float[1], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task TestWasapiOpenNullCaptureDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
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
        public void TestWasapiOpenCaptureDeviceInvalidArguments()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WasapiDeviceDriver(logger.Clone("Driver"));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().FirstOrDefault();
            if (id == null)
            {
                Assert.Inconclusive("No WASAPI capture devices present");
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
