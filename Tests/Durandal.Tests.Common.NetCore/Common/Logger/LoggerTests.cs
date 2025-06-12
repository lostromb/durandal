using Durandal.API;
using Durandal.Common.Events;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Security;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Logger;
using Durandal.Common.File;

namespace Durandal.Tests.Common.Logger
{
    [TestClass]
    public class LoggerTests
    {
        private static AesStringEncrypterPii piiEncrypter;
        private static AesStringDecrypterPii piiDecrypter;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            IRandom srand = new FastRandom(10);
            IAESDelegates aes = new SystemAESDelegates();
            byte[] aesKey = new byte[16];
            srand.NextBytes(aesKey);
            piiEncrypter = new AesStringEncrypterPii(aes, srand, DataPrivacyClassification.PrivateContent, aesKey);
            piiDecrypter = new AesStringDecrypterPii(aes, new byte[][] { aesKey });
        }

        [TestMethod]
        public void TestLoggerBasicFormatStrings()
        {
            EventOnlyLogger logger = new EventOnlyLogger();
            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "[{0:yyyy-MM-dd}] Here is the message: {1}", new DateTimeOffset(2020, 11, 28, 0, 0, 0, TimeSpan.Zero), "Kentucky");
            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Operation completed in {0:F2} ms", 12.5411f);
            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Nothing to add here");
            IList <LogEvent> history = logger.History.FilterByCriteria(LogLevel.Std).ToList();
            Assert.AreEqual(3, history.Count);
            Assert.AreEqual("[2020-11-28] Here is the message: Kentucky", history[0].Message);
            Assert.AreEqual("Operation completed in 12.54 ms", history[1].Message);
            Assert.AreEqual("Nothing to add here", history[2].Message);
        }

        [TestMethod]
        public async Task TestAggregateLoggerEvents()
        {
            EventOnlyLogger a = new EventOnlyLogger(
                componentName: "A",
                validLogLevels: LogLevel.All,
                maxLogLevels: LogLevel.All,
                maxPrivacyClasses: DataPrivacyClassification.All,
                defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                backgroundLogThreadPool: null);

            EventOnlyLogger b = new EventOnlyLogger(
                componentName: "B",
                validLogLevels: LogLevel.Err,
                maxLogLevels: LogLevel.Err | LogLevel.Wrn,
                maxPrivacyClasses: DataPrivacyClassification.All,
                defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                backgroundLogThreadPool: null);

            ILogger aggregate = new AggregateLogger("Aggregate", null, a, b);
            Assert.AreEqual("Aggregate", aggregate.ComponentName);
            EventRecorder<LogUpdatedEventArgs> aEventRecorder = new EventRecorder<LogUpdatedEventArgs>();
            EventRecorder<LogUpdatedEventArgs> bEventRecorder = new EventRecorder<LogUpdatedEventArgs>();
            a.LogUpdatedEvent.Subscribe(aEventRecorder.HandleEventAsync);
            b.LogUpdatedEvent.Subscribe(bEventRecorder.HandleEventAsync);

            aggregate.Log("A standard message", LogLevel.Std);

            // Note: this depends on implementation detail of EventOnlyLogger, where log update events will be queued to the lossy thread pool to lower their priority
            await DurandalTaskExtensions.LossyThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);

            // Assert that logger A logged the message, B didn't, and the log updated event was fired on A
            Assert.IsTrue((await aEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(100))).Success);
            Assert.IsFalse((await aEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
            Assert.IsFalse((await bEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
            Assert.AreEqual(1, a.History.FilterByCriteria(LogLevel.Std).Count());
            Assert.AreEqual(0, b.History.FilterByCriteria(LogLevel.Std).Count());

            aggregate.Log("An error message", LogLevel.Err);

            await DurandalTaskExtensions.LossyThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);

            // Assert that logger A and B logged the message, and the log updated event was fired on all loggers exactly once
            Assert.IsTrue((await aEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(100))).Success);
            Assert.IsTrue((await bEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(100))).Success);
            Assert.IsFalse((await aEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
            Assert.IsFalse((await bEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
            Assert.AreEqual(1, a.History.FilterByCriteria(LogLevel.Err).Count());
            Assert.AreEqual(1, b.History.FilterByCriteria(LogLevel.Err).Count());
        }

        //[TestMethod]
        //public async Task TestAggregateLoggerCloning()
        //{
        //    EventOnlyLogger a = new EventOnlyLogger(
        //        componentName: "A",
        //        validLogLevels: LogLevel.All,
        //        maxLogLevels: LogLevel.All,
        //        maxPrivacyClasses: DataPrivacyClassification.SystemMetadata,
        //        defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
        //        backgroundLogThreadPool: null,
        //        piiEncrypter: null);

        //    EventOnlyLogger b = new EventOnlyLogger(
        //        componentName: "B",
        //        validLogLevels: LogLevel.Err,
        //        maxLogLevels: LogLevel.Err | LogLevel.Wrn,
        //        maxPrivacyClasses: DataPrivacyClassification.All,
        //        defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
        //        backgroundLogThreadPool: null,
        //        piiEncrypter: null);

        //    ILogger aggregate = new AggregateLogger("Aggregate", null, null, a, b);
        //    EventRecorder<LogUpdatedEventArgs> overallEventRecorder = new EventRecorder<LogUpdatedEventArgs>();
        //    aggregate.LogUpdatedEvent.Subscribe(overallEventRecorder.HandleEventAsync);

        //    Assert.AreEqual("Aggregate", aggregate.ComponentName);

        //    ILogger clone = aggregate.Clone(
        //        newComponentName: "NewSubComponent",
        //        allowedLogLevels: LogLevel.Err);

        //    Assert.AreEqual("NewSubComponent", clone.ComponentName);

        //    // This should not get logged anywhere
        //    clone.Log("A standard message", LogLevel.Std);
        //    await DurandalTaskExtensions.LossyThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
        //    Assert.IsFalse((await overallEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);

        //    // This should get logged to B
        //    clone.Log("An error message", LogLevel.Err);
        //    await DurandalTaskExtensions.LossyThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
        //    RetrieveResult<CapturedEvent<LogUpdatedEventArgs>> rr = await overallEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
        //    Assert.IsTrue(rr.Success);
        //    Assert.AreEqual("An error message", rr.Result.Args.LogEvent.Message);
        //    Assert.AreEqual(LogLevel.Err, rr.Result.Args.LogEvent.Level);
        //    Assert.AreEqual(DataPrivacyClassification.SystemMetadata, rr.Result.Args.LogEvent.PrivacyClassification);

        //    clone = aggregate.Clone(
        //        newComponentName: "NewSubComponent",
        //        defaultPrivacyClass: DataPrivacyClassification.PrivateContent);

        //    // This should get logged to logger B with data privacy class "PrivateContent"
        //    clone.Log("An error message", LogLevel.Err);
        //    await DurandalTaskExtensions.LossyThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
        //    rr = await overallEventRecorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
        //    Assert.IsTrue(rr.Success);
        //    Assert.AreEqual("An error message", rr.Result.Args.LogEvent.Message);
        //    Assert.AreEqual(LogLevel.Err, rr.Result.Args.LogEvent.Level);
        //    Assert.AreEqual(DataPrivacyClassification.PrivateContent, rr.Result.Args.LogEvent.PrivacyClassification);
        //}

        [TestMethod]
        public void TestLoggingHistoryZeroLength()
        {
            EventOnlyLogger logger = new EventOnlyLogger("Main", LogLevel.All);

            IList<LogEvent> events = logger.History.FilterByCriteria(null).ToList();
            Assert.AreEqual(0, events.Count);
            events = logger.History.FilterByCriteria(new FilterCriteria()).ToList();
            Assert.AreEqual(0, events.Count);
        }

        [TestMethod]
        public void TestLoggingHistorySingleLength()
        {
            EventOnlyLogger logger = new EventOnlyLogger("Main", LogLevel.All);
            Guid traceId = Guid.NewGuid();

            logger.Log(new LogEvent("TestComponent", "Message", LogLevel.Std, DateTimeOffset.Now, traceId));
            IList<LogEvent> events = logger.History.FilterByCriteria(new FilterCriteria()
            {
                TraceId = traceId,
            }).ToList();
            Assert.AreEqual(1, events.Count);
        }

        [TestMethod]
        public void TestLoggingHistoryFilterByLevel()
        {
            EventOnlyLogger logger = new EventOnlyLogger("Main", LogLevel.All);
            Guid trace1 = Guid.NewGuid();
            Guid trace2 = Guid.NewGuid();
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Std, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component1", "Message2", LogLevel.Std, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Vrb, DateTimeOffset.Now, trace1));
            logger.Log(new LogEvent("Component2", "Message", LogLevel.Err, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component2", "Blarf", LogLevel.Wrn, DateTimeOffset.Now, trace2));
            logger.Log(new LogEvent("Component3", "Message", LogLevel.Ins, DateTimeOffset.Now, trace1));

            ILoggingHistory history = logger.History;
            Assert.AreEqual(2, history.FilterByCriteria(new FilterCriteria()
            {
                Level = LogLevel.Std
            }).Count());
            Assert.AreEqual(1, history.FilterByCriteria(new FilterCriteria()
            {
                Level = LogLevel.Vrb
            }).Count());
        }

        [TestMethod]
        public void TestLoggingHistoryFilterByExactComponent()
        {
            EventOnlyLogger logger = new EventOnlyLogger("Main", LogLevel.All);
            Guid trace1 = Guid.NewGuid();
            Guid trace2 = Guid.NewGuid();
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Std, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Vrb, DateTimeOffset.Now, trace1));
            logger.Log(new LogEvent("Component2", "Message", LogLevel.Err, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component2", "Blarf", LogLevel.Wrn, DateTimeOffset.Now, trace2));
            logger.Log(new LogEvent("Component3", "Message", LogLevel.Ins, DateTimeOffset.Now, trace1));

            ILoggingHistory history = logger.History;
            Assert.AreEqual(2, history.FilterByCriteria(new FilterCriteria()
            {
                ExactComponentName = "Component1"
            }).Count());
            Assert.AreEqual(1, history.FilterByCriteria(new FilterCriteria()
            {
                ExactComponentName = "Component2",
                Level = LogLevel.Err
            }).Count());
            Assert.AreEqual(0, history.FilterByCriteria(new FilterCriteria()
            {
                ExactComponentName = "Component3",
                Level = LogLevel.Err
            }).Count());
        }

        [TestMethod]
        public void TestLoggingHistoryFilterBySearchTerm()
        {
            EventOnlyLogger logger = new EventOnlyLogger("Main", LogLevel.All);
            Guid trace1 = Guid.NewGuid();
            Guid trace2 = Guid.NewGuid();
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Std, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Vrb, DateTimeOffset.Now, trace1));
            logger.Log(new LogEvent("Component2", "Message", LogLevel.Err, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component2", "Blarf", LogLevel.Wrn, DateTimeOffset.Now, trace2));
            logger.Log(new LogEvent("Component3", "Message", LogLevel.Ins, DateTimeOffset.Now, trace1));

            ILoggingHistory history = logger.History;
            Assert.AreEqual(4, history.FilterByCriteria(new FilterCriteria()
            {
                SearchTerm = "Message"
            }).Count());
            Assert.AreEqual(1, history.FilterByCriteria(new FilterCriteria()
            {
                SearchTerm = "Blarf"
            }).Count());
        }

        [TestMethod]
        public void TestLoggingHistoryFilterByAllowedComponentNames()
        {
            EventOnlyLogger logger = new EventOnlyLogger("Main", LogLevel.All);
            Guid trace1 = Guid.NewGuid();
            Guid trace2 = Guid.NewGuid();
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Std, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Vrb, DateTimeOffset.Now, trace1));
            logger.Log(new LogEvent("Component2", "Message", LogLevel.Err, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component2", "Blarf", LogLevel.Wrn, DateTimeOffset.Now, trace2));
            logger.Log(new LogEvent("Component3", "Message", LogLevel.Ins, DateTimeOffset.Now, trace1));

            ILoggingHistory history = logger.History;
            Assert.AreEqual(3, history.FilterByCriteria(new FilterCriteria()
            {
                AllowedComponentNames = new HashSet<string>(new[] { "Component1", "Component3" })
            }).Count());
            Assert.AreEqual(0, history.FilterByCriteria(new FilterCriteria()
            {
                AllowedComponentNames = new HashSet<string>(new[] { "Component1", "Component3" }),
                SearchTerm = "Blarf"
            }).Count());
            Assert.AreEqual(1, history.FilterByCriteria(new FilterCriteria()
            {
                AllowedComponentNames = new HashSet<string>(new[] { "Component1", "Component3" }),
                Level = LogLevel.Ins
            }).Count());
        }

        [TestMethod]
        public void TestLoggingHistoryFilterByTrace()
        {
            EventOnlyLogger logger = new EventOnlyLogger("Main", LogLevel.All);
            Guid trace1 = Guid.NewGuid();
            Guid trace2 = Guid.NewGuid();
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Std, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component1", "Message", LogLevel.Vrb, DateTimeOffset.Now, trace1));
            logger.Log(new LogEvent("Component2", "Message", LogLevel.Err, DateTimeOffset.Now));
            logger.Log(new LogEvent("Component2", "Blarf", LogLevel.Wrn, DateTimeOffset.Now, trace2));
            logger.Log(new LogEvent("Component3", "Message", LogLevel.Ins, DateTimeOffset.Now, trace1));

            ILoggingHistory history = logger.History;
            Assert.AreEqual(2, history.FilterByCriteria(new FilterCriteria()
            {
                TraceId = trace1
            }).Count(), "trace test failed");
            Assert.AreEqual(1, history.FilterByCriteria(new FilterCriteria()
            {
                TraceId = trace1,
                ExactComponentName = "Component1"
            }).Count(), "trace + component name test failed");
        }

        [TestMethod]
        public void TestLoggerValidMaxLogLevels()
        {
            EventOnlyLogger logger = new EventOnlyLogger(
                validLogLevels: LogLevel.Std | LogLevel.Err,
                maxLogLevels: LogLevel.Std | LogLevel.Err | LogLevel.Wrn | LogLevel.Ins);
            logger.Log("Yes", LogLevel.Std);
            logger.Log("Yes", LogLevel.Err);
            logger.Log("No", LogLevel.Wrn);
            logger.Log("No", LogLevel.Ins);
            logger.Log("No", LogLevel.Vrb);
            logger = logger.Clone(allowedLogLevels: LogLevel.All) as EventOnlyLogger;
            logger.Log("Yes", LogLevel.Std);
            logger.Log("Yes", LogLevel.Err);
            logger.Log("Yes", LogLevel.Wrn);
            logger.Log("Yes", LogLevel.Ins);
            logger.Log("No", LogLevel.Vrb);
            logger = logger.Clone(allowedLogLevels: LogLevel.None) as EventOnlyLogger;
            logger.Log("No", LogLevel.Std);
            logger.Log("No", LogLevel.Err);
            logger.Log("No", LogLevel.Wrn);
            logger.Log("No", LogLevel.Ins);
            logger.Log("No", LogLevel.Vrb);
            Assert.AreEqual(6, logger.History.Count());
            Assert.AreEqual(0, logger.History.FilterByCriteria(new FilterCriteria() { SearchTerm = "No" }).Count());
        }

        [TestMethod]
        public void TestLoggerDefaultPrivacyClasses()
        {
            EventOnlyLogger logger = new EventOnlyLogger(
                maxPrivacyClasses: DataPrivacyClassification.EndUserPseudonymousIdentifiers | DataPrivacyClassification.PublicNonPersonalData | DataPrivacyClassification.PublicPersonalData | DataPrivacyClassification.SystemMetadata,
                defaultPrivacyClass: DataPrivacyClassification.SystemMetadata);

            logger.Log("Metadata", privacyClass: DataPrivacyClassification.Unknown);
            IList<LogEvent> history = logger.History.FilterByCriteria(new FilterCriteria()
            {
                PrivacyClass = DataPrivacyClassification.SystemMetadata
            }).ToList();
            Assert.AreEqual(1, history.Count);

            logger = logger.Clone(defaultPrivacyClass: DataPrivacyClassification.PublicPersonalData) as EventOnlyLogger;
            logger.Log("Metadata", privacyClass: DataPrivacyClassification.Unknown);
            history = logger.History.FilterByCriteria(new FilterCriteria()
            {
                PrivacyClass = DataPrivacyClassification.PublicPersonalData
            }).ToList();
            Assert.AreEqual(1, history.Count);
        }

        [TestMethod]
        public void TestLoggerValidMaxPrivacyClasses()
        {
            EventOnlyLogger logger = new EventOnlyLogger(
                maxPrivacyClasses: DataPrivacyClassification.EndUserPseudonymousIdentifiers | DataPrivacyClassification.PublicNonPersonalData | DataPrivacyClassification.PublicPersonalData | DataPrivacyClassification.SystemMetadata);
            logger = logger.Clone(allowedPrivacyClasses: DataPrivacyClassification.PublicNonPersonalData | DataPrivacyClassification.PublicPersonalData | DataPrivacyClassification.SystemMetadata) as EventOnlyLogger;
            logger.Log("Yes", privacyClass: DataPrivacyClassification.SystemMetadata);
            logger.Log("No", privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            logger.Log("Yes", privacyClass: DataPrivacyClassification.PublicNonPersonalData);
            logger.Log("Yes", privacyClass: DataPrivacyClassification.PublicPersonalData);
            logger.Log("No", privacyClass: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PublicNonPersonalData | DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            logger.Log("No", privacyClass: DataPrivacyClassification.EndUserIdentifiableInformation);
            logger.Log("No", privacyClass: DataPrivacyClassification.PrivateContent);
            logger.Log("No", privacyClass: DataPrivacyClassification.PrivateContent | DataPrivacyClassification.SystemMetadata);
            logger = logger.Clone(allowedPrivacyClasses: DataPrivacyClassification.SystemMetadata) as EventOnlyLogger;
            logger.Log("Yes", privacyClass: DataPrivacyClassification.SystemMetadata);
            logger.Log("No", privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            logger.Log("No", privacyClass: DataPrivacyClassification.PublicNonPersonalData);
            logger.Log("No", privacyClass: DataPrivacyClassification.PublicPersonalData);
            logger.Log("No", privacyClass: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PublicNonPersonalData | DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            logger.Log("No", privacyClass: DataPrivacyClassification.EndUserIdentifiableInformation);
            logger.Log("No", privacyClass: DataPrivacyClassification.PrivateContent);
            logger.Log("No", privacyClass: DataPrivacyClassification.PrivateContent | DataPrivacyClassification.SystemMetadata);
            logger = logger.Clone(allowedPrivacyClasses: DataPrivacyClassification.All) as EventOnlyLogger;
            logger.Log("Yes", privacyClass: DataPrivacyClassification.SystemMetadata);
            logger.Log("Yes", privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            logger.Log("Yes", privacyClass: DataPrivacyClassification.PublicNonPersonalData);
            logger.Log("Yes", privacyClass: DataPrivacyClassification.PublicPersonalData);
            logger.Log("Yes", privacyClass: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PublicNonPersonalData | DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            logger.Log("No", privacyClass: DataPrivacyClassification.EndUserIdentifiableInformation);
            logger.Log("No", privacyClass: DataPrivacyClassification.PrivateContent);
            logger.Log("No", privacyClass: DataPrivacyClassification.PrivateContent | DataPrivacyClassification.SystemMetadata);
            Assert.AreEqual(9, logger.History.Count());
            Assert.AreEqual(0, logger.History.FilterByCriteria(new FilterCriteria() { SearchTerm = "No" }).Count());
        }

        [TestMethod]
        public void TestLogEventFirst3DigitsOfTraceId()
        {
            Guid traceId = Guid.Parse("f07a4cd33816455fbdb55ef433a20c3d");
            Assert.AreEqual("f07", CommonInstrumentation.GetFirst3DigitsOfTraceId(traceId));

            for (int iter = 0; iter < 1000; iter++)
            {
                traceId = Guid.NewGuid();
                Assert.AreEqual(traceId.ToString("N").Substring(0, 3), CommonInstrumentation.GetFirst3DigitsOfTraceId(traceId));
            }
        }

        [TestMethod]
        public void TestLogEventFirst3DigitsOfTraceIdToStringBuilder()
        {
            StringBuilder builder = new StringBuilder();
            Guid traceId = Guid.Parse("f07a4cd33816455fbdb55ef433a20c3d");
            CommonInstrumentation.GetFirst3DigitsOfTraceId(traceId, builder);
            Assert.AreEqual("f07", builder.ToString());

            for (int iter = 0; iter < 1000; iter++)
            {
                builder.Clear();
                traceId = Guid.NewGuid();
                CommonInstrumentation.GetFirst3DigitsOfTraceId(traceId, builder);
                Assert.AreEqual(traceId.ToString("N").Substring(0, 3), builder.ToString());
            }
        }

        [TestMethod]
        [DataRow(DataPrivacyClassification.Unknown)]
        [DataRow(DataPrivacyClassification.SystemMetadata)]
        [DataRow(DataPrivacyClassification.PublicPersonalData)]
        [DataRow(DataPrivacyClassification.EndUserIdentifiableInformation)]
        [DataRow(DataPrivacyClassification.EndUserPseudonymousIdentifiers)]
        [DataRow(DataPrivacyClassification.PrivateContent)]
        [DataRow(DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PublicNonPersonalData)]
        [DataRow(DataPrivacyClassification.PrivateContent | DataPrivacyClassification.EndUserIdentifiableInformation | DataPrivacyClassification.PublicPersonalData)]
        [DataRow(DataPrivacyClassification.PublicPersonalData | DataPrivacyClassification.EndUserIdentifiableInformation | DataPrivacyClassification.EndUserPseudonymousIdentifiers)]
        public void TestCommonInstrumentationParsePrivacyClass(DataPrivacyClassification value)
        {
            StringBuilder sb = new StringBuilder();
            CommonInstrumentation.WritePrivacyClassification(value, sb);
            string encoded = sb.ToString();
            DataPrivacyClassification parsed = CommonInstrumentation.ParsePrivacyClassString(encoded);
            Assert.AreEqual(value, parsed);
        }

        [TestMethod]
        public async Task TestLoggerFlushesAfterLoggingCritical()
        {
            FakeLogger fakeLogger = new FakeLogger(new TaskThreadPool(), "Test");
            ILogger traceLogger = fakeLogger.CreateTraceLogger(Guid.NewGuid(), "Component");
            traceLogger.Log("This base will explode in 5 seconds", LogLevel.Crt);
            RetrieveResult<CapturedEvent<LogUpdatedEventArgs>> rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr.Success);
            Assert.AreEqual(LogLevel.Crt, rr.Result.Args.LogEvent.Level);
            RetrieveResult<CapturedEvent<EventArgs>> rr2 = await fakeLogger.LogFlushedEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr2.Success);
        }

        [TestMethod]
        public async Task TestLoggerPiiEncryptionBasic()
        {
            FakeLogger fakeLogger = new FakeLogger(
                new TaskThreadPool(),
                "BaseLogger",
                validLogLevels: LogLevel.Std | LogLevel.Err,
                maxLogLevels: LogLevel.Std | LogLevel.Err | LogLevel.Wrn,
                defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                validPrivacyClasses: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent);

            PiiEncryptingLogger testLogger = new PiiEncryptingLogger(fakeLogger, piiEncrypter);
            Assert.AreEqual("BaseLogger", testLogger.ComponentName);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata, testLogger.DefaultPrivacyClass);
            Assert.AreEqual(LogLevel.Std | LogLevel.Err, testLogger.ValidLogLevels);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent, testLogger.ValidPrivacyClasses);

            // First, test messages that should be filtered out automatically
            testLogger.Log("Shouldn't see this", LogLevel.Wrn);
            testLogger.Log("Shouldn't see this", LogLevel.Std, privacyClass: DataPrivacyClassification.PublicNonPersonalData);
            Assert.IsFalse((await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);

            // Non-PII message with unknown privacy class
            testLogger.Log("This should be plaintext", LogLevel.Std, privacyClass: DataPrivacyClassification.Unknown);
            RetrieveResult<CapturedEvent<LogUpdatedEventArgs>> rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr.Success);
            Assert.AreEqual("This should be plaintext", rr.Result.Args.LogEvent.Message);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata, rr.Result.Args.LogEvent.PrivacyClassification);

            // Non-PII message with fixed privacy class
            testLogger.Log("This should be plaintext 2", LogLevel.Std, privacyClass: DataPrivacyClassification.SystemMetadata);
            rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr.Success);
            Assert.AreEqual("This should be plaintext 2", rr.Result.Args.LogEvent.Message);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata, rr.Result.Args.LogEvent.PrivacyClassification);

            // Finally, test PII messages
            testLogger.Log("This should be encrypted", LogLevel.Std, privacyClass: DataPrivacyClassification.PrivateContent);
            rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr.Success);
            Assert.IsTrue(CommonInstrumentation.IsEncrypted(rr.Result.Args.LogEvent.Message));
            Assert.AreEqual(DataPrivacyClassification.PrivateContent, rr.Result.Args.LogEvent.PrivacyClassification);
            string plainText;
            Assert.IsTrue(piiDecrypter.TryDecryptString(rr.Result.Args.LogEvent.Message, out plainText));
            Assert.AreEqual("This should be encrypted", plainText);
        }

        [TestMethod]
        public async Task TestLoggerPiiEncryptionTraceLogger()
        {
            FakeLogger fakeLogger = new FakeLogger(
                new TaskThreadPool(),
                "BaseLogger",
                validLogLevels: LogLevel.Std | LogLevel.Err,
                maxLogLevels: LogLevel.Std | LogLevel.Err | LogLevel.Wrn,
                defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                validPrivacyClasses: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent);

            PiiEncryptingLogger encryptingLogger = new PiiEncryptingLogger(fakeLogger, piiEncrypter);
            Assert.AreEqual("BaseLogger", encryptingLogger.ComponentName);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata, encryptingLogger.DefaultPrivacyClass);
            Assert.AreEqual(LogLevel.Std | LogLevel.Err, encryptingLogger.ValidLogLevels);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent, encryptingLogger.ValidPrivacyClasses);

            ILogger traceLogger = encryptingLogger.CreateTraceLogger(Guid.NewGuid(), "TraceLogger");
            Assert.AreEqual("TraceLogger", traceLogger.ComponentName);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata, traceLogger.DefaultPrivacyClass);
            Assert.AreEqual(LogLevel.Std | LogLevel.Err, traceLogger.ValidLogLevels);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent, traceLogger.ValidPrivacyClasses);
            Assert.IsTrue(traceLogger.TraceId.HasValue);

            // First, test messages that should be filtered out automatically
            traceLogger.Log("Shouldn't see this", LogLevel.Wrn);
            traceLogger.Log("Shouldn't see this", LogLevel.Std, privacyClass: DataPrivacyClassification.PublicNonPersonalData);
            Assert.IsFalse((await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);

            // Non-PII message with unknown privacy class
            traceLogger.Log("This should be plaintext", LogLevel.Std, privacyClass: DataPrivacyClassification.Unknown);
            RetrieveResult<CapturedEvent<LogUpdatedEventArgs>> rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr.Success);
            Assert.AreEqual("This should be plaintext", rr.Result.Args.LogEvent.Message);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata, rr.Result.Args.LogEvent.PrivacyClassification);

            // Non-PII message with fixed privacy class
            traceLogger.Log("This should be plaintext 2", LogLevel.Std, privacyClass: DataPrivacyClassification.SystemMetadata);
            rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr.Success);
            Assert.AreEqual("This should be plaintext 2", rr.Result.Args.LogEvent.Message);
            Assert.AreEqual(DataPrivacyClassification.SystemMetadata, rr.Result.Args.LogEvent.PrivacyClassification);

            // Finally, test PII messages
            traceLogger.Log("This should be encrypted", LogLevel.Std, privacyClass: DataPrivacyClassification.PrivateContent);
            rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr.Success);
            Assert.IsTrue(CommonInstrumentation.IsEncrypted(rr.Result.Args.LogEvent.Message));
            Assert.AreEqual(DataPrivacyClassification.PrivateContent, rr.Result.Args.LogEvent.PrivacyClassification);
            string plainText;
            Assert.IsTrue(piiDecrypter.TryDecryptString(rr.Result.Args.LogEvent.Message, out plainText));
            Assert.AreEqual("This should be encrypted", plainText);
        }

        [TestMethod]
        public async Task TestLoggerPiiEncryptionEncryptUnknownMessagesByDefault()
        {
            FakeLogger fakeLogger = new FakeLogger(
                new TaskThreadPool(),
                "BaseLogger",
                validLogLevels: LogLevel.Std | LogLevel.Err,
                maxLogLevels: LogLevel.Std | LogLevel.Err | LogLevel.Wrn,
                defaultPrivacyClass: DataPrivacyClassification.PrivateContent,
                validPrivacyClasses: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent);

            PiiEncryptingLogger testLogger = new PiiEncryptingLogger(fakeLogger, piiEncrypter);

            testLogger.Log("This should be encrypted", LogLevel.Std, privacyClass: DataPrivacyClassification.Unknown);
            RetrieveResult<CapturedEvent<LogUpdatedEventArgs>> rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
            Assert.IsTrue(rr.Success);
            Assert.IsTrue(CommonInstrumentation.IsEncrypted(rr.Result.Args.LogEvent.Message));
            Assert.AreEqual(DataPrivacyClassification.PrivateContent, rr.Result.Args.LogEvent.PrivacyClassification);
            string plainText;
            Assert.IsTrue(piiDecrypter.TryDecryptString(rr.Result.Args.LogEvent.Message, out plainText));
            Assert.AreEqual("This should be encrypted", plainText);
        }

        [TestMethod]
        public async Task TestLoggerPiiEncryptionEncryptsOnAllPaths()
        {
            FakeLogger fakeLogger = new FakeLogger(
                new TaskThreadPool(),
                "BaseLogger",
                validLogLevels: LogLevel.Std | LogLevel.Err,
                maxLogLevels: LogLevel.Std | LogLevel.Err | LogLevel.Wrn,
                defaultPrivacyClass: DataPrivacyClassification.PrivateContent,
                validPrivacyClasses: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PrivateContent);

            PiiEncryptingLogger testLogger = new PiiEncryptingLogger(fakeLogger, piiEncrypter);

            string message = "This should be encrypted";
            testLogger.Log(message, LogLevel.Std);
            testLogger.Log((object)message, LogLevel.Std);
            Exception fakeException = new Exception(message);
            testLogger.Log(fakeException, LogLevel.Std);
            testLogger.Log(() => message, LogLevel.Std);
            testLogger.Log(new LogEvent("MockComponent", message, LogLevel.Std, DateTimeOffset.UtcNow));
            testLogger.DispatchAsync((ILogger delegateLogger, DateTimeOffset logStartTime) =>
            {
                delegateLogger.Log(message, LogLevel.Std);
            });

            // Wait for 6 log messages to come through, and assert that they are all encrypted.
            for (int c = 0; c < 6; c++)
            {
                RetrieveResult<CapturedEvent<LogUpdatedEventArgs>> rr = await fakeLogger.LogWrittenEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
                Assert.IsTrue(rr.Success);
                Assert.IsTrue(CommonInstrumentation.IsEncrypted(rr.Result.Args.LogEvent.Message));
                Assert.AreEqual(DataPrivacyClassification.PrivateContent, rr.Result.Args.LogEvent.PrivacyClassification);
                string plainText;
                Assert.IsTrue(piiDecrypter.TryDecryptString(rr.Result.Args.LogEvent.Message, out plainText));
                Assert.IsTrue(plainText.Contains(message));
            }
        }

        [TestMethod]
        public async Task TestFileLogInstrumentationReader()
        {
            Guid traceId = Guid.NewGuid();
            ILogger bootstrapLogger = new ConsoleLogger();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            FileLogger logger = new FileLogger(
                fileSystem,
                bootstrapLogger: bootstrapLogger,
                logDirectory: VirtualPath.Root);
            logger.Log("Here is a regular message");
            logger.Log("Here is an error message", LogLevel.Err);
            logger.Log("Here is a PII message", LogLevel.Err, privacyClass: DataPrivacyClassification.PrivateContent);
            logger.Log("Here is a message with trace ID", LogLevel.Std, traceId);
            logger.Log("Here is a complex PII message", LogLevel.Err, privacyClass: DataPrivacyClassification.PrivateContent | DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            logger.DisposeCore();

            ILogEventSource logParser = new FileLogEventSource(fileSystem, VirtualPath.Root, bootstrapLogger);
            List<LogEvent> parsedEvents = (await logParser.GetLogEvents(new FilterCriteria())).ToList();
            Assert.AreEqual(5, parsedEvents.Count);
            Assert.AreEqual("Here is a regular message", parsedEvents[0].Message);
            Assert.IsNull(parsedEvents[0].TraceId);
            Assert.AreEqual(LogLevel.Err, parsedEvents[1].Level);
            Assert.AreEqual(DataPrivacyClassification.PrivateContent, parsedEvents[2].PrivacyClassification);
            Assert.AreEqual(traceId, parsedEvents[3].TraceId);
            Assert.AreEqual(DataPrivacyClassification.PrivateContent | DataPrivacyClassification.EndUserPseudonymousIdentifiers, parsedEvents[4].PrivacyClassification);
        }

        private class FakeLogger : LoggerBase
        {
            public FakeLogger(
                IThreadPool backgroundLogThreadPool,
                string componentName = "Main",
                LogLevel validLogLevels = DEFAULT_LOG_LEVELS,
                LogLevel maxLogLevels = LogLevel.All,
                DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata,
                DataPrivacyClassification validPrivacyClasses = DataPrivacyClassification.All,
                DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All)
                : base(new FakeLoggerCore(),
                      new LoggerContext()
                      {
                          ComponentName = componentName,
                          TraceId = null,
                          ValidLogLevels = validLogLevels,
                          MaxLogLevels = maxLogLevels,
                          DefaultPrivacyClass = defaultPrivacyClass,
                          ValidPrivacyClasses = validPrivacyClasses,
                          MaxPrivacyClasses = maxPrivacyClasses,
                          BackgroundLoggingThreadPool = backgroundLogThreadPool
                      })
            { 
            }

            /// <summary>
            /// Private constructor for creating inherited logger objects
            /// </summary>
            /// <param name="componentName"></param>
            /// <param name="stream"></param>
            private FakeLogger(ILoggerCore core, LoggerContext context)
                    : base(core, context)
            {
            }

            public EventRecorder<LogUpdatedEventArgs> LogWrittenEvent => ((FakeLoggerCore)Core)._logWrittenEvent;

            public EventRecorder<EventArgs> LogFlushedEvent => ((FakeLoggerCore)Core)._logFlushedEvent;


            protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext context)
            {
                return new FakeLogger(core, context);
            }

            /// <summary>
            /// This is the context object shared between all clones of the console logger
            /// </summary>
            private class FakeLoggerCore : ILoggerCore
            {
                internal readonly EventRecorder<LogUpdatedEventArgs> _logWrittenEvent = new EventRecorder<LogUpdatedEventArgs>();
                internal readonly EventRecorder<EventArgs> _logFlushedEvent = new EventRecorder<EventArgs>();

                public FakeLoggerCore()
                {
                }

                public void Dispose() { }

                public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
                {
                    _logFlushedEvent.HandleEvent(this, new EventArgs());
                    return DurandalTaskExtensions.NoOpTask;
                }

                public void LoggerImplementation(PooledLogEvent value)
                {
                    LogEvent converted = value.ToLogEvent();
                    value.Dispose();

                    _logWrittenEvent.HandleEvent(this, new LogUpdatedEventArgs(converted));
                }
            }
        }
    }
}
