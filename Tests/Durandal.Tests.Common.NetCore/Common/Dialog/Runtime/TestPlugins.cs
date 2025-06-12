
namespace Durandal.Tests.Common.Dialog.Runtime
{
    using Durandal.API;
        using Durandal.Common.Audio;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Ontology;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Test;
    using Durandal.Common.Utils;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Statistics;
    using Durandal.Tests.Common.Audio;
    using Durandal.Tests.Common.Dialog.Runtime;
    using Durandal.Tests.EntitySchemas;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    public class BasicAnswer : DurandalPlugin
    {
        public BasicAnswer() : base("basic") { }

        public Trigger SucceedTrigger = new Trigger();
        public Trigger SkipTrigger = new Trigger();
        public Trigger FailTrigger = new Trigger();

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            switch (queryWithContext.Understanding.Intent)
            {
                case "succeed":
                    SucceedTrigger.Trip();
                    return new PluginResult(Result.Success);
                case "skip":
                    SkipTrigger.Trip();
                    return new PluginResult(Result.Skip);
                case "skipwithmsg":
                    SkipTrigger.Trip();
                    return new PluginResult(Result.Skip)
                    {
                        ResponseText = "This plugin has been skipped"
                    };
                case "skipwithmsgbad":
                    SkipTrigger.Trip();
                    return new PluginResult(Result.Skip)
                    {
                        ResponseText = "This plugin has been skipped but you shouldn't see this"
                    };
                case "skipwithmsgmultiturn":
                    SkipTrigger.Trip();
                    int state;
                    if (services.SessionStore.TryGetInt("state", out state))
                    {
                        state++;
                        services.SessionStore.Put("state", state);
                        return new PluginResult(Result.Skip)
                        {
                            ResponseText = state.ToString(),
                            MultiTurnResult = MultiTurnBehavior.ContinuePassively
                        };
                    }
                    services.SessionStore.Put("state", 0);
                    return new PluginResult(Result.Skip)
                    {
                        ResponseText = "0",
                        MultiTurnResult = MultiTurnBehavior.ContinuePassively
                    };

                case "fail":
                    FailTrigger.Trip();
                    return new PluginResult(Result.Failure);
                case "originalquery":
                    if (queryWithContext.OriginalSpeechInput != null &&
                        queryWithContext.OriginalSpeechInput.RecognizedPhrases.Any((p) => string.Equals(p.DisplayText, "the original query is here")))
                    {
                        return new PluginResult(Result.Success);
                    }
                    else
                    {
                        return new PluginResult(Result.Failure);
                    }
                case "emptyuserprofile":
                    if (services.LocalUserProfile.Count > 0)
                    {
                        return new PluginResult(Result.Failure);
                    }
                    return new PluginResult(Result.Success);
                case "bad_ssml":
                    return new PluginResult(Result.Success)
                    {
                        ResponseSsml = "<speak>I don't escape my XML characters & that's fine with me</speak>"
                    };
            }
            throw new InvalidOperationException("wrong intent");
        }

        public void Reset()
        {
            SucceedTrigger.Reset();
            SkipTrigger.Reset();
            FailTrigger.Reset();
        }
    }

    public class SideSpeechAnswer : DurandalPlugin
    {
        public SideSpeechAnswer() : base(DialogConstants.SIDE_SPEECH_DOMAIN) { }

        public Trigger SucceedTrigger = new Trigger();
        public Trigger SucceedHighTrigger = new Trigger();

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode sideSpeechMultiturnNodeHigh = tree.CreateNode(SideSpeechHigh);
            IConversationNode sideSpeechMultiturnNodeLow = tree.CreateNode(SideSpeechLow);

            sideSpeechMultiturnNodeHigh.CreateNormalEdge(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, sideSpeechMultiturnNodeHigh);
            sideSpeechMultiturnNodeHigh.CreateNormalEdge(DialogConstants.SIDE_SPEECH_INTENT, sideSpeechMultiturnNodeLow);
            sideSpeechMultiturnNodeLow.CreateNormalEdge(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, sideSpeechMultiturnNodeHigh);
            sideSpeechMultiturnNodeLow.CreateNormalEdge(DialogConstants.SIDE_SPEECH_INTENT, sideSpeechMultiturnNodeLow);

            tree.AddStartState(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, sideSpeechMultiturnNodeHigh);
            tree.AddStartState(DialogConstants.SIDE_SPEECH_INTENT, sideSpeechMultiturnNodeLow);

            return tree;
        }

        public async Task<PluginResult> SideSpeechHigh(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            SucceedHighTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                ResponseText = "Side speech highconf triggered",
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> SideSpeechLow(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            SucceedTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                ResponseText = "Side speech lowconf triggered",
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public void Reset()
        {
            SucceedTrigger.Reset();
            SucceedHighTrigger.Reset();
        }
    }

    public class SandboxAnswer : DurandalPlugin
    {
        public SandboxAnswer() : base("sandbox") { }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;

            if (queryWithContext.Understanding.Intent.Equals("fail"))
            {
                return new PluginResult(Result.Failure);
            }
            if (queryWithContext.Understanding.Intent.Equals("timeout"))
            {
                Thread.Sleep(300000);
            }
            if (queryWithContext.Understanding.Intent.Equals("timeout_small"))
            {
                Thread.Sleep(2000);
            }
            if (queryWithContext.Understanding.Intent.Equals("exception"))
            {
                throw new NullReferenceException("Test exception being thrown from a user plugin");
            }

            return new PluginResult(Result.Success);
        }
    }

    public static class StaticAnswerModule
    {
        public static async Task<PluginResult> Run(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                ResponseText = "Static answer"
            };
        }
    }

    public class BasicTreeAnswer : DurandalPlugin
    {
        public BasicTreeAnswer() : base("basictree") { }

        public Trigger StartTrigger = new Trigger();
        public Trigger StartLockTrigger = new Trigger();
        public Trigger StartTenativeTrigger = new Trigger();
        public Trigger StartRetryTrigger = new Trigger();
        public Trigger Turn2Trigger = new Trigger();
        public Trigger ConfirmTrigger = new Trigger();
        public Trigger SideSpeechTrigger = new Trigger();
        public Trigger NoRecoTrigger = new Trigger();

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode startNode = tree.CreateNode(StartNode);
            IConversationNode startTenativeNode = tree.CreateNode(StartTenativeNode);
            IConversationNode turn2Node = tree.CreateNode(Turn2Node);
            IConversationNode turn2PromiscNode = tree.CreateNode(Turn2PromiscNode);
            IConversationNode startObjectStoreNode = tree.CreateNode(StartObjectStoreNode);
            IConversationNode turn2ObjectStoreNode = tree.CreateNode(Turn2ObjectStoreNode);
            IConversationNode confirmNode = tree.CreateNode(ConfirmNode);
            IConversationNode sideSpeechNode = tree.CreateNode(SideSpeechNode);
            IConversationNode noRecoNode = tree.CreateNode(NoRecoNode);
            IConversationNode startLockNode = tree.CreateNode(StartLockNode);
            IConversationNode startSideSpeechNode = tree.CreateNode(StartSideSpeechNode);
            IConversationNode startDialogActionNode = tree.CreateNode(StartDialogActionNode);
            IConversationNode startDialogActionTactileNode = tree.CreateNode(StartDialogActionTactileNode);
            IConversationNode fallbackNode = tree.CreateNode(FallbackNode);
            IConversationNode loopNode = tree.CreateNode(LoopNode);
            IConversationNode loop2Node = tree.CreateNode(Loop2Node);
            IConversationNode turn2TactileNode = tree.CreateNode(Turn2TactileNode);

            tree.AddStartState("start", startNode);
            tree.AddStartState("start_tenative", startTenativeNode);
            tree.AddStartState("start_lock", startLockNode);
            tree.AddStartState("start_sidespeech", startSideSpeechNode);
            tree.AddStartState("start_objectstore", startObjectStoreNode);
            tree.AddStartState("start_dialogaction", startDialogActionNode);
            tree.AddStartState("start_dialogactiontactile", startDialogActionTactileNode);
            tree.AddStartState("start_continuations", ExplicitContinuationNode1);
            tree.AddStartState("start_userprofile_1", UserProfile1);
            tree.AddStartState("start_userprofile_2", UserProfile2);
            tree.AddStartState("start_externalmodule", StaticAnswerModule.Run);

            startNode.CreateNormalEdge("turn2", turn2Node);
            startNode.CreateCommonEdge("confirm", confirmNode);
            startNode.CreateCommonEdge("side_speech", sideSpeechNode);
            startNode.CreateCommonEdge("noreco", noRecoNode);
            startNode.CreateNormalEdge("turn2_promisc", turn2PromiscNode);
            startNode.CreateNormalEdge("loop", loopNode);

            startLockNode.CreateNormalEdge("turn2", turn2Node);

            startDialogActionNode.CreateNormalEdge("turn2", turn2Node);
            startDialogActionTactileNode.CreateNormalEdge("turn2", turn2TactileNode);

            startTenativeNode.CreateNormalEdge("turn2", turn2Node);
            startTenativeNode.CreateNormalEdge("loop2", loop2Node);

            startSideSpeechNode.CreateCommonEdge("side_speech", sideSpeechNode);
            startSideSpeechNode.CreateNormalEdge("turn2", turn2Node);

            startObjectStoreNode.CreateNormalEdge("turn2", turn2ObjectStoreNode);

            turn2PromiscNode.CreatePromiscuousEdge(fallbackNode);

            loopNode.CreateNormalEdge("loop", loopNode);

            loop2Node.CreateNormalEdge("loop2", loop2Node);

            tree.AddStartState("side_speech", startNode);

            return tree;
        }

        public async Task<PluginResult> StartNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            StartTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> StartLockNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            StartLockTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> StartObjectStoreNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // verify nothing is in the store to begin with
            string val;
            if (services.SessionStore.TryGetString("key", out val))
            {
                return new PluginResult(Result.Failure);
            }

            services.SessionStore.Put("key", "value");
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> StartDialogActionNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                ResponseText = "hi"
            };

            returnVal.ResponseData["actionKey"] = services.RegisterDialogAction(new DialogAction() { Domain = this.LUDomain, Intent = "turn2" });
            returnVal.ResponseData["actionUrl"] = services.RegisterDialogActionUrl(new DialogAction() { Domain = this.LUDomain, Intent = "turn2" }, queryWithContext.ClientContext.ClientId);

            return returnVal;
        }

        public async Task<PluginResult> StartDialogActionTactileNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Determine client's input method
            if (queryWithContext.Source == InputMethod.Typed)
            {
                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ResponseText = "hi"
                };

                returnVal.ResponseData["actionKey"] = services.RegisterDialogAction(new DialogAction() { Domain = this.LUDomain, Intent = "turn2", InteractionMethod = InputMethod.Tactile });

                return returnVal;
            }
            else if (queryWithContext.Source == InputMethod.Spoken)
            {
                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ResponseText = "hi"
                };

                returnVal.ResponseData["actionKey"] = services.RegisterDialogAction(new DialogAction() { Domain = this.LUDomain, Intent = "turn2", InteractionMethod = InputMethod.TactileWithAudio });

                return returnVal;
            }
            else
            {
                return new PluginResult(Result.Failure);
            }
        }

        public async Task<PluginResult> Turn2TactileNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Return a lot of response data
            return new PluginResult(Result.Success)
            {
                ResponseHtml = "This is some HTML",
                ResponseText = "This is some text",
                ResponseSsml = "This is some speech"
            };
        }

        public async Task<PluginResult> StartTenativeNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            StartTenativeTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
        }

        public async Task<PluginResult> StartSideSpeechNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            StartTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> StartRetryNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            StartRetryTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> ConfirmNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            ConfirmTrigger.Trip();
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> FallbackNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> Turn2Node(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            Turn2Trigger.Trip();
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> Turn2PromiscNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            Turn2Trigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> Turn2ObjectStoreNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            string val;
            if (services.SessionStore.TryGetString("key", out val) && val.Equals("value"))
            {
                return new PluginResult(Result.Success);
            }

            return new PluginResult(Result.Failure);
        }

        public async Task<PluginResult> SideSpeechNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            SideSpeechTrigger.Trip();
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> NoRecoNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            NoRecoTrigger.Trip();
            return new PluginResult(Result.Success);
        }

        /// <summary>
        /// this is used to test conversation history ordering and pruning logic
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <returns></returns>
        public async Task<PluginResult> LoopNode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Validate the session context coming in
            int turnNum = queryWithContext.TurnNum;
            int pastTurnsCount = queryWithContext.PastTurns.Count;
            const int PRUNING_SESSION_LENGTH = 10; // this has to match ConversationStateInternal.CONVERSATION_HISTORY_PRUNING_LIMIT
            bool passed = pastTurnsCount == Math.Min(PRUNING_SESSION_LENGTH, turnNum);

            // Make sure each past turn is in the proper order
            int turnCounter = turnNum - pastTurnsCount;
            for (int c = 0; c < pastTurnsCount; c++)
            {
                passed = passed && queryWithContext.PastTurns[c].Utterance.OriginalText.Equals(turnCounter.ToString());
                turnCounter++;
            }

            if (passed)
            {
                return new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            }
            else
            {
                return new PluginResult(Result.Failure);
            }
        }

        public async Task<PluginResult> Loop2Node(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> ExplicitContinuationNode1(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            if (queryWithContext.Understanding.Utterance.OriginalText.Equals("static"))
            {
                return new PluginResult(Result.Success)
                {
                    Continuation = ExplicitContinuationNode2Static,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            }

            if (queryWithContext.Understanding.Utterance.OriginalText.Equals("lambda"))
            {
                return new PluginResult(Result.Success)
                {
                    Continuation = async (q, s) =>
                    {
                        await DurandalTaskExtensions.NoOpTask;
                        return new PluginResult(Result.Success);
                    },
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            }

            if (queryWithContext.Understanding.Utterance.OriginalText.Equals("private"))
            {
                return new PluginResult(Result.Success)
                {
                    Continuation = ExplicitContinuationNode2Private,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            }

            return new PluginResult(Result.Success)
            {
                Continuation = ExplicitContinuationNode2,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> ExplicitContinuationNode2(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success);
        }

        public static async Task<PluginResult> ExplicitContinuationNode2Static(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success);
        }

        private async Task<PluginResult> ExplicitContinuationNode2Private(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> UserProfile1(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            services.LocalUserProfile.ClearAll();
            services.LocalUserProfile.Put("string", "value");
            services.LocalUserProfile.Put("int", 3);

            try
            {
                // Global profile should be read-only
                services.GlobalUserProfile.Put("Invalid", false);
                return new PluginResult(Result.Failure);
            }
            catch (InvalidOperationException) { }

            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> UserProfile2(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            if (queryWithContext.TurnNum != 0)
            {
                services.Logger.Log("Turn num must be 0");
                return new PluginResult(Result.Failure);
            }

            if (services.LocalUserProfile.ContainsKey("string") &&
                services.LocalUserProfile.GetString("string").Equals("value") &&
                services.LocalUserProfile.ContainsKey("int") &&
                services.LocalUserProfile.GetInt("int") == 3)
            {
                return new PluginResult(Result.Success);
            }

            services.Logger.Log("User profile did not contain expected keys");
            return new PluginResult(Result.Failure);
        }

        public void Reset()
        {
            StartTrigger.Reset();
            StartTenativeTrigger.Reset();
            ConfirmTrigger.Reset();
            Turn2Trigger.Reset();
            SideSpeechTrigger.Reset();
            NoRecoTrigger.Reset();
            StartLockTrigger.Reset();
            StartRetryTrigger.Reset();
        }
    }

    public class RetryTreeAnswer : DurandalPlugin
    {
        public RetryTreeAnswer() : base("retrytree") { }

        public Trigger StartSucceedTrigger = new Trigger();
        public Trigger StartSkipTrigger = new Trigger();
        public Trigger StartFailTrigger = new Trigger();
        public Trigger RetrySucceedTrigger = new Trigger();
        public Trigger RetrySkipTrigger = new Trigger();
        public Trigger RetryFailTrigger = new Trigger();
        public Trigger Turn2Trigger = new Trigger();

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode startSucceedNode = tree.CreateNode(StartSucceed);
            IConversationNode startSkipNode = tree.CreateNode(StartSkip);
            IConversationNode startFailNode = tree.CreateNode(StartFail);
            IConversationNode turn2Node = tree.CreateNode(Turn2);

            tree.AddStartState("start_succeed", startSucceedNode);
            tree.AddStartState("start_skip", startSkipNode);
            tree.AddStartState("start_fail", startFailNode);

            startSucceedNode.CreateNormalEdge("turn2", turn2Node);
            startSkipNode.CreateNormalEdge("turn2", turn2Node);
            startFailNode.CreateNormalEdge("turn2", turn2Node);

            startSucceedNode.EnableRetry(RetrySucceed);
            startSkipNode.EnableRetry(RetrySkip);
            startFailNode.EnableRetry(RetryFail);

            return tree;
        }

        public async Task<PluginResult> StartSucceed(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            StartSucceedTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> StartSkip(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            StartSkipTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> StartFail(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            StartFailTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> Turn2(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            Turn2Trigger.Trip();
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> RetrySucceed(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            RetrySucceedTrigger.Trip();
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> RetrySkip(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            RetrySkipTrigger.Trip();
            return new PluginResult(Result.Skip)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> RetryFail(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            RetryFailTrigger.Trip();
            return new PluginResult(Result.Failure)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public void Reset()
        {
            StartSucceedTrigger.Reset();
            StartFailTrigger.Reset();
            StartSkipTrigger.Reset();
            RetrySucceedTrigger.Reset();
            RetrySkipTrigger.Reset();
            RetryFailTrigger.Reset();
            Turn2Trigger.Reset();
        }
    }

    public class ClientCapsAnswer : DurandalPlugin
    {
        public ClientCapsAnswer() : base("clientcaps") { }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            string responseText = "This is sample text";
            AudioResponse responseAudio = new AudioResponse(
                new byte[5000],
                RawPcmCodecFactory.CODEC_NAME_PCM_S16LE,
                CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Mono(16000)));
            string responseHtml = "<html><body>This is a sample HTML page</body></html>";
            string ResponseSSML = "<speak>This is sample SSML</speak>";
            string responseUrl = "http://www.com";

            switch (queryWithContext.Understanding.Intent)
            {
                case "url":
                    // Return a URL but no HTML
                    return new PluginResult(Result.Success)
                    {
                        ResponseSsml = ResponseSSML,
                        ResponseText = responseText,
                        ResponseUrl = responseUrl
                    };
                case "html":
                    // Return HTML but no URL
                    return new PluginResult(Result.Success)
                    {
                        ResponseHtml = responseHtml,
                        ResponseSsml = ResponseSSML,
                        ResponseText = responseText
                    };
                case "customaudio":
                    // Return a full result with custom audio
                    return new PluginResult(Result.Success)
                    {
                        ResponseHtml = responseHtml,
                        ResponseSsml = ResponseSSML,
                        ResponseText = responseText,
                        ResponseAudio = responseAudio
                    };
                case "textonly":
                    // Return text only
                    return new PluginResult(Result.Success)
                    {
                        ResponseText = responseText
                    };
                case "textssml":
                    // Return text only
                    return new PluginResult(Result.Success)
                    {
                        ResponseText = responseText,
                        ResponseSsml = ResponseSSML
                    };
                case "audioonly":
                    // Return audio only
                    return new PluginResult(Result.Success)
                    {
                        ResponseAudio = responseAudio,
                    };
                case "compressedaudio":
                    // Return audio only and also it is compressed
                    float[] sampleData = new float[44100];
                    AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100);
                    AudioSample inputSample = new AudioSample(sampleData, AudioSampleFormat.Mono(44100));
                    AudioData data = await AudioHelpers.EncodeAudioSampleUsingCodec(inputSample, new SquareDeltaCodecFactory(), SquareDeltaCodecFactory.CODEC_NAME, services.Logger).ConfigureAwait(false);
                    return new PluginResult(Result.Success)
                    {
                        ResponseAudio = new AudioResponse(data),
                    };
                case "cache":
                    // Throw some data into the temporary cache
                    string cacheUrl = services.CreateTemporaryWebResource(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Cached data")), "text/plain; charset=utf-8");
                    return new PluginResult(Result.Success)
                    {
                        ResponseUrl = cacheUrl
                    };
                default:
                    // Return everything
                    return new PluginResult(Result.Success)
                    {
                        ResponseHtml = responseHtml,
                        ResponseSsml = ResponseSSML,
                        ResponseText = responseText,
                        ResponseUrl = responseUrl
                    };
            }
        }
    }

    public class CrossDomainAnswer : DurandalPlugin
    {
        public CrossDomainAnswer(string realDomain) : base(realDomain) { }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode startNode = tree.CreateNode(Start);

            startNode.CreateExternalEdge("basic_a", "crossdomain_b", "basic_b");
            startNode.CreateExternalEdge("params_a", "crossdomain_b", "params_b");
            startNode.CreateExternalEdge("slotparams_a", "crossdomain_b", "slotparams_b");
            startNode.CreateExternalEdge("unsupported_request_a", "crossdomain_b", "unsupported_request_b");
            startNode.CreateExternalEdge("unsupported_response_a", "crossdomain_b", "unsupported_response_b");
            startNode.CreateExternalEdge("no_target_intent", "crossdomain_b", "fartmaster");
            startNode.CreateExternalEdge("sessionstore_a", "crossdomain_b", "sessionstore_b");
            startNode.CreateExternalEdge("entities_a", "crossdomain_b", "entities_b");

            // On the "a" side
            tree.AddStartState("start", startNode);

            // On the "b" side
            tree.AddStartState("params_b", ParamsEntryPointB);
            tree.AddStartState("slotparams_b", SlotParamsEntryPointB);
            tree.AddStartState("basic_b", BasicEntryPointB);
            tree.AddStartState("sessionstore_b", SessionStoreEntryPointB);
            tree.AddStartState("entities_b", EntitiesEntryPointB);

            return tree;
        }

        public async Task<PluginResult> Start(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            services.SessionStore.Put("slot", "test");
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public override async Task<CrossDomainRequestData> CrossDomainRequest(string targetIntent)
        {
            await DurandalTaskExtensions.NoOpTask;
            CrossDomainRequestData returnVal = new CrossDomainRequestData();

            if (targetIntent.Equals("basic_b"))
            {
                return returnVal;
            }
            if (targetIntent.Equals("params_b") || targetIntent.Equals("slotparams_b"))
            {
                returnVal.RequestedSlots.Add(new CrossDomainSlot("location", true));
                return returnVal;
            }
            if (targetIntent.Equals("unsupported_response_b"))
            {
                return returnVal;
            }
            if (targetIntent.Equals("fartmaster"))
            {
                return returnVal;
            }
            if (targetIntent.Equals("sessionstore_b"))
            {
                returnVal.RequestedSlots.Add(new CrossDomainSlot("slot", true));
                return returnVal;
            }
            if (targetIntent.Equals("entities_b"))
            {
                returnVal.RequestedSlots.Add(new CrossDomainSlot("location", true, "http://schema.org/Place"));
                return returnVal;
            }

            // Unsupported request
            return null;
        }

        public override async Task<CrossDomainResponseData> CrossDomainResponse(CrossDomainContext context, IPluginServices pluginServices)
        {
            await DurandalTaskExtensions.NoOpTask;
            CrossDomainResponseData returnVal = new CrossDomainResponseData();
            if (context.RequestIntent.Equals("basic_b"))
            {
                return returnVal;
            }
            if (context.RequestIntent.Equals("fartmaster"))
            {
                return returnVal;
            }
            if (context.RequestIntent.Equals("params_b"))
            {
                if (context.RequestedSlots.Count > 0 && context.RequestedSlots.FirstOrDefault().SlotName.Equals("location"))
                {
                    SlotValue responseSlot = new SlotValue("location", "my house", SlotValueFormat.CrossDomainTag);
                    Place location = new Place(pluginServices.EntityContext);
                    location.Name.Value = "my house";
                    responseSlot.AddEntity(new Hypothesis<Entity>(location, 1.0f));
                    returnVal.FilledSlots.Add(responseSlot);
                    return returnVal;
                }
            }
            if (context.RequestIntent.Equals("slotparams_b"))
            {
                SlotValue currentTurnSlot = DialogHelpers.TryGetAnySlot(context.PastConversationTurns, "param1");
                if (currentTurnSlot == null)
                {
                    return null;
                }

                currentTurnSlot.Name = "location";
                returnVal.FilledSlots.Add(currentTurnSlot);
                return returnVal;
            }
            if (context.RequestIntent.Equals("sessionstore_b"))
            {
                returnVal.FilledSlots.Add(new SlotValue("slot", pluginServices.SessionStore.GetString("slot"), SlotValueFormat.CrossDomainTag));
                return returnVal;
            }
            if (context.RequestIntent.Equals("entities_b"))
            {
                Place entity = new Place(pluginServices.EntityContext);
                entity.Name.Value = "Issaquah, WA";
                SlotValue slot = new SlotValue("location", "issaquah", SlotValueFormat.CrossDomainTag);
                slot.AddEntity(new Hypothesis<Entity>(entity, 1.0f));
                returnVal.FilledSlots.Add(slot);
                return returnVal;
            }

            // Unsupported response
            return null;
        }

        public async Task<PluginResult> ParamsEntryPointB(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Validate the input
            if (queryWithContext.TurnNum != 0 || queryWithContext.PastTurns.Count > 0)
            {
                return new PluginResult(Result.Failure);
            }

            TaggedData tags = queryWithContext.Understanding.MostLikelyTags;
            if (tags == null)
            {
                return new PluginResult(Result.Failure);
            }

            SlotValue slot = DialogHelpers.TryGetSlot(tags, "location");
            if (slot == null)
            {
                return new PluginResult(Result.Failure);
            }

            IList<ContextualEntity> locations = slot.GetEntities(services.EntityContext);
            if (locations.Count == 0 || !locations[0].Entity.IsA<Place>() || !locations[0].Entity.As<Place>().Name.Value.Equals("my house"))
            {
                return new PluginResult(Result.Failure);
            }

            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> SlotParamsEntryPointB(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Validate the input
            if (queryWithContext.TurnNum != 0 || queryWithContext.PastTurns.Count > 0)
            {
                return new PluginResult(Result.Failure);
            }

            TaggedData tags = queryWithContext.Understanding.MostLikelyTags;
            if (tags == null)
            {
                return new PluginResult(Result.Failure);
            }

            SlotValue slot = DialogHelpers.TryGetSlot(tags, "location");
            if (slot == null)
            {
                return new PluginResult(Result.Failure);
            }

            if (!slot.Value.Equals("value1"))
            {
                return new PluginResult(Result.Failure);
            }

            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> BasicEntryPointB(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // We should be virtually starting in a new conversation with no apparent context (since no params were passed in)
            if (queryWithContext.TurnNum != 0 || queryWithContext.PastTurns.Count > 0)
            {
                return new PluginResult(Result.Failure);
            }

            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> SessionStoreEntryPointB(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Validate the input
            if (queryWithContext.TurnNum != 0 || queryWithContext.PastTurns.Count > 0)
            {
                return new PluginResult(Result.Failure);
            }

            TaggedData tags = queryWithContext.Understanding.MostLikelyTags;
            if (tags == null)
            {
                return new PluginResult(Result.Failure);
            }

            SlotValue slot = DialogHelpers.TryGetSlot(tags, "slot");
            if (slot == null)
            {
                return new PluginResult(Result.Failure);
            }

            if (!slot.Value.Equals("test"))
            {
                return new PluginResult(Result.Failure);
            }

            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> EntitiesEntryPointB(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            SlotValue slot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "location");
            if (slot == null)
            {
                return new PluginResult(Result.Failure);
            }

            IList<ContextualEntity> entities = slot.GetEntities(services.EntityContext);
            if (entities.Count == 0)
            {
                return new PluginResult(Result.Failure);
            }

            Place e = entities[0].Entity.As<Place>();
            if (!string.Equals(e.Name.Value, "Issaquah, WA"))
            {
                return new PluginResult(Result.Failure);
            }

            return new PluginResult(Result.Success);
        }
    }

    public class CrossDomainSuperAnswer : DurandalPlugin
    {
        public CrossDomainSuperAnswer() : base("cd_super") { }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode startNode = tree.CreateNode(Start);
            IConversationNode callbackNode = tree.CreateNode(CallbackOne);
            IConversationNode callbackTenativeNode = tree.CreateNode(CallbackOneTenative);
            IConversationNode callbackNode2 = tree.CreateNode(CallbackTwo);
            IConversationNode callbackNode2WithParams = tree.CreateNode(CallbackTwoWithParameters);
            callbackNode.CreateNormalEdge("carry_on", callbackNode2);
            callbackTenativeNode.CreateNormalEdge("carry_on", callbackNode2);
            callbackNode.CreateNormalEdge("callback_intent_a", callbackNode2WithParams);

            startNode.CreateExternalEdge("continue_a", "cd_sub", "continue_b");
            startNode.CreateExternalEdge("tenative_a", "cd_sub", "tenative_b");
            startNode.CreateExternalEdge("cancel_a", "cd_sub", "cancel_b");
            startNode.CreateExternalEdge("continue_callback_a", "cd_sub", "continue_b", callbackNode);
            startNode.CreateExternalEdge("tenative_callback_a", "cd_sub", "continue_b", callbackTenativeNode);
            startNode.CreateExternalEdge("cancel_callback_a", "cd_sub", "cancel_b", callbackNode);
            startNode.CreateExternalEdge("call_b_with_callback", "cd_sub", "callback_1", callbackNode);
            startNode.CreateExternalEdge("call_b_with_callback_oneshot", "cd_sub", "callback_2", callbackNode);
            startNode.CreateExternalCommonEdge("commonintent", "cd_sub", "continue_b");

            tree.AddStartState("start", startNode);

            return tree;
        }

        public async Task<PluginResult> Start(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> CallbackOne(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> CallbackOneTenative(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
        }

        public async Task<PluginResult> CallbackTwo(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> CallbackTwoWithParameters(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Assert that we got the parameter we expected
            if (string.Equals("value", DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "cd_sub.parameter")))
            {
                return new PluginResult(Result.Success);
            }
            else
            {
                return new PluginResult(Result.Failure);
            }
        }

        public override async Task<CrossDomainResponseData> CrossDomainResponse(CrossDomainContext context, IPluginServices pluginServices)
        {
            await DurandalTaskExtensions.NoOpTask;
            CrossDomainResponseData returnVal = new CrossDomainResponseData(this.LUDomain, "callback_intent_a");
            returnVal.FilledSlots.Add(new SlotValue("parameter", "parameterValue", SlotValueFormat.CrossDomainTag));
            returnVal.CallbackMultiturnBehavior = MultiTurnBehavior.ContinueBasic;
            return returnVal;
        }
    }

    public class CrossDomainSubAnswer : DurandalPlugin
    {
        public CrossDomainSubAnswer() : base("cd_sub") { }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode continueNode = tree.CreateNode(Continue);
            IConversationNode tenativeNode = tree.CreateNode(Tenative);
            IConversationNode cancelNode = tree.CreateNode(Cancel);

            continueNode.CreateNormalEdge("continue_b", continueNode);
            continueNode.CreateNormalEdge("tenative_b", tenativeNode);
            continueNode.CreateNormalEdge("cancel_b", cancelNode);

            tenativeNode.CreateNormalEdge("continue_b", continueNode);
            tenativeNode.CreateNormalEdge("tenative_b", tenativeNode);
            tenativeNode.CreateNormalEdge("cancel_b", cancelNode);

            tree.AddStartState("continue_b", continueNode);
            tree.AddStartState("tenative_b", tenativeNode);
            tree.AddStartState("cancel_b", cancelNode);

            IConversationNode callbackPathNode1 = tree.CreateNode(CallbackPath1);
            IConversationNode callbackPathNode2 = tree.CreateNode(CallbackPath2);
            IConversationNode callbackPathNode2Bad = tree.CreateNode(CallbackPath2Bad);
            callbackPathNode1.CreateNormalEdge("callback_2", callbackPathNode2);
            callbackPathNode1.CreateNormalEdge("callback_2_bad", callbackPathNode2Bad);
            tree.AddStartState("callback_1", callbackPathNode1);
            tree.AddStartState("callback_2", callbackPathNode2);

            return tree;
        }

        public async Task<PluginResult> Continue(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> Tenative(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
        }

        public async Task<PluginResult> Cancel(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };
        }

        public async Task<PluginResult> CallbackPath1(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> CallbackPath2(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            DialogAction callbackAction = DialogHelpers.BuildCallbackAction(queryWithContext, new List<SlotValue>());
            callbackAction.Slots.Add(new SlotValue("parameter", "value", SlotValueFormat.CrossDomainTag));
            return new PluginResult(Result.Success)
            {
                InvokedDialogAction = callbackAction
            };
        }

        public async Task<PluginResult> CallbackPath2Bad(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            DialogAction callbackAction = new DialogAction()
            {
                Domain = "cd_super",
                Intent = "nonexistent_intent",
                Slots = new List<SlotValue>()
            };
            callbackAction.Slots.Add(new SlotValue("parameter", "value", SlotValueFormat.CrossDomainTag));
            return new PluginResult(Result.Success)
            {
                InvokedDialogAction = callbackAction
            };
        }

        public override async Task<CrossDomainRequestData> CrossDomainRequest(string targetIntent)
        {
            await DurandalTaskExtensions.NoOpTask;
            CrossDomainRequestData returnVal = new CrossDomainRequestData();
            returnVal.RequestedSlots.Add(new CrossDomainSlot("parameter", true));
            return returnVal;
        }
    }

    public class ReflectionAnswer : DurandalPlugin
    {
        public ReflectionAnswer() : base("reflection") { }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode disambiguateNode = tree.CreateNode(DisambiguateStep1);
            IConversationNode selectionNode = tree.CreateNode(Selection);
            disambiguateNode.CreateCommonEdge("side_speech", selectionNode);

            tree.AddStartState("disambiguate", disambiguateNode);

            return tree;
        }

        public async Task<PluginResult> DisambiguateStep1(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            if (!services.SessionStore.ContainsKey("triggerResults"))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Trigger results not in session store"
                };
            }

            IDictionary<string, TriggerResult> triggerResults = JsonConvert.DeserializeObject<IDictionary<string, TriggerResult>>(services.SessionStore.GetString("triggerResults"));

            List<string> allActions = new List<string>();
            foreach (var result in triggerResults.Values)
            {
                allActions.Add(result.ActionName);
            }

            string message = "Did you mean " + string.Join(" or ", allActions) + "?";

            return new PluginResult(Result.Success)
            {
                ResponseText = message,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> Selection(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            if (!services.SessionStore.ContainsKey("triggerResults"))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Trigger results not in session store"
                };
            }

            IDictionary<string, TriggerResult> triggerResults = JsonConvert.DeserializeObject<IDictionary<string, TriggerResult>>(services.SessionStore.GetString("triggerResults"));

            DialogAction callbackAction = new DialogAction()
            {
                Domain = "reflection",
                Intent = "disambiguation_callback",
                Slots = new List<SlotValue>()
            };

            string boostedDomainIntent = queryWithContext.Understanding.Utterance.OriginalText;

            callbackAction.Slots.Add(new SlotValue("disambiguated_domain_intent", boostedDomainIntent, SlotValueFormat.StructuredData));

            return new PluginResult(Result.Success)
            {
                InvokedDialogAction = callbackAction,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }
    }

    public class TriggerAnswer : DurandalPlugin
    {
        public TriggerAnswer(string domain) : base(domain) { }

        public override async Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            if (queryWithContext.Understanding.Domain.Equals(this.LUDomain) && queryWithContext.Understanding.Intent.Equals("boost_with_params"))
            {
                services.SessionStore.Put("key", "value");

                return new TriggerResult(BoostingOption.Boost)
                {
                    ActionDescription = "Do some kind of action with parameters",
                    ActionName = "Action",
                    ActionNameSsml = "Action"
                };
            }

            if (queryWithContext.Understanding.Domain.Equals(this.LUDomain) && queryWithContext.Understanding.Intent.Equals("boost_with_params_2"))
            {
                services.SessionStore.Put("key2", "value2");

                return new TriggerResult(BoostingOption.Boost)
                {
                    ActionDescription = "Do some kind of action with parameters",
                    ActionName = "Action",
                    ActionNameSsml = "Action"
                };
            }

            if (queryWithContext.Understanding.Domain.Equals(this.LUDomain) && queryWithContext.Understanding.Intent.Equals("boost"))
            {
                return new TriggerResult(BoostingOption.Boost)
                {
                    ActionDescription = "Do some kind of action",
                    ActionName = "Action",
                    ActionNameSsml = "Action"
                };
            }

            if (queryWithContext.Understanding.Domain.Equals(this.LUDomain) && queryWithContext.Understanding.Intent.Equals("suppress"))
            {
                return new TriggerResult(BoostingOption.Suppress);
            }

            if (queryWithContext.Understanding.Domain.Equals(this.LUDomain) && queryWithContext.Understanding.Intent.StartsWith("trigger_slowly_"))
            {
                await Task.Delay(10);
                return new TriggerResult(BoostingOption.NoChange);
            }

            return null;
        }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            if (queryWithContext.Understanding.Intent.Equals("fail"))
            {
                return new PluginResult(Result.Failure);
            }

            if (queryWithContext.Understanding.Intent.Equals("boost_with_params") && (!services.SessionStore.ContainsKey("key") || !services.SessionStore.GetString("key").Equals("value")))
            {
                return new PluginResult(Result.Failure);
            }

            if (queryWithContext.Understanding.Intent.Equals("boost_with_params_2") && (!services.SessionStore.ContainsKey("key2") || !services.SessionStore.GetString("key2").Equals("value2")))
            {
                return new PluginResult(Result.Failure);
            }

            return new PluginResult(Result.Success);
        }
    }

    public class EntitiesAnswer : DurandalPlugin
    {
        public EntitiesAnswer(string domain) : base(domain) { }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            // EntityContext should be empty at start of every turn
            if ((await services.EntityContext.GetEntity("mem://myentityid")) != null)
            {
                return new PluginResult(Result.Failure);
            }

            if (queryWithContext.Understanding.Intent.Equals("write"))
            {
                Restaurant restaurant = new Restaurant(services.EntityContext, "mem://myentityid");
                restaurant.Name.Value = "Dick's Drive-in";
                services.EntityHistory.AddOrUpdateEntity(restaurant);
                return new PluginResult(Result.Success);
            }

            if (queryWithContext.Understanding.Intent.Equals("read"))
            {
                IList<Hypothesis<LocalBusiness>> businesses = services.EntityHistory.FindEntities<LocalBusiness>();
                if (businesses.Count != 1)
                {
                    return new PluginResult(Result.Failure);
                }

                LocalBusiness business = businesses[0].Value;
                if (!string.Equals(business.Name.Value, "Dick's Drive-in"))
                {
                    return new PluginResult(Result.Failure);
                }

                return new PluginResult(Result.Success);
            }

            return new PluginResult(Result.Success);
        }
    }
    
    public class VersioningPlugin1 : DurandalPlugin
    {
        private ushort _minorVersion;
        public VersioningPlugin1(ushort minorVersion) : base("versioning_plugin", "versioning")
        {
            _minorVersion = minorVersion;
        }

        public VersioningPlugin1() : this(0) { }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode node1 = tree.CreateNode(Turn1V1);
            IConversationNode node2 = tree.CreateNode(Turn2V1);
            tree.AddStartState("turn1", node1);
            node1.CreateNormalEdge("turn2", node2);
            return tree;
        }

        public Task<PluginResult> Turn1V1(QueryWithContext queryWithContext, IPluginServices services)
        {
            return Task.FromResult(new PluginResult(Result.Success)
            {
                ResponseText = "Turn 1 Version 1." + _minorVersion,
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            });
        }

        public Task<PluginResult> Turn2V1(QueryWithContext queryWithContext, IPluginServices services)
        {
            return Task.FromResult(new PluginResult(Result.Success)
            {
                ResponseText = "Turn 2 Version 1." + _minorVersion
            });
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            return new PluginInformation()
            {
                InternalName = "Versioning Plugin",
                MajorVersion = 1,
                MinorVersion = _minorVersion
            };
        }
    }

    public class VersioningPlugin2 : DurandalPlugin
    {
        public VersioningPlugin2() : base("versioning_plugin", "versioning") { }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode node1 = tree.CreateNode(Turn1V2);
            IConversationNode node2 = tree.CreateNode(Turn2V2);
            tree.AddStartState("turn1", node1);
            node1.CreateNormalEdge("turn2", node2);
            return tree;
        }

        public Task<PluginResult> Turn1V2(QueryWithContext queryWithContext, IPluginServices services)
        {
            return Task.FromResult(new PluginResult(Result.Success)
            {
                ResponseText = "Turn 1 Version 2",
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            });
        }

        public Task<PluginResult> Turn2V2(QueryWithContext queryWithContext, IPluginServices services)
        {
            return Task.FromResult(new PluginResult(Result.Success)
            {
                ResponseText = "Turn 2 Version 2"
            });
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            return new PluginInformation()
            {
                InternalName = "Versioning Plugin",
                MajorVersion = 2,
                MinorVersion = 2
            };
        }
    }

    public class RemotingPlugin : DurandalPlugin
    {
        public RemotingPlugin() : base("RemotingPlugin", "test_remoting") { }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode node1 = tree.CreateNode(Test1);
            tree.AddStartState("test1", node1);
            tree.AddStartState("speech_synth", SpeechSynth);
            tree.AddStartState("speech_reco", SpeechReco);
            tree.AddStartState("oauth_create_uri", OAuthCreateAuthUri);
            tree.AddStartState("oauth_get_token", OAuthGetToken);
            tree.AddStartState("oauth_delete_token", OAuthDeleteToken);
            return tree;
        }

        public override async Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            services.SessionStore.Put("UserState", "SomeUserState");
            return new TriggerResult(BoostingOption.Boost)
            {
                ActionDescription = "Call someone on the phone",
                ActionKnownAs = new List<LexicalString>()
                {
                    new LexicalString("call"),
                    new LexicalString("phone call"),
                    new LexicalString("calling")
                },
                ActionName = "Make a call",
                ActionNameSsml = "make a call"
            };
        }

        public async Task<PluginResult> Test1(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            services.Logger.Log("This is a logging message generated in the plugin", LogLevel.Wrn, privacyClass: DataPrivacyClassification.PublicNonPersonalData);
            return new PluginResult(Result.Success)
            {
                ResponseText = "Doctor Grant, the phones are working"
            };
        }

        public async Task<PluginResult> SpeechSynth(QueryWithContext queryWithContext, IPluginServices services)
        {
            SpeechSynthesisRequest synthRequest1 = new SpeechSynthesisRequest()
            {
                Ssml = "<speak>number 1</speak>",
                Locale = LanguageCode.EN_US,
                VoiceGender = VoiceGender.Unspecified
            };


            SpeechSynthesisRequest synthRequest2 = new SpeechSynthesisRequest()
            {
                Ssml = "<speak>and this is the second one</speak>",
                Locale = LanguageCode.EN_US,
                VoiceGender = VoiceGender.Unspecified
            };

            Task<SynthesizedSpeech> task1 = services.TTSEngine.SynthesizeSpeechAsync(synthRequest1, CancellationToken.None, DefaultRealTimeProvider.Singleton, services.Logger);
            Task<SynthesizedSpeech> task2 = services.TTSEngine.SynthesizeSpeechAsync(synthRequest2, CancellationToken.None, DefaultRealTimeProvider.Singleton, services.Logger);
            SynthesizedSpeech speech1 = await task1;
            SynthesizedSpeech speech2 = await task2;
            if (!string.Equals(speech1.Ssml, "<speak>number 1</speak>"))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "TTS ssml 1 had unexpected value"
                };
            }
            if (speech1.Audio == null || speech1.Audio.Data == null || speech1.Audio.Data.Count == 0)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "TTS 1 had no audio"
                };
            }
            if (!string.Equals(speech2.Ssml, "<speak>and this is the second one</speak>"))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "TTS ssml 2 had unexpected value"
                };
            }
            if (speech2.Audio == null || speech2.Audio.Data == null || speech2.Audio.Data.Count == 0)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "TTS 2 had no audio"
                };
            }

            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> SpeechReco(QueryWithContext queryWithContext, IPluginServices services)
        {
            using (IAudioGraph audioGraphStrong = new AudioGraph(AudioGraphCapabilities.None))
            {
                WeakPointer<IAudioGraph> audioGraph = new WeakPointer<IAudioGraph>(audioGraphStrong);
                Task <ISpeechRecognizer> recoStream1Task = services.SpeechRecoEngine.CreateRecognitionStream(audioGraph, null, LanguageCode.EN_US, services.Logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Task<ISpeechRecognizer> recoStream2Task = services.SpeechRecoEngine.CreateRecognitionStream(audioGraph, null, LanguageCode.SPANISH.InCountry(RegionCode.MEXICO), services.Logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                ISpeechRecognizer recoStream1 = await recoStream1Task;
                ISpeechRecognizer recoStream2 = await recoStream2Task;
                AudioSample inputAudio = new AudioSample(new float[32000], recoStream1.InputFormat);
                AudioSplitter splitter = new AudioSplitter(audioGraph, recoStream1.InputFormat, null);
                FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(audioGraph, inputAudio, null);
                splitter.ConnectInput(sampleSource);
                splitter.AddOutput(recoStream1);
                splitter.AddOutput(recoStream2);

                await sampleSource.WriteFully(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Task<SpeechRecognitionResult> finishReco1Task = recoStream1.FinishUnderstandSpeech(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Task<SpeechRecognitionResult> finishReco2Task = recoStream2.FinishUnderstandSpeech(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                SpeechRecognitionResult reco1 = await finishReco1Task;
                if (!string.Equals("this is the english reco result", reco1.RecognizedPhrases[0].DisplayText))
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Speech reco result 1 had unexpected value"
                    };
                }
                SpeechRecognitionResult reco2 = await finishReco2Task;
                if (!string.Equals("this is the spanish reco result", reco2.RecognizedPhrases[0].DisplayText))
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Speech reco result 2 had unexpected value"
                    };
                }

                return new PluginResult(Result.Success);
            }
        }

        public async Task<PluginResult> OAuthGetToken(QueryWithContext queryWithContext, IPluginServices services)
        {
            OAuthConfig oauthConfig = new OAuthConfig()
            {
                Type = OAuthFlavor.OAuth2,
                AuthUri = "https://www.service.com/auth",
                TokenUri = "https://www.service.com/token",
                ClientId = "clientid",
                ClientSecret = "clientsecret",
                ConfigName = "service",
                Scope = "scope",
            };

            OAuthToken token = await services.TryGetOAuthToken(oauthConfig, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            if (token == null || !string.Equals("secret", token.Token))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "OAuth token was null or invalid"
                };
            }

            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> OAuthDeleteToken(QueryWithContext queryWithContext, IPluginServices services)
        {
            OAuthConfig oauthConfig = new OAuthConfig()
            {
                Type = OAuthFlavor.OAuth2,
                AuthUri = "https://www.service.com/auth",
                TokenUri = "https://www.service.com/token",
                ClientId = "clientid",
                ClientSecret = "clientsecret",
                ConfigName = "service",
                Scope = "scope",
            };

            await services.DeleteOAuthToken(oauthConfig, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> OAuthCreateAuthUri(QueryWithContext queryWithContext, IPluginServices services)
        {
            OAuthConfig oauthConfig = new OAuthConfig()
            {
                Type = OAuthFlavor.OAuth2,
                AuthUri = "https://www.service.com/auth",
                TokenUri = "https://www.service.com/token",
                ClientId = "clientid",
                ClientSecret = "clientsecret",
                ConfigName = "service",
                Scope = "scope",
            };
            Uri authUri = await services.CreateOAuthUri(oauthConfig, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            if (authUri == null || !authUri.AbsoluteUri.Contains("https://www.service.com/auth"))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "OAuth URI was invalid"
                };
            }

            return new PluginResult(Result.Success);
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            return new PluginInformation()
            {
                InternalName = "test plugin for remoting",
                MajorVersion = 1,
                MinorVersion = 0
            };
        }
    }
}
