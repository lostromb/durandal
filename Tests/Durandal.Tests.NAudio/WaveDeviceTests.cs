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
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.NAudio
{
    [TestClass]
    [DoNotParallelize]
    public class WaveDeviceTests
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
                Assert.Inconclusive("Wave audio driver is only supported on Windows");
            }

            if (WaveInEvent.DeviceCount == 0)
            {
                Assert.Inconclusive("No capture devices are present on the current system");
            }
        }

        [TestMethod]
        public void TestWaveDriverProperties()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            Assert.AreEqual("NAudioWave", driver.RenderDriverName);
            Assert.AreEqual("NAudioWave", driver.CaptureDriverName);
        }

        [TestMethod]
        public void TestWaveDriverDoesntWorkOutsideOfWindows()
        {
            FakeNativeLibraryResolver fakeLibraryResolver = new FakeNativeLibraryResolver();
            fakeLibraryResolver.SetFakeMachinePlatformForTest(new OSAndArchitecture(PlatformOperatingSystem.Unix, PlatformArchitecture.Mips64));
            NativePlatformUtils.SetGlobalResolver(fakeLibraryResolver);
            try
            {
                ILogger logger = new ConsoleLogger();
                try
                {
                    IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
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
        public void TestWaveListCaptureDevices()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            var deviceList = driver.ListCaptureDevices().ToList();
            Assert.AreEqual(1, deviceList.Count);
            Assert.AreEqual(driver.CaptureDriverName, deviceList[0].DriverName);
            Assert.AreEqual("Default Audio In", deviceList[0].DeviceFriendlyName);
        }

        [TestMethod]
        public void TestWaveListRenderDevices()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            var deviceList = driver.ListRenderDevices().ToList();
            Assert.AreEqual(1, deviceList.Count);
            Assert.AreEqual(driver.RenderDriverName, deviceList[0].DriverName);
            Assert.AreEqual("Default Audio Out", deviceList[0].DeviceFriendlyName);
        }

        [TestMethod]
        public void TestWaveResolveDefaultCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().Single();
            IAudioCaptureDeviceId resolvedId = driver.ResolveCaptureDevice(id.Id);
            Assert.AreEqual(id, resolvedId);
        }

        [TestMethod]
        public void TestWaveResolveDefaultRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            IAudioRenderDeviceId id = driver.ListRenderDevices().Single();
            IAudioRenderDeviceId resolvedId = driver.ResolveRenderDevice(id.Id);
            Assert.AreEqual(id, resolvedId);
        }

        [TestMethod]
        public void TestWaveResolveUnknownCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
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
        public void TestWaveResolveUnknownRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
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
        public void TestWaveOpenUnknownCaptureDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            IAudioCaptureDeviceId id = new BogusDeviceId()
            {
                Id = "SpoonAudio:0",
                DeviceFriendlyName = "Literally a spoon",
                DriverName = "SpoonAudio"
            };

            try
            {
                using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
                {
                    driver.OpenCaptureDevice(id, new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null);
                    Assert.Fail("Expected an ArgumentException");
                }
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestWaveOpenUnknownRenderDevice()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            IAudioRenderDeviceId id = new BogusDeviceId()
            {
                Id = "SpoonAudio:0",
                DeviceFriendlyName = "Literally a spoon",
                DriverName = "SpoonAudio"
            };

            try
            {
                using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
                {
                    driver.OpenRenderDevice(id, new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null);
                    Assert.Fail("Expected an ArgumentException");
                }
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public async Task TestWaveOpenDefaultRenderDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            IAudioRenderDeviceId id = driver.ListRenderDevices().Single();
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
        public async Task TestWaveOpenNullRenderDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
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
        public void TestWaveOpenRenderDeviceInvalidArguments()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            IAudioRenderDeviceId id = driver.ListRenderDevices().Single();
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
        public async Task TestWaveOpenDefaultCaptureDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().Single();
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
        public async Task TestWaveOpenNullCaptureDevice()
        {
            ValidateTestSetupIsOk();
            TimeSpan MAX_TEST_TIME = TimeSpan.FromSeconds(5);
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
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
        public void TestWaveOpenCaptureDeviceInvalidArguments()
        {
            ValidateTestSetupIsOk();
            ILogger logger = new ConsoleLogger();
            IAudioDriver driver = new WaveDeviceDriver(logger.Clone("Driver"));
            IAudioCaptureDeviceId id = driver.ListCaptureDevices().Single();
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                try
                {
                    driver.OpenCaptureDevice(id, new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, MultiChannelMapping.Surround_7_1ch), null);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }

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
