


namespace BVTTestDriver
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.Net;
    using Durandal.Common.NLP;
    using Durandal.Common.Security;
    using Durandal.Common.NLP.Train;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using Durandal.Common.NLP.Tagging;
    using Durandal.Common.NLP.Language.English;
    using System.Threading.Tasks;
    using Durandal.Common.Tasks;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Time;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Utils;
    using Durandal.Common.ServiceMgmt;

    public class TestRunner
    {
        public static IList<SingleTestResult> RunAllTests(
            DialogProcessingEngine dialogCore,
            ILUClient luConnection,
            TestInputSet testInputs,
            InMemoryConversationStateCache mockConversationCache,
            LanguageCode locale,
            bool threaded)
        {
            Console.WriteLine("Running BVT tests with {0} test conversations", testInputs.NumConversations);
            IList<SingleTestResult> returnVal = new List<SingleTestResult>();
            IThreadPool threadPool = new TaskThreadPool(NullMetricCollector.WeakSingleton, DimensionSet.Empty, "TestRunner");

            IList<ThreadedTestExecutor> executions = new List<ThreadedTestExecutor>();

            IWordBreaker wordBreaker = new EnglishWordBreaker();

            foreach (IList<TestUtterance> conversation in testInputs.Conversations)
            {
                string domain = conversation[0].ExpectedDomain;
                ThreadedTestExecutor executor = new ThreadedTestExecutor(domain, locale, dialogCore, luConnection, conversation, wordBreaker, mockConversationCache);
                executions.Add(executor);
                if (threaded)
                {
                    threadPool.EnqueueUserAsyncWorkItem(executor.Run);
                }
            }

            foreach (var executor in executions)
            {
                if (threaded)
                    executor.Join();
                else
                    executor.Run().Await();
                if (executor.GetResult() != null)
                {
                    returnVal.Add(executor.GetResult());
                }
            }

            return returnVal;
        }
        
        private class ThreadedTestExecutor
        {
            private string _domain;
            private LanguageCode _locale;
            private DialogProcessingEngine _dialog;
            private ILUClient _lu;
            private IList<TestUtterance> _conversation;
            private IWordBreaker _wordBreaker;
            private InMemoryConversationStateCache _mockConversationCache;
            private SingleTestResult _returnVal;

            private EventWaitHandle _finished;

            public ThreadedTestExecutor(string domain, LanguageCode locale, DialogProcessingEngine dialogCore, ILUClient luConnection, IList<TestUtterance> conversation, IWordBreaker wordBreaker, InMemoryConversationStateCache mockConversationCache)
            {
                _domain = domain;
                _dialog = dialogCore;
                _lu = luConnection;
                _conversation = conversation;
                _wordBreaker = wordBreaker;
                _locale = locale;
                _mockConversationCache = mockConversationCache;
                _finished = new EventWaitHandle(false, EventResetMode.ManualReset);
            }

            public async Task Run()
            {
                try
                {
                    _returnVal = await RunOneTest(_domain, _locale, _dialog, _lu, _conversation, _wordBreaker, _mockConversationCache);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("EXCEPTION: " + e.Message);
                }
                finally
                {
                    _finished.Set();
                }
            }

            public void Join()
            {
                _finished.WaitOne();
            }

            public SingleTestResult GetResult()
            {
                return _returnVal;
            }
        }

        private static async Task<SingleTestResult> RunOneTest(string domain, LanguageCode locale, DialogProcessingEngine dialogCore, ILUClient luConnection, IList<TestUtterance> conversation, IWordBreaker wordBreaker, InMemoryConversationStateCache mockConversationCache)
        {
            Stopwatch latencyTimer = new Stopwatch();
            latencyTimer.Start();
            SingleTestResult testResult = new SingleTestResult()
                {
                    Domain = domain,
                    ResultCode = TestResultCode.Success
                };

            /// Use the same traceId for the whole conversation
            Guid traceId = Guid.NewGuid();

            ClientContext defaultClientContext = new ClientContext()
                {
                    // ClientId == TraceId, for easier debugging
                    ClientId = traceId.ToString("N"),
                    Locale = locale
                };

            bool thisConversationIsValid = true;

            foreach (TestUtterance t in conversation)
            {
                // Is this a conversation starting utterance? (id == 0)
                // If so, wipe the conversation state
                if (t.Id == 0)
                {
                    await mockConversationCache.ClearBothStates(defaultClientContext.UserId, defaultClientContext.ClientId, NullLogger.Singleton, false);
                    thisConversationIsValid = true;
                }
                else if (!thisConversationIsValid)
                {
                    // When one query fails, we need to skip the rest of the conversation using this flag
                    continue;
                }

                ClientContext fakeContext = new ClientContext()
                    {
                        ClientId = traceId.ToString("N"),
                        Locale = locale,
                        ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                LURequest request = new LURequest()
                    {
                        DoFullAnnotation = false,
                        Context = fakeContext,
                        TraceId = CommonInstrumentation.FormatTraceId(traceId),
                        TextInput = t.Input
                    };

                thisConversationIsValid = false;

                NetworkResponseInstrumented<LUResponse> luResponse = await luConnection.MakeQueryRequest(request);
                if (luResponse.Response == null)
                {
                    testResult.ResultCode = TestResultCode.QasTimeout;
                    break;
                }

                if (luResponse.Response.Results.Count == 0)
                {
                    testResult.ResultCode = TestResultCode.NoQasResult;
                    testResult.FailedInput = t;
                    break;
                }

                List<RecoResult> recoResults = luResponse.Response.Results[0].Recognition;
                if (recoResults.Count == 0)
                {
                    testResult.ResultCode = TestResultCode.NoQasResult;
                    testResult.FailedInput = t;
                    break;
                }

                DialogEngineResponse deResponse = await dialogCore.Process(
                    results: RankedHypothesis.ConvertRecoResultList(recoResults),
                    clientContext: defaultClientContext,
                    authLevel: ClientAuthenticationLevel.ClientAuthorized | ClientAuthenticationLevel.UserAuthorized,
                    inputSource: InputMethod.Typed,
                    isNewConversation: false,
                    useTriggers: true,
                    traceId: traceId);

                if (deResponse.ResponseCode == Result.Skip)
                {
                    testResult.ResultCode = TestResultCode.QuerySkipped;
                    testResult.FailedInput = t;
                    break;
                }
                if (deResponse.ResponseCode == Result.Failure)
                {
                    testResult.ResultCode = TestResultCode.DialogError;
                    testResult.FailedInput = t;
                    break;
                }

                testResult.ActualDomainIntent = deResponse.TriggeredDomain + "/" + deResponse.TriggeredIntent;
                if (!deResponse.TriggeredDomain.Equals(t.ExpectedDomain))
                {
                    testResult.ResultCode = TestResultCode.WrongDomain;
                    testResult.FailedInput = t;
                    break;
                }
                if (!deResponse.TriggeredIntent.Equals(t.ExpectedIntent))
                {
                    testResult.ResultCode = TestResultCode.WrongIntent;
                    testResult.FailedInput = t;
                    break;
                }

                // Get slot accuracy statistics
                RecoResult triggeredRecoResult = deResponse.SelectedRecoResult;
                TaggedData goldenTags = TaggedDataSplitter.ParseSlots(t.TaggedInput, wordBreaker);
                TaggedData testTags = triggeredRecoResult.MostLikelyTags;
                CalculatePrecisionRecall(goldenTags, testTags, testResult);
                thisConversationIsValid = true;
            }
            latencyTimer.Stop();
            testResult.Latency = latencyTimer.ElapsedMilliseconds;
            return testResult;
        }

        private static void CalculatePrecisionRecall(TaggedData goldenTags, TaggedData testTags, SingleTestResult result)
        {
            float precision = 0;
            float recall = 0;
            ISet<string> allTags = new HashSet<string>();
            foreach (SlotValue slot in goldenTags.Slots)
            {
                if (!allTags.Contains(slot.Name))
                    allTags.Add(slot.Name);
            }
            foreach (SlotValue slot in testTags.Slots)
            {
                if (!allTags.Contains(slot.Name))
                    allTags.Add(slot.Name);
            }
            foreach (string tagName in allTags)
            {
                SlotValue goldenSlot = GetSlotByName(tagName, goldenTags.Slots);
                SlotValue testSlot = GetSlotByName(tagName, testTags.Slots);
                if (goldenSlot == null && testSlot == null)
                {
                    // Shouldn't hit this case
                }
                else if (goldenSlot != null && testSlot == null)
                {
                    // Recall failed
                }
                else if (goldenSlot == null && testSlot != null)
                {
                    // Recall was too high; tagged slots that shouldn't have been
                }
                else
                {
                    if (testSlot.Value.Contains(goldenSlot.Value))
                    {
                        recall += 1;
                    }
                    if (testSlot.Value.Equals(goldenSlot.Value))
                    {
                        precision += 1;
                    }
                }
            }
            if (allTags.Count == 0)
            {
                result.ContainsTags = false;
                result.TaggerPrecision = 0.0f;
                result.TaggerRecall = 0.0f;
            }
            else
            {
                result.ContainsTags = true;
                result.TaggerPrecision = precision / allTags.Count;
                result.TaggerRecall = recall / allTags.Count;
            }
        }

        private static SlotValue GetSlotByName(string name, IEnumerable<SlotValue> allSlots)
        {
            foreach (SlotValue x in allSlots)
            {
                if (x.Name.Equals(name))
                {
                    return x;
                }
            }
            return null;
        }
    }
}
