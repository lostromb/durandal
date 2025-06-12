using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DialogTests
{
    using Durandal.API;
    using Durandal.API.Utils;
    using Newtonsoft.Json;
    using System.Threading;

    public class BasicAnswer : Answer
    {
        public BasicAnswer() : base("basic") { }

        public Trigger SucceedTrigger = new Trigger();
        public Trigger SkipTrigger = new Trigger();
        public Trigger FailTrigger = new Trigger();

        public override DialogResult ProcessImpl(QueryWithContext queryWithContext, AnswerServices services)
        {
            switch (queryWithContext.Result.Intent)
            {
                case "succeed":
                    SucceedTrigger.Trip();
                    return new DialogResult(Result.Success);
                case "skip":
                    SkipTrigger.Trip();
                    return new DialogResult(Result.Skip);
                case "skipwithmsg":
                    SkipTrigger.Trip();
                    return new DialogResult(Result.Skip)
                        {
                            ResponseText = "This plugin has been skipped"
                        };
                case "fail":
                    FailTrigger.Trip();
                    return new DialogResult(Result.Failure);
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

    public class SideSpeechAnswer : Answer
    {
        public SideSpeechAnswer() : base(DialogConstants.SIDE_SPEECH_DOMAIN) { }

        public Trigger SucceedTrigger = new Trigger();
        public Trigger SucceedHighTrigger = new Trigger();
        public Trigger FailTrigger = new Trigger();

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            tree.AddStartState(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, SideSpeechHigh);
            tree.AddStartState(DialogConstants.SIDE_SPEECH_INTENT, SideSpeechLow);
            return tree;
        }

        public DialogResult SideSpeechHigh(QueryWithContext queryWithContext, AnswerServices services)
        {
            SucceedHighTrigger.Trip();
            return new DialogResult(Result.Success);
        }

        public DialogResult SideSpeechLow(QueryWithContext queryWithContext, AnswerServices services)
        {
            SucceedTrigger.Trip();
            return new DialogResult(Result.Success);
        }

        public void Reset()
        {
            SucceedTrigger.Reset();
            SucceedHighTrigger.Reset();
            FailTrigger.Reset();
        }
    }

    public class SandboxAnswer : Answer
    {
        public SandboxAnswer() : base("sandbox") { }

        public override DialogResult ProcessImpl(QueryWithContext queryWithContext, AnswerServices services)
        {
            if (queryWithContext.Result.Intent.Equals("fail"))
            {
                return new DialogResult(Result.Failure);
            }
            if (queryWithContext.Result.Intent.Equals("timeout"))
            {
                Thread.Sleep(300000);
            }
            if (queryWithContext.Result.Intent.Equals("timeout_small"))
            {
                Thread.Sleep(2000);
            }
            if (queryWithContext.Result.Intent.Equals("exception"))
            {
                throw new NullReferenceException("Test exception being thrown from a user plugin");
            }

            return new DialogResult(Result.Success);
        }
    }

    public class BasicTreeAnswer : Answer
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

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            ConversationNode startNode = tree.CreateNode(StartNode);
            ConversationNode startTenativeNode = tree.CreateNode(StartTenativeNode);
            ConversationNode turn2Node = tree.CreateNode(Turn2Node);
            ConversationNode turn2PromiscNode = tree.CreateNode(Turn2PromiscNode);
            ConversationNode startObjectStoreNode = tree.CreateNode(StartObjectStoreNode);
            ConversationNode turn2ObjectStoreNode = tree.CreateNode(Turn2ObjectStoreNode);
            ConversationNode confirmNode = tree.CreateNode(ConfirmNode);
            ConversationNode sideSpeechNode = tree.CreateNode(SideSpeechNode);
            ConversationNode noRecoNode = tree.CreateNode(NoRecoNode);
            ConversationNode startLockNode = tree.CreateNode(StartLockNode);
            ConversationNode startSideSpeechNode = tree.CreateNode(StartSideSpeechNode);
            ConversationNode startDialogActionNode = tree.CreateNode(StartDialogActionNode);
            ConversationNode fallbackNode = tree.CreateNode(FallbackNode);
            ConversationNode loopNode = tree.CreateNode(LoopNode);
            ConversationNode loop2Node = tree.CreateNode(Loop2Node);

            tree.AddStartState("start", startNode);
            tree.AddStartState("start_tenative", startTenativeNode);
            tree.AddStartState("start_lock", startLockNode);
            tree.AddStartState("start_sidespeech", startSideSpeechNode);
            tree.AddStartState("start_objectstore", startObjectStoreNode);
            tree.AddStartState("start_dialogaction", startDialogActionNode);
            tree.AddStartState("start_continuations", ExplicitContinuationNode1);
            
            startNode.CreateNormalEdge("turn2", turn2Node);
            startNode.CreateCommonEdge("confirm", confirmNode);
            startNode.CreateCommonEdge("side_speech", sideSpeechNode);
            startNode.CreateCommonEdge("noreco", noRecoNode);
            startNode.CreateNormalEdge("turn2_promisc", turn2PromiscNode);
            startNode.CreateNormalEdge("loop", loopNode);

            startLockNode.CreateNormalEdge("turn2", turn2Node);

            startDialogActionNode.CreateNormalEdge("turn2", turn2Node);

            startTenativeNode.CreateNormalEdge("turn2", turn2Node);
            startTenativeNode.CreateNormalEdge("loop2", loop2Node);

            startSideSpeechNode.CreateCommonEdge("side_speech", sideSpeechNode);
            startSideSpeechNode.CreateNormalEdge("turn2", turn2Node);

            startObjectStoreNode.CreateNormalEdge("turn2", turn2ObjectStoreNode);

            turn2PromiscNode.CreatePromiscuousEdge(fallbackNode);

            loopNode.CreateNormalEdge("loop", loopNode);

            loop2Node.CreateNormalEdge("loop2", loop2Node);

            return tree;
        }

        public DialogResult StartNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            StartTrigger.Trip();
            return new DialogResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
        }

        public DialogResult StartLockNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            StartLockTrigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult StartObjectStoreNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            // verify nothing is in the store to begin with
            string val;
            if (services.SessionStore.TryGetString("key", out val))
            {
                return new DialogResult(Result.Failure);
            }

            services.SessionStore.Put("key", "value");
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult StartDialogActionNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            DialogResult returnVal = new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                ResponseText = "hi"
            };

            returnVal.ResponseData["actionKey"] = services.RegisterDialogAction(new DialogAction() { Domain = this.Domain, Intent = "turn2" });
            returnVal.ResponseData["actionUrl"] = services.RegisterDialogActionUrl(new DialogAction() { Domain = this.Domain, Intent = "turn2" }, queryWithContext.ClientContext.ClientId);

            return returnVal;
        }

        public DialogResult StartTenativeNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            StartTenativeTrigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
        }

        public DialogResult StartSideSpeechNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            StartTrigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult StartRetryNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            StartRetryTrigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult ConfirmNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            ConfirmTrigger.Trip();
            return new DialogResult(Result.Success);
        }

        public DialogResult FallbackNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success);
        }

        public DialogResult Turn2Node(QueryWithContext queryWithContext, AnswerServices services)
        {
            Turn2Trigger.Trip();
            return new DialogResult(Result.Success);
        }

        public DialogResult Turn2PromiscNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            Turn2Trigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult Turn2ObjectStoreNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            string val;
            if (services.SessionStore.TryGetString("key", out val) && val.Equals("value"))
            {
                return new DialogResult(Result.Success);
            }

            return new DialogResult(Result.Failure);
        }

        public DialogResult SideSpeechNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            SideSpeechTrigger.Trip();
            return new DialogResult(Result.Success);
        }

        public DialogResult NoRecoNode(QueryWithContext queryWithContext, AnswerServices services)
        {
            NoRecoTrigger.Trip();
            return new DialogResult(Result.Success);
        }

        /// <summary>
        /// this is used to test conversation history ordering and pruning logic
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <returns></returns>
        public DialogResult LoopNode(QueryWithContext queryWithContext, AnswerServices services)
        {
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
                return new DialogResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            }
            else
            {
                return new DialogResult(Result.Failure);
            }
        }

        public DialogResult Loop2Node(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult ExplicitContinuationNode1(QueryWithContext queryWithContext, AnswerServices services)
        {
            if (queryWithContext.Result.Utterance.OriginalText.Equals("static"))
            {
                return new DialogResult(Result.Success)
                {
                    Continuation = ExplicitContinuationNode2Static,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            }

            if (queryWithContext.Result.Utterance.OriginalText.Equals("lambda"))
            {
                return new DialogResult(Result.Success)
                {
                    Continuation = (q, s) => { return new DialogResult(Result.Success); },
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            }

            if (queryWithContext.Result.Utterance.OriginalText.Equals("private"))
            {
                return new DialogResult(Result.Success)
                {
                    Continuation = ExplicitContinuationNode2Private,
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            }

            return new DialogResult(Result.Success)
            {
                Continuation = ExplicitContinuationNode2,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult ExplicitContinuationNode2(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success);
        }

        public static DialogResult ExplicitContinuationNode2Static(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success);
        }

        private DialogResult ExplicitContinuationNode2Private(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success);
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

    public class RetryTreeAnswer : Answer
    {
        public RetryTreeAnswer() : base("retrytree") { }

        public Trigger StartSucceedTrigger = new Trigger();
        public Trigger StartSkipTrigger = new Trigger();
        public Trigger StartFailTrigger = new Trigger();
        public Trigger RetrySucceedTrigger = new Trigger();
        public Trigger RetrySkipTrigger = new Trigger();
        public Trigger RetryFailTrigger = new Trigger();
        public Trigger Turn2Trigger = new Trigger();

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            ConversationNode startSucceedNode = tree.CreateNode(StartSucceed);
            ConversationNode startSkipNode = tree.CreateNode(StartSkip);
            ConversationNode startFailNode = tree.CreateNode(StartFail);
            ConversationNode turn2Node = tree.CreateNode(Turn2);

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

        public DialogResult StartSucceed(QueryWithContext queryWithContext, AnswerServices services)
        {
            StartSucceedTrigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult StartSkip(QueryWithContext queryWithContext, AnswerServices services)
        {
            StartSkipTrigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult StartFail(QueryWithContext queryWithContext, AnswerServices services)
        {
            StartFailTrigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult Turn2(QueryWithContext queryWithContext, AnswerServices services)
        {
            Turn2Trigger.Trip();
            return new DialogResult(Result.Success);
        }

        public DialogResult RetrySucceed(QueryWithContext queryWithContext, AnswerServices services)
        {
            RetrySucceedTrigger.Trip();
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult RetrySkip(QueryWithContext queryWithContext, AnswerServices services)
        {
            RetrySkipTrigger.Trip();
            return new DialogResult(Result.Skip)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult RetryFail(QueryWithContext queryWithContext, AnswerServices services)
        {
            RetryFailTrigger.Trip();
            return new DialogResult(Result.Failure)
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

    public class ClientCapsAnswer : Answer
    {
        public ClientCapsAnswer() : base("clientcaps") { }

        public override DialogResult ProcessImpl(QueryWithContext queryWithContext, AnswerServices services)
        {
            string responseText = "This is sample text";
            AudioResponse responseAudio = new AudioResponse(new byte[5000], 16000);
            string responseHtml = "<html><body>This is a sample HTML page</body></html>";
            string ResponseSSML = "<ssml>This is sample SSML</ssml>";
            string responseUrl = "http://www.com";

            switch (queryWithContext.Result.Intent)
            {
                case "url":
                    // Return a URL but no HTML
                    return new DialogResult(Result.Success)
                        {
                            ResponseSSML = ResponseSSML,
                            ResponseText = responseText,
                            ResponseUrl = responseUrl
                        };
                case "html":
                    // Return HTML but no URL
                    return new DialogResult(Result.Success)
                        {
                            ResponseHtml = responseHtml,
                            ResponseSSML = ResponseSSML,
                            ResponseText = responseText
                        };
                case "customaudio":
                    // Return a full result with custom audio
                    return new DialogResult(Result.Success)
                    {
                        ResponseHtml = responseHtml,
                        ResponseSSML = ResponseSSML,
                        ResponseText = responseText,
                        ResponseAudio = responseAudio
                    };
                case "textonly":
                    // Return text only
                    return new DialogResult(Result.Success)
                    {
                        ResponseText = responseText
                    };
                case "textssml":
                    // Return text only
                    return new DialogResult(Result.Success)
                    {
                        ResponseText = responseText,
                        ResponseSSML = ResponseSSML
                    };
                case "audioonly":
                    // Return audio only
                    return new DialogResult(Result.Success)
                    {
                        ResponseAudio = responseAudio,
                    };
                case "cache":
                    // Throw some data into the temporary cache
                    string cacheUrl = services.CreateTemporaryWebResource(Encoding.UTF8.GetBytes("Cached data"), "text/plain; charset=utf-8");
                    return new DialogResult(Result.Success)
                    {
                        ResponseUrl = cacheUrl
                    };
                default:
                    // Return both
                    return new DialogResult(Result.Success)
                        {
                            ResponseHtml = responseHtml,
                            ResponseSSML = ResponseSSML,
                            ResponseText = responseText,
                            ResponseUrl = responseUrl
                        };
            }
        }
    }

    public class CrossDomainAnswer : Answer
    {
        public CrossDomainAnswer(string realDomain) : base(realDomain) { }

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            ConversationNode startNode = tree.CreateNode(Start);

            startNode.CreateExternalEdge("basic_a", "crossdomain_b", "basic_b");
            startNode.CreateExternalEdge("params_a", "crossdomain_b", "params_b");
            startNode.CreateExternalEdge("slotparams_a", "crossdomain_b", "slotparams_b");
            startNode.CreateExternalEdge("unsupported_request_a", "crossdomain_b", "unsupported_request_b");
            startNode.CreateExternalEdge("unsupported_response_a", "crossdomain_b", "unsupported_response_b");
            startNode.CreateExternalEdge("no_target_intent", "crossdomain_b", "fartmaster");

            // On the "a" side
            tree.AddStartState("start", startNode);

            // On the "b" side
            tree.AddStartState("params_b", ParamsEntryPointB);
            tree.AddStartState("slotparams_b", SlotParamsEntryPointB);
            tree.AddStartState("basic_b", BasicEntryPointB);

            return tree;
        }

        public DialogResult Start(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
                {
                     MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
        }

        public override CrossDomainRequestData CrossDomainRequest(string targetIntent)
        {
            CrossDomainRequestData returnVal = new CrossDomainRequestData ();

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
            
            // Unsupported request
            return null;
        }

        public override CrossDomainResponseData CrossDomainResponse(string requestDomain, string requestIntent, ISet<CrossDomainSlot> requestedSlots, IList<RecoResult> pastConversationSlots)
        {
            CrossDomainResponseData returnVal = new CrossDomainResponseData();
            if (requestIntent.Equals("basic_b"))
            {
                return returnVal;
            }
            if (requestIntent.Equals("fartmaster"))
            {
                return returnVal;
            }
            if (requestIntent.Equals("params_b"))
            {
                if (requestedSlots.Count > 0 && requestedSlots.FirstOrDefault().SlotName.Equals("location"))
                {
                    SlotValue responseSlot = new SlotValue("location", "my house", SlotValueFormat.CrossDomainTag);
                    LocationEntity location = new LocationEntity()
                        {
                            Name = "my house"
                        };

                    responseSlot.AddLocationEntity(location);
                    returnVal.FilledSlots.Add(responseSlot);
                    return returnVal;
                }
            }
            if (requestIntent.Equals("slotparams_b"))
            {
                SlotValue currentTurnSlot = DialogHelpers.TryGetAnySlot(pastConversationSlots, "param1");
                if (currentTurnSlot == null)
                {
                    return null;
                }

                currentTurnSlot.Name = "location";
                returnVal.FilledSlots.Add(currentTurnSlot);
                return returnVal;
            }

            // Unsupported response
            return null;
        }

        public DialogResult ParamsEntryPointB(QueryWithContext queryWithContext, AnswerServices services)
        {
            // Validate the input
            if (queryWithContext.TurnNum != 0 || queryWithContext.PastTurns.Count > 0)
            {
                return new DialogResult(Result.Failure);
            }

            TaggedData tags = queryWithContext.Result.MostLikelyTags;
            if (tags == null)
            {
                return new DialogResult(Result.Failure);
            }

            SlotValue slot = DialogHelpers.TryGetSlot(tags, "location");
            if (slot == null)
            {
                return new DialogResult(Result.Failure);
            }

            IList<LocationEntity> locations = slot.GetLocationEntities();
            if (locations.Count == 0 || !locations[0].Name.Equals("my house"))
            {
                return new DialogResult(Result.Failure);
            }

            return new DialogResult(Result.Success);
        }

        public DialogResult SlotParamsEntryPointB(QueryWithContext queryWithContext, AnswerServices services)
        {
            // Validate the input
            if (queryWithContext.TurnNum != 0 || queryWithContext.PastTurns.Count > 0)
            {
                return new DialogResult(Result.Failure);
            }

            TaggedData tags = queryWithContext.Result.MostLikelyTags;
            if (tags == null)
            {
                return new DialogResult(Result.Failure);
            }

            SlotValue slot = DialogHelpers.TryGetSlot(tags, "location");
            if (slot == null)
            {
                return new DialogResult(Result.Failure);
            }

            if (!slot.Value.Equals("value1"))
            {
                return new DialogResult(Result.Failure);
            }

            return new DialogResult(Result.Success);
        }

        public DialogResult BasicEntryPointB(QueryWithContext queryWithContext, AnswerServices services)
        {
            // We should be virtually starting in a new conversation with no apparent context (since no params were passed in)
            if (queryWithContext.TurnNum != 0 || queryWithContext.PastTurns.Count > 0)
            {
                return new DialogResult(Result.Failure);
            }
            
            return new DialogResult(Result.Success);
        }
    }

    public class CrossDomainSuperAnswer : Answer
    {
        public CrossDomainSuperAnswer() : base("cd_super") { }

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            ConversationNode startNode = tree.CreateNode(Start);
            ConversationNode callbackNode = tree.CreateNode(CallbackOne);
            ConversationNode callbackTenativeNode = tree.CreateNode(CallbackOneTenative);
            ConversationNode callbackNode2 = tree.CreateNode(CallbackTwo);
            ConversationNode callbackNode2WithParams = tree.CreateNode(CallbackTwoWithParameters);
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

        public DialogResult Start(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult CallbackOne(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult CallbackOneTenative(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
        }

        public DialogResult CallbackTwo(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success);
        }

        public DialogResult CallbackTwoWithParameters(QueryWithContext queryWithContext, AnswerServices services)
        {
            // Assert that we got the parameter we expected
            if (string.Equals("value", DialogHelpers.TryGetSlotValue(queryWithContext.Result, "cd_sub.parameter")))
            {
                return new DialogResult(Result.Success);
            }
            else
            {
                return new DialogResult(Result.Failure);
            }
        }

        public override CrossDomainResponseData CrossDomainResponse(string requestDomain, string requestIntent, ISet<CrossDomainSlot> requestedSlots, IList<RecoResult> pastConversationSlots)
        {
            CrossDomainResponseData returnVal = new CrossDomainResponseData();
            returnVal.FilledSlots.Add(new SlotValue("parameter", "parameterValue", SlotValueFormat.CrossDomainTag));
            returnVal.CallbackMultiturnBehavior = MultiTurnBehavior.ContinueBasic;
            return returnVal;
        }
    }

    public class CrossDomainSubAnswer : Answer
    {
        public CrossDomainSubAnswer() : base("cd_sub") { }

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            ConversationNode continueNode = tree.CreateNode(Continue);
            ConversationNode tenativeNode = tree.CreateNode(Tenative);
            ConversationNode cancelNode = tree.CreateNode(Cancel);

            continueNode.CreateNormalEdge("continue_b", continueNode);
            continueNode.CreateNormalEdge("tenative_b", tenativeNode);
            continueNode.CreateNormalEdge("cancel_b", cancelNode);

            tenativeNode.CreateNormalEdge("continue_b", continueNode);
            tenativeNode.CreateNormalEdge("tenative_b", tenativeNode);
            tenativeNode.CreateNormalEdge("cancel_b", cancelNode);

            tree.AddStartState("continue_b", continueNode);
            tree.AddStartState("tenative_b", tenativeNode);
            tree.AddStartState("cancel_b", cancelNode);

            ConversationNode callbackPathNode1 = tree.CreateNode(CallbackPath1);
            ConversationNode callbackPathNode2 = tree.CreateNode(CallbackPath2);
            ConversationNode callbackPathNode2Bad = tree.CreateNode(CallbackPath2Bad);
            callbackPathNode1.CreateNormalEdge("callback_2", callbackPathNode2);
            callbackPathNode1.CreateNormalEdge("callback_2_bad", callbackPathNode2Bad);
            tree.AddStartState("callback_1", callbackPathNode1);
            tree.AddStartState("callback_2", callbackPathNode2);

            return tree;
        }

        public DialogResult Continue(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult Tenative(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
        }

        public DialogResult Cancel(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };
        }

        public DialogResult CallbackPath1(QueryWithContext queryWithContext, AnswerServices services)
        {
            return new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult CallbackPath2(QueryWithContext queryWithContext, AnswerServices services)
        {
            DialogAction callbackAction = new DialogAction()
            {
                Domain = "cd_super",
                Intent = "callback_intent_a",
                Slots = new List<SlotValue>()
            };
            callbackAction.Slots.Add(new SlotValue("parameter", "value", SlotValueFormat.CrossDomainTag));
            return new DialogResult(Result.Success)
            {
                InvokedDialogAction = callbackAction
            };
        }

        public DialogResult CallbackPath2Bad(QueryWithContext queryWithContext, AnswerServices services)
        {
            DialogAction callbackAction = new DialogAction()
            {
                Domain = "cd_super",
                Intent = "nonexistent_intent",
                Slots = new List<SlotValue>()
            };
            callbackAction.Slots.Add(new SlotValue("parameter", "value", SlotValueFormat.CrossDomainTag));
            return new DialogResult(Result.Success)
            {
                InvokedDialogAction = callbackAction
            };
        }

        public override CrossDomainRequestData CrossDomainRequest(string targetIntent)
        {
            CrossDomainRequestData returnVal = new CrossDomainRequestData();
            returnVal.RequestedSlots.Add(new CrossDomainSlot("parameter", true));
            return returnVal;
        }
    }

    public class ReflectionAnswer : Answer
    {
        public ReflectionAnswer() : base("reflection") { }

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            ConversationNode disambiguateNode = tree.CreateNode(DisambiguateStep1);
            ConversationNode selectionNode = tree.CreateNode(Selection);
            disambiguateNode.CreateCommonEdge("side_speech", selectionNode);

            tree.AddStartState("disambiguate", disambiguateNode);

            return tree;
        }

        public DialogResult DisambiguateStep1(QueryWithContext queryWithContext, AnswerServices services)
        {
            if (!services.SessionStore.ContainsKey("triggerResults"))
            {
                return new DialogResult(Result.Failure)
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

            return new DialogResult(Result.Success)
            {
                ResponseText = message,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public DialogResult Selection(QueryWithContext queryWithContext, AnswerServices services)
        {
            if (!services.SessionStore.ContainsKey("triggerResults"))
            {
                return new DialogResult(Result.Failure)
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

            string boostedDomainIntent = queryWithContext.Result.Utterance.OriginalText;

            callbackAction.Slots.Add(new SlotValue("disambiguated_domain_intent", boostedDomainIntent, SlotValueFormat.StructuredData));

            return new DialogResult(Result.Success)
            {
                InvokedDialogAction = callbackAction,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }
    }

    public class TriggerAnswer : Answer
    {
        public TriggerAnswer(string domain) : base(domain) { }

        public override TriggerResult Trigger(QueryWithContext queryWithContext, AnswerServices services)
        {
            if (queryWithContext.Result.Domain.Equals(this.Domain) && queryWithContext.Result.Intent.Equals("boost_with_params"))
            {
                services.SessionStore.Put("key", "value");

                return new TriggerResult(BoostingOption.Boost)
                {
                    ActionDescription = "Do some kind of action with parameters",
                    ActionName = "Action",
                    ActionNameSpoken = "Action"
                };
            }

            if (queryWithContext.Result.Domain.Equals(this.Domain) && queryWithContext.Result.Intent.Equals("boost_with_params_2"))
            {
                services.SessionStore.Put("key2", "value2");

                return new TriggerResult(BoostingOption.Boost)
                {
                    ActionDescription = "Do some kind of action with parameters",
                    ActionName = "Action",
                    ActionNameSpoken = "Action"
                };
            }

            if (queryWithContext.Result.Domain.Equals(this.Domain) && queryWithContext.Result.Intent.Equals("boost"))
            {
                return new TriggerResult(BoostingOption.Boost)
                {
                    ActionDescription = "Do some kind of action",
                    ActionName = "Action",
                    ActionNameSpoken = "Action"
                };
            }

            if (queryWithContext.Result.Domain.Equals(this.Domain) && queryWithContext.Result.Intent.Equals("suppress"))
            {
                return new TriggerResult(BoostingOption.Suppress);
            }

            return null;
        }

        public override DialogResult ProcessImpl(QueryWithContext queryWithContext, AnswerServices services)
        {
            if (queryWithContext.Result.Intent.Equals("fail"))
            {
                return new DialogResult(Result.Failure);
            }

            if (queryWithContext.Result.Intent.Equals("boost_with_params") && (!services.SessionStore.ContainsKey("key") || !services.SessionStore.GetString("key").Equals("value")))
            {
                return new DialogResult(Result.Failure);
            }

            if (queryWithContext.Result.Intent.Equals("boost_with_params_2") && (!services.SessionStore.ContainsKey("key2") || !services.SessionStore.GetString("key2").Equals("value2")))
            {
                return new DialogResult(Result.Failure);
            }

            return new DialogResult(Result.Success);
        }
    }
}
