
namespace Durandal.Tests.Common.Config
{
    using Durandal.Common.Collections;
    using Durandal.Common.Config;
    using Durandal.Common.Config.Accessors;
    using Durandal.Common.Events;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Test;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class ConfigurationTests
    {
        /// <summary>
        /// Ensure that variant configuration falls back to non-variant value when the variant does not exist
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationBasicValues()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            string testFile =
                "[Type|Int]\r\n" +
                "intkey=13\r\n" +
                "[Type|String]\r\n" +
                "stringkey=test\r\n" +
                "[Type|Bool]\r\n" +
                "boolkey=true\r\n" +
                "[Type|Float]\r\n" +
                "floatkey=2.5\r\n" +
                "[Type|StringList]\r\n" +
                "vectorkey=one,two,three\r\n";

            byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton);

            Assert.IsTrue(testConfig.ContainsKey("intkey"));
            Assert.IsTrue(testConfig.ContainsKey("stringkey"));
            Assert.IsTrue(testConfig.ContainsKey("boolkey"));
            Assert.IsTrue(testConfig.ContainsKey("floatkey"));
            Assert.IsTrue(testConfig.ContainsKey("vectorkey"));

            Assert.AreEqual(13, testConfig.GetInt32("intkey"));
            Assert.AreEqual("test", testConfig.GetString("stringkey"));
            Assert.AreEqual(true, testConfig.GetBool("boolkey"));
            Assert.AreEqual(2.5, testConfig.GetFloat32("floatkey"), 0.1);
            Assert.AreEqual(3, testConfig.GetStringList("vectorkey").Count);
        }

        [TestMethod]
        public async Task TestConfigurationTimeSpan()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            string testFile =
                "[Type|TimeSpan]\r\n" +
                "key1=0.1\r\n" +
                "[Type|TimeSpan]\r\n" +
                "key2&flavor:1=1:00\r\n" +
                "key2&flavor:2=2:00\r\n" +
                "key2=3:00\r\n" +
                "[Type|TimeSpan]\r\n" +
                "key3=2.16:53:21.543\r\n";

            byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton);

            Assert.IsTrue(testConfig.ContainsKey("key1"));
            Assert.IsTrue(testConfig.ContainsKey("key2"));
            Assert.IsTrue(testConfig.ContainsKey("key3"));

            Assert.AreEqual(TimeSpan.FromMilliseconds(100), testConfig.GetTimeSpan("key1"));
            Assert.AreEqual(TimeSpan.FromMinutes(1), testConfig.GetTimeSpan("key2", TimeSpan.Zero, new SmallDictionary<string, string>() { { "flavor", "1" } }));
            Assert.AreEqual(TimeSpan.FromMinutes(2), testConfig.GetTimeSpan("key2", TimeSpan.Zero, new SmallDictionary<string, string>() { { "flavor", "2" } }));
            Assert.AreEqual(TimeSpan.FromMinutes(3), testConfig.GetTimeSpan("key2"));
            Assert.AreEqual(new TimeSpan(2, 16, 53, 21, 543), testConfig.GetTimeSpan("key3"));
            Assert.AreEqual(TimeSpan.FromSeconds(10), testConfig.GetTimeSpan("notexist", TimeSpan.FromSeconds(10)));

            List<TimeSpan> testValues = new List<TimeSpan>()
            {
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromTicks(1000),
                TimeSpan.FromDays(300),
                new TimeSpan(5, 4, 52, 11, 360)
            };

            foreach (TimeSpan testValue in testValues)
            {
                testConfig.Set("dynamic", testValue);
                Assert.AreEqual(testValue, testConfig.GetTimeSpan("dynamic"));
            }
        }

        /// <summary>
        /// Ensure that we can parse dictionaries from a config
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationStringDictionaries()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            string testFile =
                "[Type|StringDictionary]\r\n" +
                "Dictionary1=change_state/device_type:device_type\r\n" +
                "[Type|StringDictionary]\r\n" +
                "Dictionary2=change_state/device_type:device_type,query_state/device_type:somethingelse";

            byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton);

            Assert.IsTrue(testConfig.ContainsKey("Dictionary1"));
            Assert.IsTrue(testConfig.ContainsKey("Dictionary2"));

            IDictionary<string, string> dict1 = testConfig.GetStringDictionary("Dictionary1");
            Assert.AreEqual(1, dict1.Count);
            Assert.IsTrue(dict1.ContainsKey("change_state/device_type"));
            Assert.AreEqual("device_type", dict1["change_state/device_type"]);
            IDictionary<string, string> dict2 = testConfig.GetStringDictionary("Dictionary2");
            Assert.AreEqual(2, dict2.Count);
            Assert.IsTrue(dict2.ContainsKey("change_state/device_type"));
            Assert.AreEqual("device_type", dict2["change_state/device_type"]);
            Assert.IsTrue(dict2.ContainsKey("query_state/device_type"));
            Assert.AreEqual("somethingelse", dict2["query_state/device_type"]);
        }

        [TestMethod]
        public async Task TestConfigurationStringLists()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            string testFile =
                "[Type|StringList]\r\n" +
                "vectorkey=one,two,three\r\n" +
                "[Type|StringList]\r\n" +
                "singlevector=one\r\n";

            byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton);

            Assert.IsTrue(testConfig.ContainsKey("vectorkey"));
            Assert.IsTrue(testConfig.ContainsKey("singlevector"));

            Assert.AreEqual(ConfigValueType.StringList, testConfig.GetRaw("vectorkey").ValueType);
            Assert.AreEqual(ConfigValueType.StringList, testConfig.GetRaw("singlevector").ValueType);

            Assert.AreEqual(3, testConfig.GetStringList("vectorkey").Count);
            Assert.AreEqual(1, testConfig.GetStringList("singlevector").Count);
        }

        /// <summary>
        /// Ensure that writing back to a stream will preserve the original file
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationWriteback()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            IList<string> testFile = new List<string>();
            testFile.Add("[Type|Int]");
            testFile.Add("intkey=13");
            testFile.Add("# Comment");
            testFile.Add("");
            testFile.Add("[Type|String]");
            testFile.Add("stringkey=test");

            byte[] testFileData = Encoding.UTF8.GetBytes(string.Join("\r\n", testFile));

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton));

            MemoryStream output = new MemoryStream();
            Assert.IsTrue(await testConfig.WriteToStream(output));

            string response = Encoding.UTF8.GetString(output.ToArray());
            Assert.AreEqual("[Type|Int]\r\nintkey=13\r\n# Comment\r\n\r\n[Type|String]\r\nstringkey=test\r\n", response);
        }

        /// <summary>
        /// Ensure that writing back to a stream will preserve the original file while also updating values that have changed
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationWritebackOfExistingValues()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            IList<string> testFile = new List<string>();
            testFile.Add("[Type|String]");
            testFile.Add("stringkey=test");
            testFile.Add("# Comment");
            testFile.Add("");
            testFile.Add("[Type|Int]");
            testFile.Add("intkey=13");

            byte[] testFileData = Encoding.UTF8.GetBytes(string.Join("\r\n", testFile));

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton));

            testConfig.Set("stringkey", "anewvalue");

            MemoryStream output = new MemoryStream();
            Assert.IsTrue(await testConfig.WriteToStream(output));

            string response = Encoding.UTF8.GetString(output.ToArray());
            Assert.AreEqual("[Type|String]\r\nstringkey=anewvalue\r\n# Comment\r\n\r\n[Type|Int]\r\nintkey=13\r\n", response);
        }

        /// <summary>
        /// Ensure that writing back to a stream will preserve the original file while also updating values that have changed, with variants
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationWritebackOfExistingValuesWithVariant()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            IList<string> testFile = new List<string>();
            testFile.Add("# Comment");
            testFile.Add("[Description|desc]");
            testFile.Add("[Type|String]");
            testFile.Add("stringkey=test");
            testFile.Add("# Comment");
            testFile.Add("");
            testFile.Add("[Type|Int]");
            testFile.Add("intkey=13");

            byte[] testFileData = Encoding.UTF8.GetBytes(string.Join("\r\n", testFile));

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton));

            testConfig.Set("stringkey", "anewvalue", new SmallDictionary<string, string>() { { "locale", "en-US" } });

            MemoryStream output = new MemoryStream();
            Assert.IsTrue(await testConfig.WriteToStream(output));

            string response = Encoding.UTF8.GetString(output.ToArray());
            Assert.AreEqual("# Comment\r\n[Description|desc]\r\n[Type|String]\r\nstringkey&locale:en-US=anewvalue\r\nstringkey=test\r\n# Comment\r\n\r\n[Type|Int]\r\nintkey=13\r\n", response);
        }

        /// <summary>
        /// Ensure that writing back to a stream will preserve the original file, while appending new values to the end
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationWritebackOfNewValues()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            IList<string> testFile = new List<string>();
            testFile.Add("[Type|Int]");
            testFile.Add("intkey=13");
            testFile.Add("# Comment");
            testFile.Add("");
            testFile.Add("[Type|String]");
            testFile.Add("stringkey=test");

            byte[] testFileData = Encoding.UTF8.GetBytes(string.Join("\r\n", testFile));

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton));

            testConfig.Set("newval", true);

            MemoryStream output = new MemoryStream();
            Assert.IsTrue(await testConfig.WriteToStream(output));

            string response = Encoding.UTF8.GetString(output.ToArray());
            Assert.AreEqual("[Type|Int]\r\nintkey=13\r\n# Comment\r\n\r\n[Type|String]\r\nstringkey=test\r\n[Type|Bool]\r\nnewval=true\r\n", response);
        }

        /// <summary>
        /// Ensure that variant configuration works in general
        /// </summary>
        [TestMethod]
        public void TestConfigurationVariantConfig()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            IConfiguration testConfig = new InMemoryConfiguration(logger);
            testConfig.Set("testkey", "none");
            testConfig.Set("testkey", "english", new SmallDictionary<string, string>() { { "locale", "en-US" } });
            testConfig.Set("testkey", "spanish", new SmallDictionary<string, string>() { { "locale", "es-es" } });

            Assert.AreEqual("none", testConfig.GetString("testkey"));
            Assert.AreEqual("english", testConfig.GetString("testkey", string.Empty, new SmallDictionary<string, string>() { { "locale", "en-US" } }));
            Assert.AreEqual("spanish", testConfig.GetString("testkey", string.Empty, new SmallDictionary<string, string>() { { "locale", "es-es" } }));
        }

        /// <summary>
        /// Ensure that variant configuration falls back to non-variant value when the variant does not exist
        /// </summary>
        [TestMethod]
        public void TestConfigurationVariantConfigFallback()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            IConfiguration testConfig = new InMemoryConfiguration(logger);
            Assert.AreEqual("accessordefault", testConfig.GetString("notexistkey", "accessordefault", new SmallDictionary<string, string>() { { "locale", "it-it" } }));
            testConfig.Set("testkey", "fallbackvalue");
            testConfig.Set("testkey", "english", new SmallDictionary<string, string>() { { "locale", "en-US" } });
            testConfig.Set("testkey", "spanish", new SmallDictionary<string, string>() { { "locale", "es-es" } });
            Assert.AreEqual("fallbackvalue", testConfig.GetString("testkey", "accessordefault", new SmallDictionary<string, string>() { { "locale", "it-it" } }));
        }

        /// <summary>
        /// Ensure that variant configuration preserves the annotations of its non-variant values.
        /// This test is not really relevant after the configuration v2.0 refactor, but I might as well keep it.
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationVariantParamsInheritAnnotations()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            string testFile = 
                "[Description|desc]\r\n" +
                "[Type|Int]\r\n" +
                "key&var:1=2\r\n" +
                "key=1\r\n";

            byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton);

            Assert.AreEqual(testConfig.GetAllValues().Count, 1);
            RawConfigValue rawVal = testConfig.GetRaw("key");
            Assert.IsNotNull(rawVal);
            Assert.AreEqual(1, rawVal.Annotations.Count);
        }

        /// <summary>
        /// Ensure that variant configuration enforces a strict ordering
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationVariantParamsOrderEnforced()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            string testFile =
                "[Type|Int]\r\n" +
                "key=1\r\n" +
                "key&var:1=2\r\n";

            byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

            try
            {
                IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
                await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton);
                Assert.Fail("Configuration.LoadStream should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        ///// <summary>
        ///// Ensure that basic persistent variants are honored
        ///// </summary>
        //[TestMethod]
        //public async Task TestConfigurationPersistentVariantsBasic()
        //{
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
        //    string testFile =
        //        "[locale:en-US]\r\n" +
        //        "[Type|Int]\r\n" +
        //        "key=2\r\n" +
        //        "[/]\r\n" +
        //        "[Type|Int]\r\n" +
        //        "key=1\r\n";
                
        //    byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

        //    IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
        //    await testConfig.LoadStream(new MemoryStream(testFileData));
        //    Assert.AreEqual(1, testConfig.GetInt32("key", 0));
        //    Assert.AreEqual(2, testConfig.GetInt32("key", 0, new SmallDictionary<string, string>() { { "locale", "en-US" } }));
        //}

        ///// <summary>
        ///// Ensure that basic persistent variants are honored across multiple keys
        ///// </summary>
        //[TestMethod]
        //public async Task TestConfigurationPersistentVariantsBasic2()
        //{
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
        //    string testFile =
        //        "[locale:en-US]\r\n" +
        //        "[Type|Int]\r\n" +
        //        "key1=1\r\n" +
        //        "[Type|Int]\r\n" +
        //        "key2=2\r\n" +
        //        "[/]\r\n";
                
        //    byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

        //    IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
        //    await testConfig.LoadStream(new MemoryStream(testFileData));
        //    Assert.IsFalse(testConfig.ContainsKey("key1"));
        //    Assert.AreEqual(1, testConfig.GetInt32("key1", 0, new SmallDictionary<string, string>() { { "locale", "en-US" } }));
        //    Assert.AreEqual(2, testConfig.GetInt32("key2", 0, new SmallDictionary<string, string>() { { "locale", "en-US" } }));
        //}

        ///// <summary>
        ///// Ensure that persistent variants can be overwritten
        ///// </summary>
        //[TestMethod]
        //public async Task TestConfigurationPersistentVariantsOverwriting()
        //{
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
        //    string testFile =
        //        "[locale:en-US]\r\n" +
        //        "[Type|Int]\r\n" +
        //        "key=1\r\n" +
        //        "[locale:zh-cn]\r\n" +
        //        "[Type|Int]\r\n" +
        //        "key=2\r\n" +
        //        "[user:me]\r\n" +
        //        "[Type|Int]\r\n" +
        //        "key=3\r\n";

        //    byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

        //    IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
        //    await testConfig.LoadStream(new MemoryStream(testFileData));
        //    Assert.IsFalse(testConfig.ContainsKey("key"));
        //    Assert.AreEqual(1, testConfig.GetInt32("key", 0, new SmallDictionary<string, string>() { { "locale", "en-US" } }));
        //    Assert.AreEqual(2, testConfig.GetInt32("key", 0, new SmallDictionary<string, string>() { { "locale", "zh-cn" } }));
        //    Assert.AreEqual(3, testConfig.GetInt32("key", 0, new SmallDictionary<string, string>() { { "user", "me" } }));
        //}

        [TestMethod]
        public async Task TestConfigurationSpecialCharsInAnnotations()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            string testFile =
                "[Description|This is a long description of this field. The default value is *:*. Here is a url http://www.google.com/yes?test=yes#fragment]\r\n" +
                "[Default|*;*]\r\n" +
                "[Type|String]\r\n" +
                "stringkey=test\r\n" +
                 "[Type|Int]\r\n" +
                "intkey=13\r\n";

            byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton);

            Assert.IsTrue(testConfig.ContainsKey("stringkey"));
            Assert.IsTrue(testConfig.ContainsKey("intkey"));

            Assert.AreEqual(13, testConfig.GetInt32("intkey"));
            Assert.AreEqual("test", testConfig.GetString("stringkey"));
            RawConfigValue value = testConfig.GetRaw("stringkey");
            Assert.AreEqual(2, value.Annotations.Count);
            foreach (var annotation in value.Annotations)
            {
                if (annotation.GetTypeName() == "Description")
                {
                    Assert.AreEqual("This is a long description of this field. The default value is *:*. Here is a url http://www.google.com/yes?test=yes#fragment", annotation.GetStringValue());
                }
                else if (annotation.GetTypeName() == "Default")
                {
                    Assert.AreEqual("*;*", annotation.GetStringValue());
                }
                else
                {
                    Assert.Fail("Unexpected annotation found");
                }
            }
        }

        /// <summary>
        /// Tests that an empty string is a valid config value
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationEmptyStringConfigValue()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            string testFile =
                "[Type|String]\r\n" +
                "stringkey=\r\n" +
                 "[Type|Int]\r\n" +
                "intkey=13\r\n";

            byte[] testFileData = Encoding.UTF8.GetBytes(testFile);

            IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);
            await testConfig.LoadStream(new MemoryStream(testFileData), DefaultRealTimeProvider.Singleton);

            Assert.IsTrue(testConfig.ContainsKey("stringkey"));
            Assert.IsTrue(testConfig.ContainsKey("intkey"));

            Assert.AreEqual(13, testConfig.GetInt32("intkey"));
            Assert.AreEqual(string.Empty, testConfig.GetString("stringkey"));
        }

        /// <summary>
        /// Tests that we can thrash configuration values on an ini file all day and won't cause a problem
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationThreadSafetyIniFile()
        {
            const int threads = 20;
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ConfigWriters", threads, false))
            {
                InMemoryFileSystem fileSystem = new InMemoryFileSystem();
                IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, new VirtualPath("test_config.ini"), fileSystem, DefaultRealTimeProvider.Singleton);

                CancellationTokenSource cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                threadPool.EnqueueUserWorkItem(() =>
                {
                    FastRandom rand = new FastRandom();
                    byte[] rawBinary = new byte[100];
                    rand.NextBytes(rawBinary);
                    while (!cancelToken.IsCancellationRequested)
                    {
                        testConfig.Set("testbinary", rawBinary);
                        testConfig.Set("teststring", "duuude");
                        testConfig.Set("testfloat", rand.NextFloat());
                    }
                });
                for (int c = 0; c < threads - 1; c++)
                {
                    threadPool.EnqueueUserWorkItem(() =>
                    {
                        while (!cancelToken.IsCancellationRequested)
                        {
                            testConfig.GetBinary("testbinary");
                            testConfig.GetString("teststring");
                            testConfig.GetFloat32("testfloat");
                        }
                    });
                }

                await threadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        /// <summary>
        /// Tests that we can write values to a configuration backed by a file and then immediately dispose of it, and all pending changes are reflected in the file properly.
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationFlushesValuesBeforeDisposal()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            for (int loop = 0; loop < 20; loop++)
            {
                VirtualPath fileName = new VirtualPath("test_config_" + loop + ".ini");
                using (IniFileConfiguration writeConfig = await IniFileConfiguration.Create(logger, fileName, fileSystem, DefaultRealTimeProvider.Singleton))
                {
                    for (int c = 0; c < 20; c++)
                    {
                        writeConfig.Set("IntKey" + c, c);
                        writeConfig.Set("StringKey" + c, c.ToString());
                    }
                }

                using (IniFileConfiguration readConfig = await IniFileConfiguration.Create(logger, fileName, fileSystem, DefaultRealTimeProvider.Singleton))
                {
                    for (int c = 0; c < 20; c++)
                    {
                        Assert.IsTrue(readConfig.ContainsKey("IntKey" + c));
                        Assert.AreEqual(c, readConfig.GetInt32("IntKey" + c));
                        Assert.IsTrue(readConfig.ContainsKey("StringKey" + c));
                        Assert.AreEqual(c.ToString(), readConfig.GetString("StringKey" + c));
                    }
                }
            }
        }

        /// <summary>
        /// Tests that we can thrash configuration values in memory all day and won't cause a problem
        /// </summary>
        [TestMethod]
        public async Task TestConfigurationThreadSafetyInMemory()
        {
            const int threads = 20;
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ConfigWriters", threads, false))
            {
                IniFileConfiguration testConfig = await IniFileConfiguration.Create(logger, null, null, DefaultRealTimeProvider.Singleton);

                CancellationTokenSource cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                threadPool.EnqueueUserWorkItem(() =>
                {
                    FastRandom rand = new FastRandom();
                    byte[] rawBinary = new byte[100];
                    rand.NextBytes(rawBinary);
                    while (!cancelToken.IsCancellationRequested)
                    {
                        testConfig.Set("testbinary", rawBinary);
                        testConfig.Set("teststring", "duuude");
                        testConfig.Set("testfloat", rand.NextFloat());
                    }
                });
                for (int c = 0; c < threads - 1; c++)
                {
                    threadPool.EnqueueUserWorkItem(() =>
                    {
                        while (!cancelToken.IsCancellationRequested)
                        {
                            testConfig.GetBinary("testbinary");
                            testConfig.GetString("teststring");
                            testConfig.GetFloat32("testfloat");
                        }
                    });
                }

                while (!cancelToken.IsCancellationRequested)
                {
                    using (MemoryStream stream1 = new MemoryStream())
                    {
                        await testConfig.WriteToStream(stream1);

                        using (MemoryStream stream2 = new MemoryStream(stream1.ToArray(), false))
                        {
                            await testConfig.LoadStream(stream2, DefaultRealTimeProvider.Singleton);
                        }
                    }

                    await Task.Delay(10);
                }
            }
        }

        [TestMethod]
        public async Task TestConfigurationInt32Accessor()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryConfiguration configuration = new InMemoryConfiguration(logger.Clone("Config"));
            EventRecorder<ConfigValueChangedEventArgs<int>> eventRecorder = new EventRecorder<ConfigValueChangedEventArgs<int>>();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            using (IConfigValue<int> accessor = configuration.CreateInt32Accessor(logger.Clone("Accessor"), "key", 10, new SmallDictionary<string, string>() { { "locale", "en-US" } }))
            {
                accessor.ChangedEvent.Subscribe(eventRecorder.HandleEventAsync);

                // Because the _accessor's_ default value is 10 and no value is specified in the config, we expect to see 10 if we fetch the value
                Assert.AreEqual(10, accessor.Value);

                // Set the _configuration's_ default value to 0. This should now override the accessor's default.
                configuration.Set("key", 0, null, lockStepTime);
                lockStepTime.Step(TimeSpan.FromMilliseconds(1));
                RetrieveResult<CapturedEvent<ConfigValueChangedEventArgs<int>>> rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(rr.Success);
                Assert.IsNotNull(rr.Result.Args);
                Assert.AreEqual("key", rr.Result.Args.Key);
                Assert.AreEqual(0, rr.Result.Args.NewValue);
                Assert.AreEqual(0, accessor.Value);

                // If we change the variant value then it should fire an event with an updated value
                configuration.Set("key", 1033, new SmallDictionary<string, string>() { { "locale", "en-US" } }, lockStepTime);
                lockStepTime.Step(TimeSpan.FromMilliseconds(1));
                rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(rr.Success);
                Assert.IsNotNull(rr.Result.Args);
                Assert.AreEqual("key", rr.Result.Args.Key);
                Assert.AreEqual(1033, rr.Result.Args.NewValue);
                Assert.AreEqual(1033, accessor.Value);

                // If we change some unrelated variant, we still get a change event, but the value is still the same as before
                configuration.Set("key", 2462, new SmallDictionary<string, string>() { { "locale", "es-mx" } }, lockStepTime);
                lockStepTime.Step(TimeSpan.FromMilliseconds(1));
                rr = await eventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(rr.Success);
                Assert.IsNotNull(rr.Result.Args);
                Assert.AreEqual("key", rr.Result.Args.Key);
                Assert.AreEqual(1033, rr.Result.Args.NewValue);
                Assert.AreEqual(1033, accessor.Value);
            }
        }
    }
}
