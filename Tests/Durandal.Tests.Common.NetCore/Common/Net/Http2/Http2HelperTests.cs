using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.Net.Http2.HPack;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Net.Http2
{
    [TestClass]
    public class Http2HelperTests
    {
        [TestMethod]
        public void TestHttp2HelpersDeserializeSettings()
        {
            Http2Settings expectedSettings = new Http2Settings(
                enablePush: true,
                headerTableSize: 4096,
                initialWindowSize: 65535,
                maxConcurrentStreams: 2147483647,
                maxFrameSize: 16384,
                maxHeaderListSize: 8192,
                enableConnectProtocol: false);

            Http2Settings parsedSettings;
            Assert.IsTrue(Http2Helpers.TryParseSettingsFromBase64("AAEAABAAAAIAAAABAAN_____AAQAAP__AAUAAEAAAAYAACAA", isServer: true, settings: out parsedSettings));
            Assert.AreEqual(expectedSettings, parsedSettings);
        }

        [TestMethod]
        public void TestHttp2HelpersSerializeDefaultSettings()
        {
            Http2Settings defaultSettings = Http2Settings.ServerDefault();
            Http2Settings parsedSettings;
            string base64 = Http2Helpers.SerializeSettingsToBase64(defaultSettings);
            Assert.AreEqual("AAEAABAAAAIAAAAAAAN_____AAQAAP__AAUAAEAAAAZ_____AAgAAAAA", base64);
            Assert.IsTrue(Http2Helpers.TryParseSettingsFromBase64(base64, isServer: false, settings: out parsedSettings));
            Assert.IsNotNull(parsedSettings);
            Assert.AreEqual(defaultSettings, parsedSettings);

            defaultSettings = Http2Settings.Default();
            base64 = Http2Helpers.SerializeSettingsToBase64(defaultSettings);
            Assert.AreEqual("AAEAABAAAAIAAAABAAN_____AAQAAP__AAUAAEAAAAZ_____AAgAAAAA", base64);
            Assert.IsTrue(Http2Helpers.TryParseSettingsFromBase64(base64, isServer: true, settings: out parsedSettings));
            Assert.IsNotNull(parsedSettings);
            Assert.AreEqual(defaultSettings, parsedSettings);
        }

        [TestMethod]
        public void TestHttp2HelpersSerializeSettings()
        {
            IRandom rand = new FastRandom(542101);
            for (int c = 0; c < 100; c++)
            {
                Http2Settings expectedSettings = new Http2Settings(
                    enablePush: rand.NextInt(0, 2) == 0,
                    headerTableSize: rand.NextInt(1024, 4096),
                    initialWindowSize: rand.NextInt(65535, 200000),
                    maxConcurrentStreams: rand.NextInt(0, 100000),
                    maxFrameSize: rand.NextInt(16384, 16000000),
                    maxHeaderListSize: rand.NextInt(10000, 100000),
                    enableConnectProtocol: rand.NextInt(0, 2) == 0);

                string base64 = Http2Helpers.SerializeSettingsToBase64(expectedSettings);
                Http2Settings parsedSettings;
                Assert.IsTrue(Http2Helpers.TryParseSettingsFromBase64(base64, rand.NextInt(0, 100) < 50, out parsedSettings));
                Assert.IsNotNull(parsedSettings);
                Assert.AreEqual(expectedSettings, parsedSettings);
            }
        }
    }
}
