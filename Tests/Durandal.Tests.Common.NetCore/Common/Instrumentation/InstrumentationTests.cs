
namespace Durandal.Tests.Common.Instrumentation
{
    using Durandal.API;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Instrumentation.Profiling;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Security;
    using Durandal.Common.Statistics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Extensions.BondProtocol;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class InstrumentationTests
    {
        [TestMethod]
        public void TestInstrumentationSerializeEventsListWithBond()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            DateTimeOffset baseTime = new DateTimeOffset(1999, 10, 10, 10, 10, 10, TimeSpan.Zero);
            InstrumentationBlob input = new InstrumentationBlob();
            Guid traceId = Guid.NewGuid();
            LogEvent one = new LogEvent("ONE", "Message", LogLevel.Ins, baseTime, traceId);
            LogEvent two = new LogEvent("TWO", "Message", LogLevel.Ins, baseTime, traceId);
            input.AddEvent(one);
            input.AddEvent(two);
            IByteConverter<InstrumentationEventList> instrumentationSerializer = new BondByteConverterInstrumentationEventList();
            byte[] blob = input.Compress(instrumentationSerializer);
            InstrumentationBlob output = InstrumentationBlob.Decompress(blob, instrumentationSerializer);
            List<LogEvent> outEvents = output.GetEvents();
            Assert.AreEqual(one, outEvents[0]);
            Assert.AreEqual(two, outEvents[1]);
        }

        [TestMethod]
        public void TestInstrumentationSerializeEventsListWithBinary()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            DateTimeOffset baseTime = new DateTimeOffset(1999, 10, 10, 10, 10, 10, TimeSpan.Zero);
            InstrumentationBlob input = new InstrumentationBlob();
            Guid traceId = Guid.NewGuid();
            LogEvent one = new LogEvent("ONE", "Message", LogLevel.Ins, baseTime, traceId);
            LogEvent two = new LogEvent("TWO", "Message", LogLevel.Ins, baseTime, traceId);
            input.AddEvent(one);
            input.AddEvent(two);
            IByteConverter<InstrumentationEventList> instrumentationSerializer = new InstrumentationBlobSerializer();
            byte[] blob = input.Compress(instrumentationSerializer);
            InstrumentationBlob output = InstrumentationBlob.Decompress(blob, instrumentationSerializer);
            List<LogEvent> outEvents = output.GetEvents();
            Assert.AreEqual(one, outEvents[0]);
            Assert.AreEqual(two, outEvents[1]);
        }

        [TestMethod]
        public void TestInstrumentationMergePartialImpressionsWithArrays()
        {
            IList<string> events = new List<string>();
            Guid traceId = Guid.NewGuid();
            events.Add(CommonInstrumentation.GenerateObjectEntry("Test.Array", new[] { "A" }));
            events.Add(CommonInstrumentation.GenerateObjectEntry("Test.Array", new[] { "B" }));
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            JObject obj = CommonInstrumentation.MergeImpressions(events, logger);
            Assert.IsNotNull(obj);
            Assert.AreEqual("{\r\n  \"Test\": {\r\n    \"Array\": [\r\n      \"A\",\r\n      \"B\"\r\n    ]\r\n  }\r\n}", obj.ToString());
        }

        [TestMethod]
        public void TestInstrumentationGenerateLatencyEntries()
        {
            string responseString = CommonInstrumentation.GenerateLatencyEntry("Operation1", 100);
            Console.WriteLine(responseString);
            Assert.IsTrue(responseString.StartsWith("{\"Perf\":{\"Latency\":{\"Operation1\":{\"Values\":[{\"Value\":100.00,\"StartTime\":"));
            Assert.IsTrue(responseString.EndsWith("}]}}}}"));

            responseString = CommonInstrumentation.GenerateLatencyEntry("Operation1.Sub1", 1000);
            Console.WriteLine(responseString);
            Assert.IsTrue(responseString.StartsWith("{\"Perf\":{\"Latency\":{\"Operation1.Sub1\":{\"Values\":[{\"Value\":1000.00,\"StartTime\":"));
            Assert.IsTrue(responseString.EndsWith("}]}}}}"));

            responseString = CommonInstrumentation.GenerateLatencyEntry("Operation1", 4.5);
            Console.WriteLine(responseString);
            Assert.IsTrue(responseString.StartsWith("{\"Perf\":{\"Latency\":{\"Operation1\":{\"Values\":[{\"Value\":4.50,\"StartTime\":"));
            Assert.IsTrue(responseString.EndsWith("}]}}}}"));
        }

        [TestMethod]
        public void TestInstrumentationGenerateSizeEntries()
        {
            Assert.AreEqual("{\"Perf\":{\"Size\":{\"Operation1\":{\"Values\":[{\"Value\":100}]}}}}", CommonInstrumentation.GenerateSizeEntry("Operation1", 100));
            Assert.AreEqual("{\"Perf\":{\"Size\":{\"Operation1.Sub1\":{\"Values\":[{\"Value\":1000}]}}}}", CommonInstrumentation.GenerateSizeEntry("Operation1.Sub1", 1000));
            Assert.AreEqual("{\"Perf\":{\"Size\":{\"Operation1\":{\"Values\":[{\"Value\":0}]}}}}", CommonInstrumentation.GenerateSizeEntry("Operation1", 0));
        }

        [TestMethod]
        public void TestInstrumentationGenerateObjectEntries()
        {
            Hypothesis<string> testValue = new Hypothesis<string>("Val", 1);
            for (int c = 0; c < 100; c++)
            {
                Assert.AreEqual("{\"Root\":{\"Value\":\"Val\",\"Conf\":1.0}}", CommonInstrumentation.GenerateObjectEntry("Root", testValue));
                Assert.AreEqual("{\"Root\":{\"Path\":{\"Value\":\"Val\",\"Conf\":1.0}}}", CommonInstrumentation.GenerateObjectEntry("Root.Path", testValue));
                Assert.AreEqual("{\"Root\":{\"Path\":{\"Two\":{\"Value\":\"Val\",\"Conf\":1.0}}}}", CommonInstrumentation.GenerateObjectEntry("Root.Path.Two", testValue));
                Assert.AreEqual("{\"Root\":{\"Path\":{\"Two\":{\"Three\":{\"Value\":\"Val\",\"Conf\":1.0}}}}}", CommonInstrumentation.GenerateObjectEntry("Root.Path.Two.Three", testValue));
                Assert.AreEqual("{\"Value\":\"Val\",\"Conf\":1.0}", CommonInstrumentation.GenerateObjectEntry(string.Empty, testValue));
                Assert.AreEqual("{\"Value\":\"Val\",\"Conf\":1.0}", CommonInstrumentation.GenerateObjectEntry(null, testValue));
            }

            //System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            //for (int c = 0; c < 200000; c++)
            //{
            //    Assert.AreEqual("{\"Root\":{\"Value\":\"Val\",\"Conf\":1.0}}", CommonInstrumentation.GenerateObjectEntry("Root", testValue));
            //    Assert.AreEqual("{\"Root\":{\"Path\":{\"Value\":\"Val\",\"Conf\":1.0}}}", CommonInstrumentation.GenerateObjectEntry("Root.Path", testValue));
            //    Assert.AreEqual("{\"Root\":{\"Path\":{\"Two\":{\"Value\":\"Val\",\"Conf\":1.0}}}}", CommonInstrumentation.GenerateObjectEntry("Root.Path.Two", testValue));
            //    Assert.AreEqual("{\"Root\":{\"Path\":{\"Two\":{\"Three\":{\"Value\":\"Val\",\"Conf\":1.0}}}}}", CommonInstrumentation.GenerateObjectEntry("Root.Path.Two.Three", testValue));
            //    //Assert.AreEqual("{\"Value\":\"Val\",\"Conf\":1.0}", CommonInstrumentation.GenerateObjectEntry(string.Empty, testValue));
            //    //Assert.AreEqual("{\"Value\":\"Val\",\"Conf\":1.0}", CommonInstrumentation.GenerateObjectEntry(null, testValue));
            //}
            //timer.Stop();
            //Console.WriteLine(timer.ElapsedMilliseconds);
        }

        [TestMethod]
        public void TestUnifiedTraceMergeLegacyLatencyValues()
        {
            Guid traceId = Guid.NewGuid();
            List<LogEvent> logs = new List<LogEvent>();
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Latency\":{\"Operation1\":1.54}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Latency\":{\"Operation2\":3}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Latency\":{\"Operation3\":1000.5223}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            UnifiedTrace trace = UnifiedTrace.CreateFromLogData(traceId, logs, new ConsoleLogger(), new NullStringEncrypter());
            Assert.IsNotNull(trace);
            Assert.IsTrue(trace.Latencies.ContainsKey("Operation1"));
            Assert.AreEqual(1, trace.Latencies["Operation1"].Values.Count);
            Assert.AreEqual(1.54f, trace.Latencies["Operation1"].Values[0].Value, 0.01f);
            Assert.IsTrue(trace.Latencies.ContainsKey("Operation2"));
            Assert.AreEqual(1, trace.Latencies["Operation2"].Values.Count);
            Assert.AreEqual(3f, trace.Latencies["Operation2"].Values[0].Value, 0.01f);
            Assert.IsTrue(trace.Latencies.ContainsKey("Operation3"));
            Assert.AreEqual(1, trace.Latencies["Operation3"].Values.Count);
            Assert.AreEqual(1000.5223f, trace.Latencies["Operation3"].Values[0].Value, 0.01f);
        }

        [TestMethod]
        public void TestUnifiedTraceMergeLegacyHeterogeneousLatencyValues()
        {
            Guid traceId = Guid.NewGuid();
            List<LogEvent> logs = new List<LogEvent>();
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Latency\":{\"Operation1\":1.54}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Latency\":{\"Operation2\":3}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", CommonInstrumentation.GenerateInstancedLatencyEntry("Operation3", "InstanceId", 453.123), LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            UnifiedTrace trace = UnifiedTrace.CreateFromLogData(traceId, logs, new ConsoleLogger(), new NullStringEncrypter());
            Assert.IsNotNull(trace);
            Assert.IsTrue(trace.Latencies.ContainsKey("Operation1"));
            Assert.AreEqual(1, trace.Latencies["Operation1"].Values.Count);
            Assert.AreEqual(1.54f, trace.Latencies["Operation1"].Values[0].Value, 0.01f);
            Assert.AreEqual(1.54f, trace.Latencies["Operation1"].Sum.Value, 0.01f);
            Assert.AreEqual(1.54f, trace.Latencies["Operation1"].Average.Value, 0.01f);
            Assert.IsNull(trace.Latencies["Operation1"].Values[0].Id);
            Assert.IsTrue(trace.Latencies.ContainsKey("Operation2"));
            Assert.AreEqual(1, trace.Latencies["Operation2"].Values.Count);
            Assert.AreEqual(3f, trace.Latencies["Operation2"].Values[0].Value, 0.01f);
            Assert.AreEqual(3f, trace.Latencies["Operation2"].Sum.Value, 0.01f);
            Assert.AreEqual(3f, trace.Latencies["Operation2"].Average.Value, 0.01f);
            Assert.IsNull(trace.Latencies["Operation2"].Values[0].Id);
            Assert.IsTrue(trace.Latencies.ContainsKey("Operation3"));
            Assert.AreEqual(1, trace.Latencies["Operation3"].Values.Count);
            Assert.AreEqual(453.123f, trace.Latencies["Operation3"].Values[0].Value, 0.01f);
            Assert.AreEqual(453.123f, trace.Latencies["Operation3"].Sum.Value, 0.01f);
            Assert.AreEqual(453.123f, trace.Latencies["Operation3"].Average.Value, 0.01f);
            Assert.AreEqual("InstanceId", trace.Latencies["Operation3"].Values[0].Id);
        }

        [TestMethod]
        public void TestUnifiedTraceMergeListOfLatencyValues()
        {
            Guid traceId = Guid.NewGuid();
            List<LogEvent> logs = new List<LogEvent>();
            logs.Add(new LogEvent("TestComponent", CommonInstrumentation.GenerateInstancedLatencyEntry("Operation1", "Instance1", 100), LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", CommonInstrumentation.GenerateInstancedLatencyEntry("Operation1", "Instance2", 200), LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", CommonInstrumentation.GenerateLatencyEntry("Operation1", 300), LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            UnifiedTrace trace = UnifiedTrace.CreateFromLogData(traceId, logs, new ConsoleLogger(), new NullStringEncrypter());
            Assert.IsNotNull(trace);
            Assert.IsTrue(trace.Latencies.ContainsKey("Operation1"));
            Assert.AreEqual(3, trace.Latencies["Operation1"].Values.Count);
            Assert.AreEqual(100f, trace.Latencies["Operation1"].Values[0].Value, 0.01f);
            Assert.AreEqual("Instance1", trace.Latencies["Operation1"].Values[0].Id);
            Assert.AreEqual(200f, trace.Latencies["Operation1"].Values[1].Value, 0.01f);
            Assert.AreEqual("Instance2", trace.Latencies["Operation1"].Values[1].Id);
            Assert.AreEqual(300f, trace.Latencies["Operation1"].Values[2].Value, 0.01f);
            Assert.IsNull(trace.Latencies["Operation1"].Values[2].Id);
            Assert.AreEqual(600f, trace.Latencies["Operation1"].Sum.Value, 0.01f);
            Assert.AreEqual(200f, trace.Latencies["Operation1"].Average.Value, 0.01f);
        }

        [TestMethod]
        public void TestUnifiedTraceMergeLegacySizeValues()
        {
            Guid traceId = Guid.NewGuid();
            List<LogEvent> logs = new List<LogEvent>();
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Size\":{\"Operation1\":1}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Size\":{\"Operation2\":3}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Size\":{\"Operation3\":1000}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            UnifiedTrace trace = UnifiedTrace.CreateFromLogData(traceId, logs, new ConsoleLogger(), new NullStringEncrypter());
            Assert.IsNotNull(trace);
            Assert.IsTrue(trace.Sizes.ContainsKey("Operation1"));
            Assert.AreEqual(1, trace.Sizes["Operation1"].Values.Count);
            Assert.AreEqual(1, trace.Sizes["Operation1"].Values[0].Value);
            Assert.IsTrue(trace.Sizes.ContainsKey("Operation2"));
            Assert.AreEqual(1, trace.Sizes["Operation2"].Values.Count);
            Assert.AreEqual(3, trace.Sizes["Operation2"].Values[0].Value);
            Assert.IsTrue(trace.Sizes.ContainsKey("Operation3"));
            Assert.AreEqual(1, trace.Sizes["Operation3"].Values.Count);
            Assert.AreEqual(1000, trace.Sizes["Operation3"].Values[0].Value);
        }

        [TestMethod]
        public void TestUnifiedTraceMergeLegacyHeterogeneousSizeValues()
        {
            Guid traceId = Guid.NewGuid();
            List<LogEvent> logs = new List<LogEvent>();
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Size\":{\"Operation1\":1}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", "{\"Perf\":{\"Size\":{\"Operation2\":3}}}", LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", CommonInstrumentation.GenerateInstancedSizeEntry("Operation3", "InstanceId", 453), LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            UnifiedTrace trace = UnifiedTrace.CreateFromLogData(traceId, logs, new ConsoleLogger(), new NullStringEncrypter());
            Assert.IsNotNull(trace);
            Assert.IsTrue(trace.Sizes.ContainsKey("Operation1"));
            Assert.AreEqual(1, trace.Sizes["Operation1"].Values.Count);
            Assert.AreEqual(1, trace.Sizes["Operation1"].Values[0].Value);
            Assert.AreEqual(1, trace.Sizes["Operation1"].Sum.Value);
            Assert.AreEqual(1, trace.Sizes["Operation1"].Average.Value);
            Assert.IsNull(trace.Sizes["Operation1"].Values[0].Id);
            Assert.IsTrue(trace.Sizes.ContainsKey("Operation2"));
            Assert.AreEqual(1, trace.Sizes["Operation2"].Values.Count);
            Assert.AreEqual(3, trace.Sizes["Operation2"].Values[0].Value);
            Assert.AreEqual(3, trace.Sizes["Operation2"].Sum.Value);
            Assert.AreEqual(3, trace.Sizes["Operation2"].Average.Value);
            Assert.IsNull(trace.Sizes["Operation2"].Values[0].Id);
            Assert.IsTrue(trace.Sizes.ContainsKey("Operation3"));
            Assert.AreEqual(1, trace.Sizes["Operation3"].Values.Count);
            Assert.AreEqual(453, trace.Sizes["Operation3"].Values[0].Value);
            Assert.AreEqual(453, trace.Sizes["Operation3"].Sum.Value);
            Assert.AreEqual(453, trace.Sizes["Operation3"].Average.Value);
            Assert.AreEqual("InstanceId", trace.Sizes["Operation3"].Values[0].Id);
        }

        [TestMethod]
        public void TestUnifiedTraceMergeListOfSizeValues()
        {
            Guid traceId = Guid.NewGuid();
            List<LogEvent> logs = new List<LogEvent>();
            logs.Add(new LogEvent("TestComponent", CommonInstrumentation.GenerateInstancedSizeEntry("Operation1", "Instance1", 100), LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", CommonInstrumentation.GenerateInstancedSizeEntry("Operation1", "Instance2", 200), LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            logs.Add(new LogEvent("TestComponent", CommonInstrumentation.GenerateSizeEntry("Operation1", 300), LogLevel.Ins, DateTimeOffset.UtcNow, traceId));
            UnifiedTrace trace = UnifiedTrace.CreateFromLogData(traceId, logs, new ConsoleLogger(), new NullStringEncrypter());
            Assert.IsNotNull(trace);
            Assert.IsTrue(trace.Sizes.ContainsKey("Operation1"));
            Assert.AreEqual(3, trace.Sizes["Operation1"].Values.Count);
            Assert.AreEqual(100, trace.Sizes["Operation1"].Values[0].Value);
            Assert.AreEqual("Instance1", trace.Sizes["Operation1"].Values[0].Id);
            Assert.AreEqual(200, trace.Sizes["Operation1"].Values[1].Value);
            Assert.AreEqual("Instance2", trace.Sizes["Operation1"].Values[1].Id);
            Assert.AreEqual(300, trace.Sizes["Operation1"].Values[2].Value);
            Assert.IsNull(trace.Sizes["Operation1"].Values[2].Id);
            Assert.AreEqual(600, trace.Sizes["Operation1"].Sum.Value);
            Assert.AreEqual(200, trace.Sizes["Operation1"].Average.Value);
        }

        [TestMethod]
        public void TestMetricCollectorBasic()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testAbort = new CancellationTokenSource();
            testAbort.CancelAfter(TimeSpan.FromSeconds(30));
            LockStepRealTimeProvider lockstepTime = new LockStepRealTimeProvider(logger);
            DimensionSet emptyDimensions = DimensionSet.Empty;
            DimensionSet instanceDimensions = new DimensionSet(new MetricDimension[] { new MetricDimension("InstanceName", "Number1") });
            using (IMetricCollector collector = new MetricCollector(logger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), lockstepTime))
            {
                long baseTime = lockstepTime.TimestampMilliseconds;
                lockstepTime.Step(TimeSpan.FromMilliseconds(500));

                IReadOnlyDictionary<CounterInstance, double?> metrics = collector.GetCurrentMetrics();
                Assert.AreEqual(0, metrics.Count);

                // Assert that counter has an empty initial value
                metrics = collector.GetCurrentMetrics();
                Assert.AreEqual(0, metrics.Count);

                // Report some metrics, phase 1
                collector.ReportInstant("TestCounter", emptyDimensions, 1);
                collector.ReportInstant("TestCounter", instanceDimensions, 2);
                collector.ReportContinuous("TestContinousCounter", emptyDimensions, 6);
                lockstepTime.Step(TimeSpan.FromMilliseconds(1000));
                metrics = collector.GetCurrentMetrics();
                Assert.AreEqual(3, metrics.Count);
                CounterInstance instantCounterName = new CounterInstance("TestCounter", emptyDimensions, CounterType.Instant);
                CounterInstance instantCounterNameWithInstance = new CounterInstance("TestCounter", instanceDimensions, CounterType.Instant);
                CounterInstance continousCounterName = new CounterInstance("TestContinousCounter", emptyDimensions, CounterType.Continuous);

                Assert.IsTrue(metrics.ContainsKey(instantCounterName));
                Assert.AreEqual(1, metrics[instantCounterName].GetValueOrDefault(-999), 0.01);
                Assert.IsTrue(metrics.ContainsKey(instantCounterNameWithInstance));
                Assert.AreEqual(2, metrics[instantCounterNameWithInstance].GetValueOrDefault(-999), 0.01);
                Assert.IsTrue(metrics.ContainsKey(continousCounterName));
                Assert.AreEqual(6, metrics[continousCounterName].GetValueOrDefault(-999), 0.01);

                // Report some more metrics, phase 2
                collector.ReportInstant("TestCounter", emptyDimensions, 3);
                collector.ReportInstant("TestCounter", instanceDimensions, 4);
                collector.ReportContinuous("TestContinousCounter", emptyDimensions, 10);
                lockstepTime.Step(TimeSpan.FromMilliseconds(1000));
                metrics = collector.GetCurrentMetrics();
                Assert.AreEqual(3, metrics.Count);
                Assert.IsTrue(metrics.ContainsKey(instantCounterName));
                Assert.AreEqual(2, metrics[instantCounterName].GetValueOrDefault(-999), 0.01);
                Assert.IsTrue(metrics.ContainsKey(instantCounterNameWithInstance));
                Assert.AreEqual(3, metrics[instantCounterNameWithInstance].GetValueOrDefault(-999), 0.01);
                Assert.IsTrue(metrics.ContainsKey(continousCounterName));
                Assert.AreEqual(8, metrics[continousCounterName].GetValueOrDefault(-999), 0.01);

                // Now go far ahead and verify metrics have expired
                lockstepTime.Step(TimeSpan.FromMilliseconds(20000));
                metrics = collector.GetCurrentMetrics();
                Assert.AreEqual(3, metrics.Count);
                Assert.IsTrue(metrics.ContainsKey(instantCounterName));
                Assert.IsFalse(metrics[instantCounterName].HasValue);
                Assert.IsTrue(metrics.ContainsKey(instantCounterNameWithInstance));
                Assert.IsFalse(metrics[instantCounterNameWithInstance].HasValue);
                Assert.IsTrue(metrics.ContainsKey(continousCounterName));
                Assert.IsFalse(metrics[continousCounterName].HasValue);
            }
        }

        [TestMethod]
        public async Task TestMetricCollectorThreaded()
        {
            const int NUM_TEST_THREADS = 10;
            ILogger logger = new ConsoleLogger();
            TaskFactory factory = new TaskFactory();
            List<Task> workerTasks = new List<Task>();
            IRandom rand = new FastRandom();
            const long testTimeMs = 5000;

            using (IMetricCollector collector = new MetricCollector(logger, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), DefaultRealTimeProvider.Singleton))
            {
                Stopwatch timer = Stopwatch.StartNew();

                for (int thread = 0; thread < NUM_TEST_THREADS; thread++)
                {
                    workerTasks.Add(factory.StartNew(() =>
                    {
                        while (timer.ElapsedMilliseconds < testTimeMs)
                        {
                            collector.ReportContinuous("Continuous", DimensionSet.Empty, timer.ElapsedMilliseconds);
                            collector.ReportInstant("Instant", DimensionSet.Empty);
                            collector.ReportPercentile("Percentile", DimensionSet.Empty, rand.NextDouble());
                        }
                    }));
                }

                foreach (Task workerTask in workerTasks)
                {
                    await workerTask;
                }
            }
        }

        [TestMethod]
        public void TestInstrumentationSplitJObjectsByPrivacyClass()
        {
            ILogger traceLogger = new ConsoleLogger();

            RecognizedPhrase obj = new RecognizedPhrase()
            {
                Utterance = "This utterance is EUII",
                Recognition = new List<RecoResult>()
                {
                    new RecoResult()
                    {
                        Confidence = 0.5f,
                        Domain = "domain1",
                        Intent = "intent2",
                        Source = "source",
                        Utterance = new Sentence("This sentence is EUII"),
                        TagHyps = new List<TaggedData>()
                    },
                    new RecoResult()
                    {
                        Confidence = 0.5f,
                        Domain = "domain2",
                        Intent = "intent2",
                        Source = "source",
                        Utterance = new Sentence("This sentence is EUII"),
                        TagHyps = new List<TaggedData>()
                        {
                            new TaggedData()
                            {
                                Utterance = "This tag utterance is EUII and PPD",
                                Slots = new List<SlotValue>()
                                {
                                    new SlotValue("contact_name", "PII", SlotValueFormat.TypedText)
                                }
                            }
                        }
                    }
                }
            };

            Dictionary<string, DataPrivacyClassification> classifications = new Dictionary<string, DataPrivacyClassification>();
            classifications["$.Utterance"] = DataPrivacyClassification.EndUserIdentifiableInformation;
            classifications["$.Recognition"] = DataPrivacyClassification.SystemMetadata;
            classifications["$.Recognition[*].Utterance"] = DataPrivacyClassification.EndUserIdentifiableInformation;
            classifications["$.Recognition[*].TagHyps[*].Slots"] = DataPrivacyClassification.PrivateContent;
            classifications["$.Recognition[*].TagHyps[*].Utterance"] = DataPrivacyClassification.EndUserIdentifiableInformation | DataPrivacyClassification.PublicPersonalData;

            JObject inputJObject = JObject.FromObject(obj);

            //Console.WriteLine("INPUT:");
            //Console.WriteLine(inputJObject.ToString());

            IDictionary<DataPrivacyClassification, JToken> output = CommonInstrumentation.SplitObjectByPrivacyClass(inputJObject, DataPrivacyClassification.SystemMetadata, classifications, traceLogger);

            //foreach (var o in output)
            //{
            //    Console.WriteLine(o.Key.ToString());
            //    Console.WriteLine(o.Value.ToString());
            //}

            Assert.IsTrue(output.ContainsKey(DataPrivacyClassification.SystemMetadata));
            Assert.IsTrue(JToken.DeepEquals(output[DataPrivacyClassification.SystemMetadata],
                JObject.Parse(
                "{" +
                "  \"Recognition\": [" +
                "    {" +
                "      \"Domain\": \"domain1\"," +
                "      \"Intent\": \"intent2\"," +
                "      \"Confidence\": 0.5," +
                "      \"Source\": \"source\"" +
                "    }," +
                "    {" +
                "      \"Domain\": \"domain2\"," +
                "      \"Intent\": \"intent2\"," +
                "      \"Confidence\": 0.5," +
                "      \"TagHyps\": [" +
                "        {" +
                "          \"Confidence\": 0.0" +
                "        }" +
                "      ]," +
                "      \"Source\": \"source\"" +
                "    }" +
                "  ]" +
                "}"
                )));

            Assert.IsTrue(output.ContainsKey(DataPrivacyClassification.EndUserIdentifiableInformation));
            Assert.IsTrue(JToken.DeepEquals(output[DataPrivacyClassification.EndUserIdentifiableInformation],
                JObject.Parse(
                "{" +
                "  \"Utterance\": \"This utterance is EUII\"" +
                "}"
                )));

            Assert.IsTrue(output.ContainsKey(DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.EndUserIdentifiableInformation));
            Assert.IsTrue(JToken.DeepEquals(output[DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.EndUserIdentifiableInformation],
                JObject.Parse(
                "{" +
                "  \"Recognition\": [" +
                "    {" +
                "      \"Utterance\": {" +
                "        \"OriginalText\": \"This sentence is EUII\"," +
                "        \"LexicalForm\": \"\"," +
                "        \"Length\": 0" +
                "      }" +
                "    }," +
                "    {" +
                "      \"Utterance\": {" +
                "        \"OriginalText\": \"This sentence is EUII\"," +
                "        \"LexicalForm\": \"\"," +
                "        \"Length\": 0" +
                "      }" +
                "    }" +
                "  ]" +
                "}"
                )));

            Assert.IsTrue(output.ContainsKey(DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent));
            Assert.IsTrue(JToken.DeepEquals(output[DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent],
                JObject.Parse(
                "{" +
                "  \"Recognition\": [" +
                "    {}," +
                "    {" +
                "      \"TagHyps\": [" +
                "        {" +
                "          \"Slots\": [" +
                "            {" +
                "              \"Name\": \"contact_name\"," +
                "              \"Value\": \"PII\"," +
                "              \"Format\": 1," +
                "              \"LexicalForm\": \"\"" +
                "            }" +
                "          ]" +
                "        }" +
                "      ]" +
                "    }" +
                "  ]" +
                "}"
                )));

            Assert.IsTrue(output.ContainsKey(DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.EndUserIdentifiableInformation | DataPrivacyClassification.PublicPersonalData));
            Assert.IsTrue(JToken.DeepEquals(output[DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.EndUserIdentifiableInformation | DataPrivacyClassification.PublicPersonalData],
                JObject.Parse(
                "{" +
                "  \"Recognition\": [" +
                "    {}," +
                "    {" +
                "      \"TagHyps\": [" +
                "        {" +
                "          \"Utterance\": \"This tag utterance is EUII and PPD\"" +
                "        }" +
                "      ]" +
                "    }" +
                "  ]" +
                "}"
                )));

            // Merge the object back together to assert that we didn't lose anything
            IList<JObject> allObjects = new List<JObject>();
            foreach (JToken token in output.Values)
            {
                allObjects.Add((JObject)token);
            }

            JObject merged = CommonInstrumentation.MergeJObjects(allObjects, traceLogger,
                new JsonMergeSettings()
                {
                    MergeArrayHandling = MergeArrayHandling.Merge,
                    MergeNullValueHandling = MergeNullValueHandling.Merge
                });
            JObject expected = JObject.Parse(
                "{" +
                "  \"Recognition\": [" +
                "    {" +
                "      \"Domain\": \"domain1\"," +
                "      \"Intent\": \"intent2\"," +
                "      \"Confidence\": 0.5," +
                "      \"Source\": \"source\"," +
                "      \"Utterance\": {" +
                "        \"OriginalText\": \"This sentence is EUII\"," +
                "        \"LexicalForm\": \"\"," +
                "        \"Length\": 0" +
                "      }" +
                "    }," +
                "    {" +
                "      \"Domain\": \"domain2\"," +
                "      \"Intent\": \"intent2\"," +
                "      \"Confidence\": 0.5," +
                "      \"TagHyps\": [" +
                "        {" +
                "          \"Confidence\": 0.0," +
                "          \"Slots\": [" +
                "            {" +
                "              \"Name\": \"contact_name\"," +
                "              \"Value\": \"PII\"," +
                "              \"Format\": 1," +
                "              \"LexicalForm\": \"\"" +
                "            }" +
                "          ]," +
                "          \"Utterance\": \"This tag utterance is EUII and PPD\"" +
                "        }" +
                "      ]," +
                "      \"Source\": \"source\"," +
                "      \"Utterance\": {" +
                "        \"OriginalText\": \"This sentence is EUII\"," +
                "        \"LexicalForm\": \"\"," +
                "        \"Length\": 0" +
                "      }" +
                "    }" +
                "  ]," +
                "  \"Utterance\": \"This utterance is EUII\"" +
                "}"
                );
            Console.WriteLine("OUTPUT:");
            Console.WriteLine(merged.ToString());

            Assert.IsTrue(JToken.DeepEquals(expected, merged));
        }

        [TestMethod]
        public void TestInstrumentationPrependObject()
        {
            JToken actual = JObject.Parse("{ \"Data\": \"data1\", \"SomethingElse\": \"data2\" }");
            actual = CommonInstrumentation.PrependPath(actual, "$.ClientRequest.Audio.Hello");
            JObject expected = JObject.Parse("{ \"ClientRequest\": { \"Audio\": { \"Hello\": { \"Data\": \"data1\", \"SomethingElse\": \"data2\" } } } }");
            //Console.WriteLine(actual.ToString());
            //Console.WriteLine(expected.ToString());
            Assert.IsTrue(JToken.DeepEquals(actual, expected));
        }

        [TestMethod]
        public void TestInstrumentationNullifyField()
        {
            JObject actual = JObject.Parse("{ \"ClientRequest\": { \"Audio\": { \"Data\": \"data\", \"SomethingElse\": \"data\" } } }");
            CommonInstrumentation.NullifyField(actual, "$.ClientRequest.Audio.DoesntExist");
            CommonInstrumentation.NullifyField(actual, "$.ClientRequest.Audio.Data");
            JObject expected = JObject.Parse("{ \"ClientRequest\": { \"Audio\": { \"Data\": null, \"SomethingElse\": \"data\" } } }");
            //Console.WriteLine(actual.ToString());
            //Console.WriteLine(expected.ToString());
            Assert.IsTrue(JToken.DeepEquals(actual, expected));
        }

        [TestMethod]
        public void TestAesMessageEncryption()
        {
            IAESDelegates aes = new SystemAESDelegates();
            IRandom srand = new CryptographicRandom();
            byte[] encryptionKey = aes.GenerateKey("This is my password", 16);
            string hexString = BinaryHelpers.ToHexString(encryptionKey);
            Assert.AreEqual("479693C5CA2EA1827B7522233D3DD1E3", hexString);
            IStringEncrypterPii encrypter = new AesStringEncrypterPii(aes, srand, DataPrivacyClassification.Unknown, encryptionKey);
            IStringDecrypterPii decrypter = new AesStringDecrypterPii(aes, new byte[][] { encryptionKey });

            string plaintext = "Ladies and gentlemen we are pleased to present for you a video game legend, please put your hands together for Mr. Hideo Kojima";

            string encrypted = encrypter.EncryptString(plaintext);
            Console.WriteLine(encrypted);
            Assert.IsNotNull(encrypted);
            string decrypted;
            Assert.IsTrue(decrypter.TryDecryptString(encrypted, out decrypted));
            Assert.AreEqual(plaintext, decrypted);
        }

        [TestMethod]
        public void TestRsaMessageEncryption()
        {
            IRandom srand = new FastRandom(10);
            IRSADelegates rsa = new StandardRSADelegates(srand);
            IAESDelegates aes = new SystemAESDelegates();
            SeekableTimeProvider realTime = new SeekableTimeProvider();
            PrivateKey rsaKey = rsa.GenerateRSAKey(1024);
            for (int c = 0; c < 10; c++)
            {
                RsaStringEncrypterPii encrypter = new RsaStringEncrypterPii(rsa, aes, srand, realTime, DataPrivacyClassification.Unknown, rsaKey.GetPublicKey());
                RsaStringDecrypterPii decrypter = new RsaStringDecrypterPii(rsa, aes, new PrivateKey[] { rsaKey }, realTime);

                string plaintext = "Ladies and gentlemen we are pleased to present for you a video game legend, please put your hands together for Mr. Hideo Kojima";

                string encrypted = encrypter.EncryptString(plaintext);
                Console.WriteLine(encrypted);
                Assert.IsNotNull(encrypted);
                string decrypted;
                Assert.IsTrue(decrypter.TryDecryptString(encrypted, out decrypted));
                Assert.AreEqual(plaintext, decrypted);

                // Force a new transient key generation by advancing time
                realTime.SkipTime((long)TimeSpan.FromHours(10).TotalMilliseconds);
                // Wait for key generation to finish
                Thread.Sleep(200);
            }
        }

        [TestMethod]
        public void TestDimensionSetSerialization()
        {
            DimensionSet original = DimensionSet.Empty;
            Assert.AreEqual(original, DimensionSet.Parse(original.ToString()));
            original = new DimensionSet(new MetricDimension[]
                {
                    new MetricDimension("key1", "value1")
                });
            Assert.AreEqual(original, DimensionSet.Parse(original.ToString()));
            original = new DimensionSet(new MetricDimension[]
                {
                    new MetricDimension("key1", "value1"),
                    new MetricDimension("key2", "value2")
                });
            Assert.AreEqual(original, DimensionSet.Parse(original.ToString()));
            original = new DimensionSet(new MetricDimension[]
                {
                    new MetricDimension("key1", "value1"),
                    new MetricDimension("key2", "value2"),
                    new MetricDimension("key3", "value3")
                });
            Assert.AreEqual(original, DimensionSet.Parse(original.ToString()));
        }

        [TestMethod]
        public void TestDimensionSetConstructEmpty()
        {
            DimensionSet d = new DimensionSet(new MetricDimension[0]);
            Assert.IsNotNull(d);
            Assert.IsTrue(d.IsEmpty);
        }

        [TestMethod]
        public void TestDimensionSetConstructSimple()
        {
            DimensionSet d = new DimensionSet(new MetricDimension[] { new MetricDimension("key", "value") });
            Assert.IsNotNull(d);
            Assert.IsFalse(d.IsEmpty);
        }

        [TestMethod]
        public void TestDimensionSetConstructClashingKeys()
        {
            try
            {
                DimensionSet a = new DimensionSet(new MetricDimension[] { new MetricDimension("key1", "value1"), new MetricDimension("key1", "value2") });
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestDimensionSetConstructClashingKeysSameValue()
        {
            try
            {
                DimensionSet a = new DimensionSet(new MetricDimension[] { new MetricDimension("key1", "value1"), new MetricDimension("key1", "value1") });
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestDimensionSetConstructWithNullDimensions()
        {
            try
            {
                DimensionSet d = new DimensionSet(new MetricDimension[] { null });
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestDimensionSetConstructWithNullMetricName()
        {
            try
            {
                DimensionSet d = new DimensionSet(new MetricDimension[] { new MetricDimension(null, "test") });
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestDimensionSetConstructWithNullMetricValue()
        {
            try
            {
                DimensionSet d = new DimensionSet(new MetricDimension[] { new MetricDimension("test", null) });
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestDimensionSetConstructWithEmptyMetricName()
        {
            try
            {
                DimensionSet d = new DimensionSet(new MetricDimension[] { new MetricDimension(string.Empty, "test") });
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestDimensionSetConstructWithEmptyMetricValue()
        {
            try
            {
                DimensionSet d = new DimensionSet(new MetricDimension[] { new MetricDimension("test", string.Empty) });
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestDimensionSetCombineSimple()
        {
            DimensionSet a = new DimensionSet(new MetricDimension[] { new MetricDimension("key1", "value1") });
            DimensionSet b = new DimensionSet(new MetricDimension[] { new MetricDimension("key2", "value2") });
            DimensionSet c = a.Combine(b);
            DimensionSet inverseC = b.Combine(a);
            Assert.IsFalse(c.IsEmpty);
            Assert.IsFalse(inverseC.IsEmpty);
            Assert.AreEqual(c, inverseC);
        }

        [TestMethod]
        public void TestDimensionSetCombineClashingKeys()
        {
            try
            {
                DimensionSet a = new DimensionSet(new MetricDimension[] { new MetricDimension("key1", "value1") });
                DimensionSet b = new DimensionSet(new MetricDimension[] { new MetricDimension("key1", "value2") });
                DimensionSet c = a.Combine(b);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestDimensionSetCombineClashingKeysSameValue()
        {
            try
            {
                DimensionSet a = new DimensionSet(new MetricDimension[] { new MetricDimension("key1", "value1") });
                DimensionSet b = new DimensionSet(new MetricDimension[] { new MetricDimension("key1", "value1") });
                DimensionSet c = a.Combine(b);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestDimensionSetIsSubsetOf()
        {
            // Empty is subset of Empty
            Assert.IsTrue(
                DimensionSet.Empty.IsSubsetOf(
                DimensionSet.Empty));
            // Empty is subset of full set
            Assert.IsTrue(
                DimensionSet.Empty.IsSubsetOf(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke"), new MetricDimension("Title", "Jedi") })));
            // A is a subset of a larger superset B
            Assert.IsTrue(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke") }).IsSubsetOf(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke"), new MetricDimension("Title", "Jedi") })));
            // A is a subset of itself
            Assert.IsTrue(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke"), new MetricDimension("Title", "Jedi") }).IsSubsetOf(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke"), new MetricDimension("Title", "Jedi") })));
            // Order doesn't matter
            Assert.IsTrue(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke"), new MetricDimension("Title", "Jedi") }).IsSubsetOf(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Title", "Jedi"), new MetricDimension("Name", "Luke") })));
            // A is a not subset of a larger superset B if the value is different
            Assert.IsFalse(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "David") }).IsSubsetOf(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke"), new MetricDimension("Title", "Jedi") })));
            // A is not a subset of a smaller set B
            Assert.IsFalse(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke"), new MetricDimension("Title", "Jedi"), new MetricDimension("Rank", "Master") }).IsSubsetOf(
                new DimensionSet(new MetricDimension[] { new MetricDimension("Name", "Luke"), new MetricDimension("Title", "Jedi") })));
        }

        [TestMethod]
        public void TestCommonInstrumentation_FormatTraceId()
        {
            StringBuilder sb = new StringBuilder();
            for (int iter = 0; iter < 10000; iter++)
            {
                Guid activityId = Guid.NewGuid();
                CommonInstrumentation.FormatTraceId(activityId, sb);
                string expected = activityId.ToString("N");
                string actual = sb.ToString();
                Assert.AreEqual(expected, actual);
                sb.Clear();
            }
        }

        [TestMethod]
        public void TestCommonInstrumentation_FormatTraceId_NullBuffer()
        {
            TestAssert.ExceptionThrown<ArgumentNullException>(() => CommonInstrumentation.FormatTraceId(Guid.NewGuid(), null));
        }

        [TestMethod]
        [Ignore]
        public async Task TestMicroProfilerMemoryStream()
        {
            const int NUM_TEST_THREADS = 6;
            const int ITERATIONS_PER_THREAD = 10000;
            using (MemoryStream profilingData = new MemoryStream())
            using (IThreadPool threadPool = new TaskThreadPool())
            using (Barrier threadBarrier = new Barrier(NUM_TEST_THREADS + 1))
            {
                IMicroProfilerClient mpClient = new MemoryMicroProfilerClient();

                try
                {
                    MicroProfiler.Initialize(ref mpClient, NullLogger.Singleton);
                }
                finally
                {
                    // If some other test initialized this profiler, delete the old one
                    mpClient?.Dispose();
                    mpClient = null;
                }

                MemoryMicroProfilerClient originalClient = null;
                try
                {
                    for (int thread = 0; thread < NUM_TEST_THREADS; thread++)
                    {
                        threadPool.EnqueueUserWorkItem(() =>
                        {
                            threadBarrier.SignalAndWait();
                            for (int c = 0; c < ITERATIONS_PER_THREAD / 2; c++)
                            {
                                uint opId = MicroProfiler.GenerateOperationId();
                                MicroProfiler.Send(MicroProfilingEventType.UnitTest, opId);
                            }

                            MicroProfiler.Flush();
                            for (int c = 0; c < ITERATIONS_PER_THREAD / 2; c++)
                            {
                                uint opId = MicroProfiler.GenerateOperationId();
                                MicroProfiler.Send(MicroProfilingEventType.UnitTest, opId);
                            }

                            MicroProfiler.Flush();
                            threadBarrier.SignalAndWait();
                        });
                    }

                    // Tell threads to start
                    threadBarrier.SignalAndWait();

                    // Wait for threads to stop
                    threadBarrier.SignalAndWait();

                    // Set stream back to null and fetch the original client
                    MicroProfiler.Initialize(ref mpClient, NullLogger.Singleton);
                    originalClient = mpClient as MemoryMicroProfilerClient;

                    Assert.IsNotNull(originalClient);
                    originalClient.BaseStream.Seek(0, SeekOrigin.Begin);
                    await originalClient.BaseStream.CopyToAsync(profilingData);

                    // dispose of stream now

                    int numEvents = 0;
                    profilingData.Seek(0, SeekOrigin.Begin);
                    MicroProfileReader reader = new MicroProfileReader(profilingData);
                    string nextMessage;
                    do
                    {
                        nextMessage = await reader.ReadNextEvent();
                        if (nextMessage != null)
                        {
                            Assert.IsTrue(nextMessage.EndsWith("\tExample message for unit testing"));
                            numEvents++;
                        }
                    }
                    while (nextMessage != null);

                    Assert.IsTrue(numEvents > (ITERATIONS_PER_THREAD * NUM_TEST_THREADS * 8 / 10));
                }
                finally
                {
                    originalClient?.Dispose();
                }
            }
        }
    }
}
