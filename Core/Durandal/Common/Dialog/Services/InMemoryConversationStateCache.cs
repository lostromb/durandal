using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Dialog.Services
{
    public class InMemoryConversationStateCache : IConversationStateCache
    {
        private IDictionary<string, SerializedConversationStateStack> _roamingStates;
        private IDictionary<string, SerializedConversationStateStack> _clientSpecificStates;
        private readonly ReaderWriterLockSlim _conversationStateLock;
        private int _disposed = 0;

        public InMemoryConversationStateCache()
        {
            _clientSpecificStates = new Dictionary<string, SerializedConversationStateStack>();
            _roamingStates = new Dictionary<string, SerializedConversationStateStack>();
            _conversationStateLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~InMemoryConversationStateCache()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public async Task<bool> SetClientSpecificState(string userId, string clientId, Stack<ConversationState> newState, ILogger queryLogger, bool fireAndForget)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                throw new ArgumentNullException("Client ID");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("User ID");
            }

            string clientSpecificKey = userId + ":" + clientId;

            _conversationStateLock.EnterWriteLock();
            try
            {
                if (_clientSpecificStates.ContainsKey(clientSpecificKey))
                {
                    _clientSpecificStates.Remove(clientSpecificKey);
                }

                SerializedConversationStateStack convertedStack = new SerializedConversationStateStack()
                {
                    Stack = ConversationState.StackToList(ConversationState.ConvertStack(newState))
                };

                _clientSpecificStates[clientSpecificKey] = convertedStack;
                queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Store_SessionWriteClientState, clientId, 0), LogLevel.Ins);
                queryLogger.Log("Updated client-specific conversation state " + clientSpecificKey, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            }
            finally
            {
                _conversationStateLock.ExitWriteLock();
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> SetRoamingState(string userId, Stack<ConversationState> newState, ILogger queryLogger, bool fireAndForget)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            _conversationStateLock.EnterWriteLock();
            try
            {
                if (_roamingStates.ContainsKey(userId))
                {
                    _roamingStates.Remove(userId);
                }

                SerializedConversationStateStack convertedStack = new SerializedConversationStateStack()
                {
                    Stack = ConversationState.StackToList(ConversationState.ConvertStack(newState))
                };

                _roamingStates[userId] = convertedStack;
                queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Store_SessionWriteRoamingState, userId, 0), LogLevel.Ins);
                queryLogger.Log("Updated roaming conversation state " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            }
            finally
            {
                _conversationStateLock.ExitWriteLock();
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<RetrieveResult<Stack<ConversationState>>> TryRetrieveState(string userId, string clientId, ILogger queryLogger, IRealTimeProvider realTime)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            string clientSpecificKey = string.IsNullOrEmpty(clientId) ? null : (userId + ":" + clientId);

            _conversationStateLock.EnterUpgradeableReadLock();
            try
            {
                SerializedConversationStateStack state = null;
                if (clientSpecificKey != null && _clientSpecificStates.ContainsKey(clientSpecificKey))
                {
                    state = _clientSpecificStates[clientSpecificKey];
                    queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "Using client-specific state {0}", clientSpecificKey);
                }
                else if (_roamingStates.ContainsKey(userId))
                {
                    state = _roamingStates[userId];
                    queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "Using roaming conversation state {0}", userId);
                }

                if (state != null)
                {
                    Stack<SerializedConversationState> existingStack = ConversationState.ListToStack(state.Stack);

                    while (existingStack.Count != 0)
                    {
                        SerializedConversationState topState = existingStack.Peek();
                        DateTimeOffset expireTime = new DateTimeOffset(topState.ConversationExpireTime, TimeSpan.Zero);
                        DateTimeOffset currentTime = realTime.Time;
                        if (currentTime > expireTime)
                        {
                            queryLogger.Log("State for " + topState.CurrentPluginDomain + " expired, "
                                + currentTime.ToString() + " > " + expireTime.ToString());
                            existingStack.Pop();
                        }
                        else
                        {
                            // A state already exists and has not entirely expired, so use it
                            queryLogger.Log("Got conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                            Stack<ConversationState> returnVal = ConversationState.ConvertStack(existingStack);
                            return new RetrieveResult<Stack<ConversationState>>(returnVal);
                        }
                    }

                    // State exists but has expired, so create a new one and remove the one in the store
                    queryLogger.Log("Client " + userId + " attempted to retrieve an expired conversation state", LogLevel.Wrn, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);

                    _conversationStateLock.EnterWriteLock();
                    if (_roamingStates.ContainsKey(userId))
                    {
                        _roamingStates.Remove(userId);
                    }
                    if (_clientSpecificStates.ContainsKey(clientSpecificKey))
                    {
                        _clientSpecificStates.Remove(clientSpecificKey);
                    }
                    _conversationStateLock.ExitWriteLock();

                    queryLogger.Log("Building new conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                }
                else
                {
                    // No state at all; create empty
                    queryLogger.Log("Building new conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                }
            }
            finally
            {
                _conversationStateLock.ExitUpgradeableReadLock();
            }

            return new RetrieveResult<Stack<ConversationState>>(new Stack<ConversationState>(), 0, false);
        }

        /// <summary>
        /// Used exclusively in automated testing; not actually part of the interface
        /// </summary>
        public void ClearAllConversationStates()
        {
            _conversationStateLock.EnterWriteLock();
            try
            {
                // queryLogger.Log("Clearing all conversation states");
                _roamingStates.Clear();
                _clientSpecificStates.Clear();
            }
            finally
            {
                _conversationStateLock.ExitWriteLock();
            }
        }

        public async Task<bool> ClearRoamingState(string userId, ILogger queryLogger, bool fireAndForget)
        {
            if (string.IsNullOrEmpty(userId))
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                throw new ArgumentNullException("User ID");
            }

            bool returnVal = false;

            _conversationStateLock.EnterWriteLock();
            try
            {
                queryLogger.Log("Clearing roaming conversation state " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                if (_roamingStates.ContainsKey(userId))
                {
                    _roamingStates.Remove(userId);
                    returnVal = true;
                }

                return returnVal;
            }
            finally
            {
                _conversationStateLock.ExitWriteLock();
            }
        }

        public async Task<bool> ClearClientSpecificState(string userId, string clientId, ILogger queryLogger, bool fireAndForget)
        {
            if (string.IsNullOrEmpty(userId))
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                throw new ArgumentNullException("User ID");
            }
            else if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("Client ID");
            }
            
            bool returnVal = false;

            _conversationStateLock.EnterWriteLock();
            try
            {
                string clientSpecificKey = userId + ":" + clientId;
                queryLogger.Log("Clearing client-specific conversation state " + clientSpecificKey, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                if (_clientSpecificStates.ContainsKey(clientSpecificKey))
                {
                    _clientSpecificStates.Remove(clientSpecificKey);
                    returnVal = true;
                }

                return returnVal;
            }
            finally
            {
                _conversationStateLock.ExitWriteLock();
            }
        }

        public async Task<bool> ClearBothStates(string userId, string clientId, ILogger queryLogger, bool fireAndForget)
        {
            if (string.IsNullOrEmpty(userId))
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                throw new ArgumentNullException("User ID");
            }
            else if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("Client ID");
            }

            bool returnVal = false;

            _conversationStateLock.EnterWriteLock();
            try
            {
                // Clear only roaming state
                queryLogger.Log("Clearing roaming conversation state " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                if (_roamingStates.ContainsKey(userId))
                {
                    _roamingStates.Remove(userId);
                    returnVal = true;
                }

                // Clear only client-specific state
                string clientSpecificKey = userId + ":" + clientId;
                queryLogger.Log("Clearing client-specific conversation state " + clientSpecificKey, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                if (_clientSpecificStates.ContainsKey(clientSpecificKey))
                {
                    _clientSpecificStates.Remove(clientSpecificKey);
                    returnVal = true;
                }

                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_SessionClear, 0), LogLevel.Ins);
                return returnVal;
            }
            finally
            {
                _conversationStateLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _conversationStateLock?.Dispose();
            }
        }
    }
}
