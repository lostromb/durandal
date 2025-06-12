using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Dialog
{
    [TestClass]
    public class ConversationStoreTests
    {
        [TestMethod]
        public void TestConversationStackOrder()
        {
            Stack<int> testStack = new Stack<int>();
            testStack.Push(1);
            testStack.Push(2);
            testStack.Push(3);
            List<int> testList = ConversationState.StackToList<int>(testStack);
            Assert.AreEqual(3, testList[0]);
            Assert.AreEqual(2, testList[1]);
            Assert.AreEqual(1, testList[2]);
            testStack = ConversationState.ListToStack<int>(testList);
            Assert.AreEqual(3, testStack.Pop());
            Assert.AreEqual(2, testStack.Pop());
            Assert.AreEqual(1, testStack.Pop());
        }

        [TestMethod]
        public async Task TestConversationStackCanCompletelyExpire()
        {
            IConversationStateCache cache = new InMemoryConversationStateCache();
            Stack<ConversationState> convo = new Stack<ConversationState>();
            ILogger logger = new ConsoleLogger();
            convo.Push(ConversationState.Deserialize(new SerializedConversationState()
            {
                ConversationExpireTime = 1,
                CurrentPluginDomain = "a"
            }, logger));
            convo.Push(ConversationState.Deserialize(new SerializedConversationState()
            {
                ConversationExpireTime = 1,
                CurrentPluginDomain = "b"
            }, logger));
            convo.Push(ConversationState.Deserialize(new SerializedConversationState()
            {
                ConversationExpireTime = 1,
                CurrentPluginDomain = "c"
            }, logger));
            await cache.SetRoamingState("user", convo, logger, false);
            RetrieveResult<Stack<ConversationState>> result = await cache.TryRetrieveState("user", "client", logger, DefaultRealTimeProvider.Singleton);
            Assert.IsFalse(result.Success);
            convo = result.Result;
            Assert.AreEqual(0, convo.Count);
        }

        [TestMethod]
        public async Task TestConversationStackCanPartiallyExpire()
        {
            IConversationStateCache cache = new InMemoryConversationStateCache();
            Stack<ConversationState> convo = new Stack<ConversationState>();
            ILogger logger = new ConsoleLogger();
            convo.Push(ConversationState.Deserialize(new SerializedConversationState()
            {
                ConversationExpireTime = DateTimeOffset.UtcNow.AddMinutes(1).Ticks,
                CurrentPluginDomain = "a"
            }, logger));
            convo.Push(ConversationState.Deserialize(new SerializedConversationState()
            {
                ConversationExpireTime = DateTimeOffset.UtcNow.AddMinutes(1).Ticks,
                CurrentPluginDomain = "b"
            }, logger));
            convo.Push(ConversationState.Deserialize(new SerializedConversationState()
            {
                ConversationExpireTime = 1,
                CurrentPluginDomain = "c"
            }, logger));
            await cache.SetClientSpecificState("user", "client", convo, logger, false);
            RetrieveResult<Stack<ConversationState>> result = await cache.TryRetrieveState("user", "client", logger, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(result.Success);
            convo = result.Result;
            Assert.AreEqual(2, convo.Count);
            Assert.AreEqual("b", convo.Pop().CurrentPluginDomain);
            Assert.AreEqual("a", convo.Pop().CurrentPluginDomain);
        }

        [TestMethod]
        public async Task TestConversationStateCacheInMemory()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            InMemoryConversationStateCache stateCache = new InMemoryConversationStateCache();
            string userId = "testuserid";
            string clientId = "testclientid";
            
            // Assert that there are no initial states
            RetrieveResult<Stack<ConversationState>> stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, logger, DefaultRealTimeProvider.Singleton);
            Assert.IsFalse(stateFetchResult.Success);

            // Store a roaming state and fetch it
            Stack<ConversationState> stateStack = new Stack<ConversationState>();
            stateStack.Push(
                ConversationState.Deserialize(
                    new SerializedConversationState()
                    {
                        CurrentPluginDomain = "a",
                        CurrentConversationNode = "func.a",
                        PreviousConversationTurns = new List<RecoResult>(),
                        LastMultiturnState = MultiTurnBehavior.None,
                        ConversationExpireTime = DateTimeOffset.UtcNow.AddHours(1).Ticks
                    },
                    logger));
            bool storeResult = await stateCache.SetRoamingState(userId, stateStack, logger, false);
            Assert.IsTrue(storeResult);

            stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, logger, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(stateFetchResult.Success);
            stateStack = stateFetchResult.Result;
            Assert.AreEqual(1, stateStack.Count);

            // Now write a client-specific state
            stateStack.Push(
                ConversationState.Deserialize(
                    new SerializedConversationState()
                    {
                        CurrentPluginDomain = "b",
                        CurrentConversationNode = "func.b",
                        PreviousConversationTurns = new List<RecoResult>(),
                        LastMultiturnState = MultiTurnBehavior.None,
                        ConversationExpireTime = DateTimeOffset.UtcNow.AddHours(1).Ticks
                    },
                    logger));

            storeResult = await stateCache.SetClientSpecificState(userId, clientId, stateStack, logger, false);
            Assert.IsTrue(storeResult);

            // And retrieve it
            stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, logger, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(stateFetchResult.Success);
            stateStack = stateFetchResult.Result;
            Assert.AreEqual(2, stateStack.Count);

            // Assert the retrieving from a different client will fetch the roaming result
            stateFetchResult = await stateCache.TryRetrieveState(userId, "nonexistentclient", logger, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(stateFetchResult.Success);
            stateStack = stateFetchResult.Result;
            Assert.AreEqual(1, stateStack.Count);

            // Now delete the roaming state
            bool deleteResult = await stateCache.ClearRoamingState(userId, logger, false);
            Assert.IsTrue(deleteResult);

            stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, logger, DefaultRealTimeProvider.Singleton);
            Assert.IsTrue(stateFetchResult.Success);
            stateStack = stateFetchResult.Result;
            Assert.AreEqual(2, stateStack.Count);
            stateFetchResult = await stateCache.TryRetrieveState(userId, "nonexistentclient", logger, DefaultRealTimeProvider.Singleton);
            Assert.IsFalse(stateFetchResult.Success);

            // And delete client-specific state
            deleteResult = await stateCache.ClearRoamingState(userId, logger, false);
            Assert.IsFalse(deleteResult);
            deleteResult = await stateCache.ClearClientSpecificState(userId, clientId, logger, false);
            Assert.IsTrue(deleteResult);

            stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, logger, DefaultRealTimeProvider.Singleton);
            Assert.IsFalse(stateFetchResult.Success);

            deleteResult = await stateCache.ClearBothStates(userId, clientId, logger, false);
            Assert.IsFalse(deleteResult);

            // Now write back both states and then delete them again
            stateStack = new Stack<ConversationState>();
            stateStack.Push(
                ConversationState.Deserialize(
                    new SerializedConversationState()
                    {
                        CurrentPluginDomain = "a",
                        CurrentConversationNode = "func.a",
                        PreviousConversationTurns = new List<RecoResult>(),
                        LastMultiturnState = MultiTurnBehavior.None,
                        ConversationExpireTime = DateTimeOffset.UtcNow.AddHours(1).Ticks
                    },
                    logger));
            storeResult = await stateCache.SetRoamingState(userId, stateStack, logger, false);
            Assert.IsTrue(storeResult);
            storeResult = await stateCache.SetClientSpecificState(userId, clientId, stateStack, logger, false);
            Assert.IsTrue(storeResult);
            deleteResult = await stateCache.ClearBothStates(userId, clientId, logger, false);
            Assert.IsTrue(deleteResult);
            stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, logger, DefaultRealTimeProvider.Singleton);
            Assert.IsFalse(stateFetchResult.Success);

            // Also try writing an expired state and asserting that it doesn't get returned
            stateStack = new Stack<ConversationState>();
            stateStack.Push(
                ConversationState.Deserialize(
                    new SerializedConversationState()
                    {
                        CurrentPluginDomain = "a",
                        CurrentConversationNode = "func.a",
                        PreviousConversationTurns = new List<RecoResult>(),
                        LastMultiturnState = MultiTurnBehavior.None,
                        ConversationExpireTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(5)).Ticks
                    },
                    logger));
            storeResult = await stateCache.SetRoamingState(userId, stateStack, logger, false);
            Assert.IsTrue(storeResult);
            stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, logger, DefaultRealTimeProvider.Singleton);
            Assert.IsFalse(stateFetchResult.Success);
            deleteResult = await stateCache.ClearBothStates(userId, clientId, logger, false);
            Assert.IsFalse(deleteResult); // Accessing an expired state automatically deleted it, so manual delete should return false
        }
    }
}
