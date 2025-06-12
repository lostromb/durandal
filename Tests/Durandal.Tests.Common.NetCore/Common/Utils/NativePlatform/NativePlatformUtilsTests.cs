using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Utils.NativePlatform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Utils.NativePlatform
{
    [TestClass]
    public class NativePlatformUtilsTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            NativePlatformUtils.SetGlobalResolver(null);
        }

        [TestMethod]
        public void TestParseCommonRIDs()
        {
            List<Tuple<string, OSAndArchitecture>> testCases = new List<Tuple<string, OSAndArchitecture>>();
            testCases.Add(new Tuple<string, OSAndArchitecture>("win-x64", new OSAndArchitecture(PlatformOperatingSystem.Windows, PlatformArchitecture.X64)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("win-x86", new OSAndArchitecture(PlatformOperatingSystem.Windows, PlatformArchitecture.I386)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("win-arm64", new OSAndArchitecture(PlatformOperatingSystem.Windows, PlatformArchitecture.Arm64)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("linux-x64", new OSAndArchitecture(PlatformOperatingSystem.Linux, PlatformArchitecture.X64)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("linux-x86", new OSAndArchitecture(PlatformOperatingSystem.Linux, PlatformArchitecture.I386)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("linux-arm", new OSAndArchitecture(PlatformOperatingSystem.Linux, PlatformArchitecture.ArmV7)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("android-arm64", new OSAndArchitecture(PlatformOperatingSystem.Android, PlatformArchitecture.Arm64)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("ios-arm64", new OSAndArchitecture(PlatformOperatingSystem.iOS, PlatformArchitecture.Arm64)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("osx-x64", new OSAndArchitecture(PlatformOperatingSystem.MacOS, PlatformArchitecture.X64)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("win10-x64", new OSAndArchitecture(PlatformOperatingSystem.Windows, PlatformArchitecture.X64)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("osx", new OSAndArchitecture(PlatformOperatingSystem.MacOS, PlatformArchitecture.Unknown)));
            testCases.Add(new Tuple<string, OSAndArchitecture>("win11", new OSAndArchitecture(PlatformOperatingSystem.Windows, PlatformArchitecture.Unknown)));

            foreach (var testCase in testCases)
            {
                Assert.AreEqual(testCase.Item2, NativePlatformUtils.ParseRuntimeId(testCase.Item1.AsSpan()));
            }
        }

        [TestMethod]
        public void TestGetMachinePerfClass()
        {
            PerformanceClass perf = NativePlatformUtils.GetMachinePerformanceClass();
            Assert.AreNotEqual(PerformanceClass.Unknown, perf);
            Stopwatch timer = Stopwatch.StartNew();
            perf = NativePlatformUtils.GetMachinePerformanceClass();
            timer.Stop();
            Assert.IsTrue(timer.ElapsedMillisecondsPrecise() < 5, "Getting cached performance class should be a trivial operation");
        }

        [TestMethod]
        public void TestGetCurrentPlatform_BasicResolver()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);

            PlatformArchitecture expectedArchitecture = PlatformArchitecture.Unknown;
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                expectedArchitecture = PlatformArchitecture.X64;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                expectedArchitecture = PlatformArchitecture.I386;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                expectedArchitecture = PlatformArchitecture.Arm64;
            }
            else
            {
                Assert.Inconclusive("This test only runs on x86, x64, or arm64 platforms");
            }

            OSAndArchitecture actualPlatform = NativePlatformUtils.GetCurrentPlatform(logger);
            Assert.AreEqual(expectedArchitecture, actualPlatform.Architecture);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.AreEqual(PlatformOperatingSystem.Windows, actualPlatform.OS);
            }
        }

        [TestMethod]
        public void TestGetCurrentPlatform_FancyResolver()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            PlatformArchitecture expectedArchitecture = PlatformArchitecture.Unknown;
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                expectedArchitecture = PlatformArchitecture.X64;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                expectedArchitecture = PlatformArchitecture.I386;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                expectedArchitecture = PlatformArchitecture.Arm64;
            }
            else
            {
                Assert.Inconclusive("This test only runs on x86, x64, or arm64 platforms");
            }

            OSAndArchitecture actualPlatform = NativePlatformUtils.GetCurrentPlatform(logger);
            Assert.AreEqual(expectedArchitecture, actualPlatform.Architecture);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.AreEqual(PlatformOperatingSystem.Windows, actualPlatform.OS);
            }
        }

        [TestMethod]
        public void TestGetNativePlatformArchitecture()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            PlatformArchitecture arch = PlatformArchitecture.Unknown;

            PlatformArchitecture expectedArchitecture = PlatformArchitecture.Unknown;
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                expectedArchitecture = PlatformArchitecture.X64;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                expectedArchitecture = PlatformArchitecture.I386;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                expectedArchitecture = PlatformArchitecture.Arm64;
            }
            else
            {
                Assert.Inconclusive("This test only runs on x86, x64, or arm64 platforms");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arch = NativePlatformUtils.TryGetNativeArchitecture(PlatformOperatingSystem.Windows, logger);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                arch = NativePlatformUtils.TryGetNativeArchitecture(PlatformOperatingSystem.Linux, logger);
            }
            else
            {
                Assert.Inconclusive("This test only runs on Windows or Linux platforms");
            }

            Assert.AreEqual(expectedArchitecture, arch);
        }
    }
}
