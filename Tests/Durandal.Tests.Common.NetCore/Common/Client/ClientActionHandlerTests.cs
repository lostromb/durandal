using Durandal.API;
using Durandal.Common.Client;
using Durandal.Common.Client.Actions;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Client
{
    [TestClass]
    public class ClientActionHandlerTests
    {
        [TestMethod]
        public async Task TestDelayedDialogActionHandlerBasic()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            using (ExecuteDelayedActionHandler handler = new ExecuteDelayedActionHandler())
            {
                Assert.IsTrue(handler.GetSupportedClientActions().Contains(ExecuteDelayedAction.ActionName));

                EventRecorder<DialogActionEventArgs> recorder = new EventRecorder<DialogActionEventArgs>();
                handler.ExecuteActionEvent.Subscribe(recorder.HandleEventAsync);

                ExecuteDelayedAction mockAction = new ExecuteDelayedAction()
                {
                    ActionId = "TestActionId",
                    DelaySeconds = 60,
                    InteractionMethod = InputMethod.Programmatic.ToString()
                };

                await handler.HandleAction(ExecuteDelayedAction.ActionName, JObject.FromObject(mockAction), logger.Clone("ActionHandler"), null, CancellationToken.None, lockStepTime);
                lockStepTime.Step(TimeSpan.FromSeconds(59));
                Assert.IsFalse((await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                lockStepTime.Step(TimeSpan.FromSeconds(2));
                RetrieveResult<CapturedEvent<DialogActionEventArgs>> rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(rr.Success);
                Assert.AreEqual(mockAction.ActionId, rr.Result.Args.ActionId);
                Assert.AreEqual(InputMethod.Programmatic, rr.Result.Args.InteractionMethod);

                handler.ExecuteActionEvent.Unsubscribe(recorder.HandleEventAsync);
            }
        }

        [TestMethod]
        public async Task TestDelayedDialogActionHandlerCancelsPreviousAction()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            using (ExecuteDelayedActionHandler handler = new ExecuteDelayedActionHandler())
            {
                Assert.IsTrue(handler.GetSupportedClientActions().Contains(ExecuteDelayedAction.ActionName));

                EventRecorder<DialogActionEventArgs> recorder = new EventRecorder<DialogActionEventArgs>();
                handler.ExecuteActionEvent.Subscribe(recorder.HandleEventAsync);

                ExecuteDelayedAction mockAction1 = new ExecuteDelayedAction()
                {
                    ActionId = "FirstAction",
                    DelaySeconds = 60,
                    InteractionMethod = InputMethod.Programmatic.ToString()
                };

                await handler.HandleAction(ExecuteDelayedAction.ActionName, JObject.FromObject(mockAction1), logger.Clone("ActionHandler"), null, CancellationToken.None, lockStepTime);
                lockStepTime.Step(TimeSpan.FromSeconds(30));

                ExecuteDelayedAction mockAction2 = new ExecuteDelayedAction()
                {
                    ActionId = "SecondAction",
                    DelaySeconds = 60,
                    InteractionMethod = InputMethod.Programmatic.ToString()
                };

                await handler.HandleAction(ExecuteDelayedAction.ActionName, JObject.FromObject(mockAction2), logger.Clone("ActionHandler"), null, CancellationToken.None, lockStepTime);

                lockStepTime.Step(TimeSpan.FromSeconds(59));
                Assert.IsFalse((await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                lockStepTime.Step(TimeSpan.FromSeconds(2));
                RetrieveResult<CapturedEvent<DialogActionEventArgs>> rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(rr.Success);
                Assert.AreEqual(mockAction2.ActionId, rr.Result.Args.ActionId);
                Assert.AreEqual(InputMethod.Programmatic, rr.Result.Args.InteractionMethod);
                Assert.IsFalse((await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);

                handler.ExecuteActionEvent.Unsubscribe(recorder.HandleEventAsync);
            }
        }
    }
}
