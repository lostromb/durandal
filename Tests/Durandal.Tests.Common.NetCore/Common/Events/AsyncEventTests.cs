using Durandal.Common.Client;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Events
{
    [TestClass]
    public class AsyncEventTests
    {
        [TestMethod]
        public async Task TestAsyncEventBasic()
        {
            AsyncEvent<TextEventArgs> myEvent = new AsyncEvent<TextEventArgs>();
            Assert.IsFalse(myEvent.HasSubscribers);

            EventRecorder<TextEventArgs> recorder = new EventRecorder<TextEventArgs>();
            myEvent.Subscribe(recorder.HandleEventAsync);
            Assert.IsTrue(myEvent.HasSubscribers);

            await myEvent.Fire(this, new TextEventArgs("Test"), DefaultRealTimeProvider.Singleton);
            await AssertExactlyOneEventWasCaptured(recorder, "Test");

            myEvent.Unsubscribe(recorder.HandleEventAsync);
            Assert.IsFalse(myEvent.HasSubscribers);
        }

        [TestMethod]
        public async Task TestAsyncEventMultipleSubscribers()
        {
            AsyncEvent<TextEventArgs> myEvent = new AsyncEvent<TextEventArgs>();
            List<EventRecorder<TextEventArgs>> recorders = new List<EventRecorder<TextEventArgs>>();

            for (int c = 0; c < 10; c++)
            {
                EventRecorder<TextEventArgs> recorder = new EventRecorder<TextEventArgs>();
                recorders.Add(recorder);
                myEvent.Subscribe(recorder.HandleEventAsync);
            }

            Assert.IsTrue(myEvent.HasSubscribers);

            await myEvent.Fire(this, new TextEventArgs("Test"), DefaultRealTimeProvider.Singleton);

            foreach (EventRecorder<TextEventArgs> recorder in recorders)
            {
                await AssertExactlyOneEventWasCaptured(recorder, "Test");
            }

            foreach (EventRecorder<TextEventArgs> recorder in recorders)
            {
                Assert.IsTrue(myEvent.HasSubscribers);
                myEvent.Unsubscribe(recorder.HandleEventAsync);
            }

            Assert.IsFalse(myEvent.HasSubscribers);
        }

        [TestMethod]
        public void TestAsyncEventCannotSubscribeMultipleTimes()
        {
            AsyncEvent<TextEventArgs> myEvent = new AsyncEvent<TextEventArgs>();
            Assert.IsFalse(myEvent.HasSubscribers);

            EventRecorder<TextEventArgs> recorder = new EventRecorder<TextEventArgs>();
            myEvent.Subscribe(recorder.HandleEventAsync);
            Assert.IsTrue(myEvent.HasSubscribers);

            try
            {
                myEvent.Subscribe(recorder.HandleEventAsync);
                Assert.Fail("Should have thrown an InvalidOperationException");
            }
            catch (InvalidOperationException) { }
        }


        [TestMethod]
        public void TestAsyncEventCannotUnsubscribeWhenNotSubscribed()
        {
            AsyncEvent<TextEventArgs> myEvent = new AsyncEvent<TextEventArgs>();
            EventRecorder<TextEventArgs> recorder = new EventRecorder<TextEventArgs>();

            try
            {
                myEvent.Unsubscribe(recorder.HandleEventAsync);
                Assert.Fail("Should have thrown an InvalidOperationException");
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public async Task TestAsyncEventSubordinatesDontCollide()
        {
            AsyncEvent<TextEventArgs> rootEvent = new AsyncEvent<TextEventArgs>();
            AsyncEvent<TextEventArgs> echo1 = new AsyncEvent<TextEventArgs>(rootEvent);
            AsyncEvent<TextEventArgs> echo2 = new AsyncEvent<TextEventArgs>(rootEvent);

            EventRecorder<TextEventArgs> rootEventRecorder = new EventRecorder<TextEventArgs>();
            EventRecorder<TextEventArgs> echo1Recorder = new EventRecorder<TextEventArgs>();
            EventRecorder<TextEventArgs> echo2Recorder = new EventRecorder<TextEventArgs>();

            rootEvent.Subscribe(rootEventRecorder.HandleEventAsync);
            echo1.Subscribe(echo1Recorder.HandleEventAsync);
            echo2.Subscribe(echo2Recorder.HandleEventAsync);

            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsTrue(echo1.HasSubscribers);
            Assert.IsTrue(echo2.HasSubscribers);

            await rootEvent.Fire(this, new TextEventArgs("One"), DefaultRealTimeProvider.Singleton);

            // Assert that all recorders got their own copy of the event
            await AssertExactlyOneEventWasCaptured(rootEventRecorder, "One");
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "One");
            await AssertExactlyOneEventWasCaptured(echo2Recorder, "One");

            // Unsubscribe echo2
            echo2.Unsubscribe(echo2Recorder.HandleEventAsync);
            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsTrue(echo1.HasSubscribers);
            Assert.IsFalse(echo2.HasSubscribers);

            await rootEvent.Fire(this, new TextEventArgs("Two"), DefaultRealTimeProvider.Singleton);

            // Root and echo1 should have got an event
            await AssertExactlyOneEventWasCaptured(rootEventRecorder, "Two");
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "Two");
            await AssertNoEventWasCaptured(echo2Recorder);

            // Unsubscribe echo1
            echo1.Unsubscribe(echo1Recorder.HandleEventAsync);
            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsFalse(echo1.HasSubscribers);
            Assert.IsFalse(echo2.HasSubscribers);

            await rootEvent.Fire(this, new TextEventArgs("Three"), DefaultRealTimeProvider.Singleton);

            // Only root should have gotten an event
            await AssertExactlyOneEventWasCaptured(rootEventRecorder, "Three");
            await AssertNoEventWasCaptured(echo1Recorder);
            await AssertNoEventWasCaptured(echo2Recorder);
        }

        [TestMethod]
        public async Task TestAsyncEventSubordinatesCanChain()
        {
            AsyncEvent<TextEventArgs> rootEvent = new AsyncEvent<TextEventArgs>();
            AsyncEvent<TextEventArgs> echo1 = new AsyncEvent<TextEventArgs>(rootEvent);
            AsyncEvent<TextEventArgs> echo2 = new AsyncEvent<TextEventArgs>(echo1);

            EventRecorder<TextEventArgs> rootEventRecorder = new EventRecorder<TextEventArgs>();
            EventRecorder<TextEventArgs> echo1Recorder = new EventRecorder<TextEventArgs>();
            EventRecorder<TextEventArgs> echo2Recorder = new EventRecorder<TextEventArgs>();

            rootEvent.Subscribe(rootEventRecorder.HandleEventAsync);
            echo1.Subscribe(echo1Recorder.HandleEventAsync);
            echo2.Subscribe(echo2Recorder.HandleEventAsync);

            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsTrue(echo1.HasSubscribers);
            Assert.IsTrue(echo2.HasSubscribers);

            await rootEvent.Fire(this, new TextEventArgs("One"), DefaultRealTimeProvider.Singleton);

            // Assert that all recorders got their own copy of the event
            await AssertExactlyOneEventWasCaptured(rootEventRecorder, "One");
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "One");
            await AssertExactlyOneEventWasCaptured(echo2Recorder, "One");

            // Unsubscribe echo2
            echo2.Unsubscribe(echo2Recorder.HandleEventAsync);
            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsTrue(echo1.HasSubscribers);
            Assert.IsFalse(echo2.HasSubscribers);

            await rootEvent.Fire(this, new TextEventArgs("Two"), DefaultRealTimeProvider.Singleton);

            // Root and echo1 should have got an event
            await AssertExactlyOneEventWasCaptured(rootEventRecorder, "Two");
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "Two");
            await AssertNoEventWasCaptured(echo2Recorder);

            // Unsubscribe echo1
            echo1.Unsubscribe(echo1Recorder.HandleEventAsync);
            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsFalse(echo1.HasSubscribers);
            Assert.IsFalse(echo2.HasSubscribers);

            await rootEvent.Fire(this, new TextEventArgs("Three"), DefaultRealTimeProvider.Singleton);

            // Only root should have gotten an event
            await AssertExactlyOneEventWasCaptured(rootEventRecorder, "Three");
            await AssertNoEventWasCaptured(echo1Recorder);
            await AssertNoEventWasCaptured(echo2Recorder);
        }

        [TestMethod]
        public async Task TestAsyncEventSubordinateEvents()
        {
            AsyncEvent<TextEventArgs> rootEvent = new AsyncEvent<TextEventArgs>();
            AsyncEvent<TextEventArgs> echo1 = new AsyncEvent<TextEventArgs>(rootEvent);
            AsyncEvent<TextEventArgs> echo2 = new AsyncEvent<TextEventArgs>(echo1);

            EventRecorder<TextEventArgs> rootEventRecorder = new EventRecorder<TextEventArgs>();
            EventRecorder<TextEventArgs> echo1Recorder = new EventRecorder<TextEventArgs>();
            EventRecorder<TextEventArgs> echo2Recorder = new EventRecorder<TextEventArgs>();

            rootEvent.Subscribe(rootEventRecorder.HandleEventAsync);
            echo1.Subscribe(echo1Recorder.HandleEventAsync);
            echo2.Subscribe(echo2Recorder.HandleEventAsync);

            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsTrue(echo1.HasSubscribers);
            Assert.IsTrue(echo2.HasSubscribers);

            await rootEvent.Fire(this, new TextEventArgs("One"), DefaultRealTimeProvider.Singleton);

            // All handlers get 1 event
            await AssertExactlyOneEventWasCaptured(rootEventRecorder, "One");
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "One");
            await AssertExactlyOneEventWasCaptured(echo2Recorder, "One");

            // Remove root listener from root event
            rootEvent.Unsubscribe(rootEventRecorder.HandleEventAsync);

            // Root event still has subscribers via echo1 and echo2
            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsTrue(echo1.HasSubscribers);
            Assert.IsTrue(echo2.HasSubscribers);

            // Fire event on root.  Echo1 and Echo2 pick it up
            await rootEvent.Fire(this, new TextEventArgs("Two"), DefaultRealTimeProvider.Singleton);
            await AssertNoEventWasCaptured(rootEventRecorder);
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "Two");
            await AssertExactlyOneEventWasCaptured(echo2Recorder, "Two");

            // Fire event on echo1; we expect echo1 and echo2 handlers to pick it up
            await echo1.Fire(this, new TextEventArgs("Three"), DefaultRealTimeProvider.Singleton);
            await AssertNoEventWasCaptured(rootEventRecorder);
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "Three");
            await AssertExactlyOneEventWasCaptured(echo2Recorder, "Three");

            echo2.Unsubscribe(echo2Recorder.HandleEventAsync);

            Assert.IsTrue(rootEvent.HasSubscribers);
            Assert.IsTrue(echo1.HasSubscribers);
            Assert.IsFalse(echo2.HasSubscribers);

            // Fire event on root. Echo 1 handler should pick it up
            await rootEvent.Fire(this, new TextEventArgs("Four"), DefaultRealTimeProvider.Singleton);
            await AssertNoEventWasCaptured(rootEventRecorder);
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "Four");
            await AssertNoEventWasCaptured(echo2Recorder);

            // Nobody is listening to echo2
            await echo2.Fire(this, new TextEventArgs("Five"), DefaultRealTimeProvider.Singleton);
            await AssertNoEventWasCaptured(rootEventRecorder);
            await AssertNoEventWasCaptured(echo1Recorder);
            await AssertNoEventWasCaptured(echo2Recorder);

            // But an event on echo1 still gets through
            await echo1.Fire(this, new TextEventArgs("Six"), DefaultRealTimeProvider.Singleton);
            await AssertNoEventWasCaptured(rootEventRecorder);
            await AssertExactlyOneEventWasCaptured(echo1Recorder, "Six");
            await AssertNoEventWasCaptured(echo2Recorder);

            echo1.Unsubscribe(echo1Recorder.HandleEventAsync);

            Assert.IsFalse(rootEvent.HasSubscribers);
            Assert.IsFalse(echo1.HasSubscribers);
            Assert.IsFalse(echo2.HasSubscribers);
            await rootEvent.Fire(this, new TextEventArgs("Junk"), DefaultRealTimeProvider.Singleton);
            await echo1.Fire(this, new TextEventArgs("Junk"), DefaultRealTimeProvider.Singleton);
            await echo2.Fire(this, new TextEventArgs("Junk"), DefaultRealTimeProvider.Singleton);
            await AssertNoEventWasCaptured(rootEventRecorder);
            await AssertNoEventWasCaptured(echo1Recorder);
            await AssertNoEventWasCaptured(echo2Recorder);
        }

        [TestMethod]
        public void TestAsyncEventLogsExceptionsOnBackgroundTasks()
        {
            ILogger logger = new ConsoleLogger();
            EventOnlyLogger backgroundTaskLogger = new EventOnlyLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger);
            AsyncEvent<EventArgs> myEvent = new AsyncEvent<EventArgs>();
            myEvent.Subscribe(EventHandlerThatThrowsException);

            // Schedule an event that will eventually throw exceptions
            myEvent.FireInBackground(this, new EventArgs(), backgroundTaskLogger, lockStepTime);
            lockStepTime.Step(TimeSpan.FromMilliseconds(100), 10);

            // Assert that the logger contains error messages
            Assert.IsTrue(backgroundTaskLogger.History.FilterByCriteria(new FilterCriteria()
            {
                Level = LogLevel.Err,
                SearchTerm = "Something bad happened"
            }).Any());
        }

        public async Task EventHandlerThatThrowsException<T>(object source, T args, IRealTimeProvider realTime) where T : EventArgs
        {
            await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);
            throw new IndexOutOfRangeException("Oh no! Something bad happened. This exception was raised from an async event test case.");
        }

        private static async Task AssertExactlyOneEventWasCaptured(EventRecorder<TextEventArgs> recorder, string expectedText)
        {
            RetrieveResult<CapturedEvent<TextEventArgs>> rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
            Assert.IsTrue(rr.Success);
            CapturedEvent<TextEventArgs> capturedEvent = rr.Result;
            Assert.AreEqual(expectedText, capturedEvent.Args.Text);
            rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
            Assert.IsFalse(rr.Success);
        }

        private static async Task AssertNoEventWasCaptured(EventRecorder<TextEventArgs> recorder)
        {
            RetrieveResult<CapturedEvent<TextEventArgs>> rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
            Assert.IsFalse(rr.Success);
        }
    }
}
