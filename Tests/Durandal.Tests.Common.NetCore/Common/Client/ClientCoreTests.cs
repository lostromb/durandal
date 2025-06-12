

namespace Durandal.Tests.Common.Client
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Client;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.Speech.Triggers;
    using Durandal.Common.Test;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Time;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Tasks;
    using Durandal.Common.Events;
using Durandal.Tests.Common.Client;

    [TestClass]
    public class ClientCoreTests
    {
        private static readonly ILogger _logger = new ConsoleLogger("ClientTests", LogLevel.All);

        /// <summary>
        /// Tests that a client can make a basic text-only request and get a text response back
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestClientText()
        {
            FakeDialogClient dialog = new FakeDialogClient();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger.Clone("ClientCoreTestWrapper"), false, realTime))
            {
                await client.Initialize(dialog);

                dialog.SetResponse(new DialogResponse()
                {
                    ExecutionResult = Result.Success,
                    ResponseText = "Success",
                });

                for (int c = 0; c < 10; c++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeTextRequest("this is a unit test", realTime: realTime));
                    realTime.Step(TimeSpan.FromMilliseconds(100));

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
                    RetrieveResult<CapturedEvent<TextEventArgs>> rr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
                    Assert.IsTrue(rr.Success);
                    Assert.AreEqual("Success", rr.Result.Args.Text);
                }
            }
        }

        /// <summary>
        /// Tests that a client can make a basic audio-only request and get an audio response back
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestClientAudio()
        {
            FakeDialogClient dialog = new FakeDialogClient();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger.Clone("ClientCoreTestWrapper"), true, realTime))
            {
                await client.Initialize(dialog);

                dialog.SetResponse(new DialogResponse()
                {
                    ExecutionResult = Result.Success,
                    ResponseAudio = DialogTestHelpers.GenerateAudioData(AudioSampleFormat.Mono(16000), 2000)
                });

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: realTime));
                AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(AudioSampleFormat.Mono(16000), 3000);
                client.Microphone.AddInput(mockUserSpeech);
                client.SpeechReco.SetRecoResult("en-US", "This is a unit test");

                // Advance enough time to pipe the speech input. The client core test wrapper is fixed at 2 seconds
                realTime.Step(TimeSpan.FromMilliseconds(2000), 100);
                realTime.Step(TimeSpan.FromMilliseconds(100));

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
                Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
            }
        }

        /// <summary>
        /// Tests that a client can make a basic audio-only request and get an audio response back.
        /// This test simulates the user manually terminating the recording via a UI input.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestClientAudioManualButton()
        {
            FakeDialogClient dialog = new FakeDialogClient();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger.Clone("ClientCoreTestWrapper"), true, realTime))
            {
                await client.Initialize(dialog);

                dialog.SetResponse(new DialogResponse()
                {
                    ExecutionResult = Result.Success,
                    ResponseAudio = DialogTestHelpers.GenerateAudioData(AudioSampleFormat.Mono(16000), 2000)
                });

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: realTime));
                AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(AudioSampleFormat.Mono(16000), 3000);
                client.Microphone.AddInput(mockUserSpeech);
                client.SpeechReco.SetRecoResult("en-US", "This is a unit test");

                // Wait for exactly 1 second and then send the "early terminate" signal.
                realTime.Step(TimeSpan.FromMilliseconds(1000), 100);
                await client.Core.ForceRecordingFinish(realTime);

                // Then wait a little time for the results to process
                realTime.Step(TimeSpan.FromMilliseconds(100), 10);

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
                Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
            }
        }

        //[TestMethod]
        //public void TestMockVoiceTrigger()
        //{
        //    IAudioGraph inputAudioGraph = new BasicAudioGraph();
        //    FakeAudioTrigger trigger = new FakeAudioTrigger(inputAudioGraph, AudioSampleFormat.Mono(16000));
        //    IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
        //    KeywordSpottingConfiguration config = new KeywordSpottingConfiguration()
        //    {
        //        PrimaryKeyword = "Durandal",
        //        PrimaryKeywordSensitivity = 0,
        //        SecondaryKeywords = new List<string>(),
        //        SecondaryKeywordSensitivity = 0
        //    };
        //    config.SecondaryKeywords.Add("stop it");
        //    config.SecondaryKeywords.Add("be quiet");

        //    trigger.Configure(config);

        //    AudioSample noise = DialogTestHelpers.GenerateUtterance(AudioSampleFormat.Mono(16000), 1000);
        //    AudioTriggerResult result = trigger.SendAudio(noise, realTime);
        //    Assert.IsFalse(result.Triggered);

        //    AudioSample primary = new AudioSample(trigger.GetPrimaryTrigger(), AudioSampleFormat.Mono(16000));
        //    result = trigger.SendAudio(primary, realTime);
        //    Assert.IsTrue(result.Triggered);
        //    Assert.IsTrue(result.WasPrimaryKeyword);

        //    AudioSample secondary = new AudioSample(trigger.BuildTrigger("stop it"), AudioSampleFormat.Mono(16000));
        //    result = trigger.SendAudio(secondary, realTime);
        //    Assert.IsTrue(result.Triggered);
        //    Assert.IsFalse(result.WasPrimaryKeyword);

        //    secondary = new AudioSample(trigger.BuildTrigger("be quiet"), AudioSampleFormat.Mono(16000));
        //    result = trigger.SendAudio(secondary, realTime);
        //    Assert.IsTrue(result.Triggered);
        //    Assert.IsFalse(result.WasPrimaryKeyword);
        //}

        ///// <summary>
        ///// Tests that a client can make a basic audio-only request and get an audio response back.
        ///// This test simulates the user using a hands-free trigger to initiate the turn
        ///// </summary>
        ///// <returns></returns>
        //[TestMethod]
        //public async Task TestClientAudioByVoiceTrigger()
        //{
        //    FakeDialogClient dialog = new FakeDialogClient();
        //    LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
        //    using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger.Clone("ClientCoreTestWrapper"), true, realTime))
        //    {
        //        await client.Initialize(dialog);

        //        dialog.SetResponse(new DialogResponse()
        //        {
        //            ExecutionResult = Result.Success,
        //            ResponseAudio = DialogTestHelpers.GenerateAudioData(AudioSampleFormat.Mono(16000), 2000)
        //        });

        //        AudioSample triggerPhrase = new AudioSample(client.Trigger.GetPrimaryTrigger(), AudioSampleFormat.Mono(16000));
        //        AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(AudioSampleFormat.Mono(16000), 3000);

        //        realTime.Step(TimeSpan.FromMilliseconds(10000));
        //        client.ResetEvents();

        //        // Send the trigger audio and wait 50 ms for activation
        //        client.Microphone.AddInput(triggerPhrase);
        //        realTime.Step(TimeSpan.FromMilliseconds(50), 10);

        //        Assert.IsTrue((await client.SpeechTriggerEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);

        //        client.Microphone.AddInput(mockUserSpeech);
        //        client.SpeechReco.SetRecoResult("en-US", "This is a unit test");

        //        realTime.Step(TimeSpan.FromMilliseconds(3000), 1000);
        //        // Then wait a little time for the results to process
        //        realTime.Step(TimeSpan.FromMilliseconds(4000), 100);

        //        // Ensure that the core emitted the proper events
        //        Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
        //        Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
        //    }
        //}

        /// <summary>
        /// Test that on multiturn, the client will give another prompt and record a second turn of the conversation
        /// </summary>
        /// <returns></returns>
        [Ignore] // Client async audio is still broken
        [TestMethod]
        public async Task TestClientAudioMultiTurnBasic()
        {
            FakeDialogClient dialog = new FakeDialogClient();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger.Clone("ClientCoreTestWrapper"), true, realTime))
            {
                await client.Initialize(dialog);

                AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(AudioSampleFormat.Mono(16000), 4000);

                dialog.SetResponse(new DialogResponse()
                {
                    ExecutionResult = Result.Success,
                    ResponseText = "Turn1",
                    ContinueImmediately = true,
                    SuggestedRetryDelay = 1500
                });

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: realTime));

                Assert.IsTrue((await client.AudioPromptEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);

                client.Microphone.AddInput(mockUserSpeech);
                client.SpeechReco.SetRecoResult("en-US", "This is a unit test");

                // Wait for exactly 1 second and then send the "early terminate" signal.
                realTime.Step(TimeSpan.FromMilliseconds(1000), 100);
                await client.Core.ForceRecordingFinish(realTime);

                _logger.Log("1");
                // Then wait a little time for the results to process
                realTime.Step(TimeSpan.FromMilliseconds(100), 10);

                _logger.Log("2");

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
                RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
                Assert.IsTrue(textRr.Success);
                Assert.AreEqual("Turn1", textRr.Result.Args.Text);

                dialog.SetResponse(new DialogResponse()
                {
                    ExecutionResult = Result.Success,
                    ResponseText = "Turn2",
                });

                client.ResetEvents();

                // Now wait for the delay of 1500 milliseconds to pass
                realTime.Step(TimeSpan.FromMilliseconds(1300), 100);
                Assert.IsFalse((await client.AudioPromptEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                realTime.Step(TimeSpan.FromMilliseconds(250), 10);
                Assert.IsTrue((await client.AudioPromptEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);

                client.Microphone.AddInput(mockUserSpeech);
                realTime.Step(TimeSpan.FromMilliseconds(1000), 100);

                await client.Core.ForceRecordingFinish(realTime);
                _logger.Log("3");
                realTime.Step(TimeSpan.FromMilliseconds(100), 10);

                _logger.Log("4");
                // Turn 2 should have processed now
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
                textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
                Assert.IsTrue(textRr.Success);
                Assert.AreEqual("Turn2", textRr.Result.Args.Text);
            }
        }

        ///// <summary>
        ///// Test that when dialog sends instant keywords, they can be used to trigger a second turn without
        ///// a separate recording period
        ///// </summary>
        ///// <returns></returns>
        //[TestMethod]
        //public async Task TestClientAudioSecondaryTriggers()
        //{
        //    FakeDialogClient dialog = new FakeDialogClient();
        //    LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
        //    using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger.Clone("ClientCoreTestWrapper"), true, realTime))
        //    {
        //        await client.Initialize(dialog);

        //        AudioSample triggerPhrase = new AudioSample(client.Trigger.GetPrimaryTrigger(), AudioSampleFormat.Mono(16000));
        //        AudioSample secondTriggerPhrase = new AudioSample(client.Trigger.BuildTrigger("turn2"), AudioSampleFormat.Mono(16000));
        //        AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(AudioSampleFormat.Mono(16000), 3000);

        //        client.ResetEvents();

        //        List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
        //        spotterPhrases.Add(new TriggerKeyword()
        //        {
        //            TriggerPhrase = "turn2",
        //            AllowBargeIn = true,
        //            ExpireTimeSeconds = 15
        //        });

        //        dialog.SetResponse(new DialogResponse()
        //        {
        //            ExecutionResult = Result.Success,
        //            TriggerKeywords = spotterPhrases
        //        });

        //        // Send the trigger audio and wait 50 ms for activation
        //        client.Microphone.AddInput(triggerPhrase);
        //        realTime.Step(TimeSpan.FromMilliseconds(50), 10);

        //        Assert.IsTrue((await client.SpeechTriggerEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);

        //        client.Microphone.AddInput(mockUserSpeech);
        //        client.SpeechReco.SetRecoResult("en-US", "This is a unit test");

        //        realTime.Step(TimeSpan.FromMilliseconds(3000));
        //        // Then wait a little time for the results to process
        //        realTime.Step(TimeSpan.FromMilliseconds(2000), 100);

        //        // Ensure that the core emitted the proper events
        //        Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);

        //        dialog.SetResponse(new DialogResponse()
        //        {
        //            ExecutionResult = Result.Success,
        //            ResponseText = "Turn2"
        //        });

        //        // Now say the secondary trigger
        //        client.ResetEvents();
        //        client.Microphone.AddInput(secondTriggerPhrase);
        //        realTime.Step(TimeSpan.FromMilliseconds(50), 10);
        //        Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);
        //        RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
        //        Assert.IsTrue(textRr.Success);
        //        Assert.AreEqual("Turn2", textRr.Result.Args.Text);
        //    }
        //}

        ///// <summary>
        ///// Tests that secondary triggers can expire and will not trigger a second turn afterwards
        ///// </summary>
        ///// <returns></returns>
        //[TestMethod]
        //public async Task TestClientAudioSecondaryTriggerExpiration()
        //{
        //    FakeDialogClient dialog = new FakeDialogClient();
        //    LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
        //    using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger.Clone("ClientCoreTestWrapper"), true, realTime))
        //    {
        //        await client.Initialize(dialog);

        //        AudioSample triggerPhrase = new AudioSample(client.Trigger.GetPrimaryTrigger(), AudioSampleFormat.Mono(16000));
        //        AudioSample secondTriggerPhrase = new AudioSample(client.Trigger.BuildTrigger("turn2"), AudioSampleFormat.Mono(16000));
        //        AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(AudioSampleFormat.Mono(16000), 3000);

        //        List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
        //        spotterPhrases.Add(new TriggerKeyword()
        //        {
        //            TriggerPhrase = "turn2",
        //            AllowBargeIn = false,
        //            ExpireTimeSeconds = 5
        //        });

        //        dialog.SetResponse(new DialogResponse()
        //        {
        //            ExecutionResult = Result.Success,
        //            TriggerKeywords = spotterPhrases
        //        });

        //        // Send the trigger audio and wait 50 ms for activation
        //        client.Microphone.AddInput(triggerPhrase);
        //        realTime.Step(TimeSpan.FromMilliseconds(50), 10);

        //        Assert.IsTrue((await client.SpeechTriggerEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);

        //        client.Microphone.AddInput(mockUserSpeech);
        //        client.SpeechReco.SetRecoResult("en-US", "This is a unit test");

        //        realTime.Step(TimeSpan.FromMilliseconds(10000));
        //        // Then wait a little time for the results to process
        //        realTime.Step(TimeSpan.FromMilliseconds(2000), 100);

        //        // Ensure that the core emitted the proper events
        //        Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1))).Success);

        //        // Wait 5 seconds and say the secondary trigger. It should have expired by now
        //        realTime.Step(TimeSpan.FromMilliseconds(5000));

        //        client.ResetEvents();
        //        client.Microphone.AddInput(secondTriggerPhrase);
        //        realTime.Step(TimeSpan.FromMilliseconds(50), 10);
        //        Assert.IsFalse((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
        //        Assert.IsFalse((await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
        //    }
        //}
    }
}
