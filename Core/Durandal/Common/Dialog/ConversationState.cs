namespace Durandal.Common.Dialog
{
    using Durandal.API;
    using Durandal.Common.Collections;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Logger;
    using Durandal.Common.Time;
    using Runtime;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The non-serializable form of ConversationState, used when we are actually executing a plugin logic
    /// </summary>
    public class ConversationState
    {
        /// <summary>
        /// The current turn index
        /// </summary>
        public int TurnNum
        {
            get;
            private set;
        }

        /// <summary>
        /// Counts how many times the retry handler was called on the current node since the last successful turn
        /// </summary>
        public int RetryNum
        {
            get;
            private set;
        }

        /// <summary>
        /// A list of previous turns in the conversation. The OLDEST turn is the FIRST in the list (in other words, the order of objects in this
        /// list is the same as the order in which the conversation was processed).
        /// Objects in this list will be pruned to remove old turns if it grows too large
        /// </summary>
        public List<RecoResult> PreviousConversationTurns
        {
            get;
            private set;
        }

        public IConversationTree ConversationTree
        {
            get;
            private set;
        }

        public string CurrentPluginDomain
        {
            get;
            private set;
        }

        public MultiTurnBehavior LastMultiturnState
        {
            get;
            private set;
        }

        public string CurrentNode
        {
            get;
            private set;
        }

        public DateTimeOffset ConversationExpireTime
        {
            get;
            private set;
        }

        public InMemoryDataStore SessionStore
        {
            get;
            private set;
        }

        public string ExplicitContinuation
        {
            get;
            private set;
        }

        public string CurrentPluginId
        {
            get;
            private set;
        }

        public Version CurrentPluginVersion
        {
            get;
            private set;
        }

        public ConversationState()
        {
            PreviousConversationTurns = new List<RecoResult>();
            TurnNum = 0;
            ConversationTree = null;
            CurrentPluginDomain = string.Empty;
            CurrentNode = null;
            LastMultiturnState = MultiTurnBehavior.None;
            ConversationExpireTime = DateTimeOffset.MinValue;
            SessionStore = new InMemoryDataStore();
            CurrentPluginId = string.Empty;
            CurrentPluginVersion = new Version(0, 0);
        }

        /// <summary>
        /// Constructs an internal conversation state from a serialized one
        /// </summary>
        /// <param name="bond"></param>
        /// <param name="queryLogger"></param>
        private ConversationState(SerializedConversationState bond, ILogger queryLogger)
        {
            this.TurnNum = bond.TurnNum;
            this.RetryNum = bond.RetryNum;
            this.PreviousConversationTurns = bond.PreviousConversationTurns ?? new List<RecoResult>();
            this.CurrentPluginDomain = bond.CurrentPluginDomain ?? string.Empty;
            this.LastMultiturnState = bond.LastMultiturnState ?? MultiTurnBehavior.None;
            this.ConversationExpireTime = new DateTimeOffset(bond.ConversationExpireTime, TimeSpan.Zero);
            this.CurrentPluginId = bond.CurrentPluginId;
            this.CurrentPluginVersion = new Version(bond.CurrentPluginVersionMajor, bond.CurrentPluginVersionMinor);
            if (!string.IsNullOrEmpty(this.CurrentPluginDomain))
            {
                if (!string.IsNullOrEmpty(bond.NextContinuationFuncName))
                {
                    this.ExplicitContinuation = bond.NextContinuationFuncName;
                }
                else
                {
                    // only load the conversation tree if no explicit delegate is specified
                    if (!string.IsNullOrEmpty(bond.CurrentConversationNode))
                    {
                        this.CurrentNode = bond.CurrentConversationNode;
                    }
                }
            }

            this.SessionStore = bond.SessionStore;

            // backwards compatability
            if (string.IsNullOrEmpty(this.CurrentPluginId))
            {
                queryLogger.Log("No plugin ID specified in state; assuming it is the same as LU domain " + this.CurrentPluginDomain, LogLevel.Wrn);
                this.CurrentPluginId = this.CurrentPluginDomain;
            }
        }

        public static ConversationState Deserialize(SerializedConversationState bond, ILogger queryLogger)
        {
            return new ConversationState(bond, queryLogger);
        }

        /// <summary>
        /// Converts this native C# session state to a serializable one, for persistence
        /// </summary>
        /// <returns></returns>
        public SerializedConversationState Serialize()
        {
            SerializedConversationState returnVal = new SerializedConversationState();
            returnVal.TurnNum = this.TurnNum;
            returnVal.RetryNum = this.RetryNum;
            returnVal.PreviousConversationTurns = this.PreviousConversationTurns;
            returnVal.CurrentPluginDomain = this.CurrentPluginDomain;
            returnVal.LastMultiturnState = this.LastMultiturnState;
            returnVal.ConversationExpireTime = this.ConversationExpireTime.ToUniversalTime().Ticks;
            if (!string.IsNullOrEmpty(this.CurrentNode))
            {
                returnVal.CurrentConversationNode = this.CurrentNode;
            }
            else
            {
                returnVal.CurrentConversationNode = null;
            }
            returnVal.SessionStore = this.SessionStore;
            if (this.ExplicitContinuation == null)
            {
                returnVal.NextContinuationFuncName = string.Empty;
            }
            else
            {
                returnVal.NextContinuationFuncName = this.ExplicitContinuation;
            }

            returnVal.CurrentPluginId = this.CurrentPluginId;
            returnVal.CurrentPluginVersionMajor = this.CurrentPluginVersion.Major;
            returnVal.CurrentPluginVersionMinor = this.CurrentPluginVersion.Minor;

            return returnVal;
        }

        public string GetNextTurnContinuation(string domain, string intent)
        {
            if (ConversationTree == null)
                return ExplicitContinuation; // fall back to explicit continuation when tree is not present
            return ConversationTree.GetNextContinuation(CurrentNode, domain, intent);
        }

        public string GetRetryContinuation()
        {
            if (ConversationTree == null)
                return null;
            if (CurrentNode == null)
                return null;
            return ConversationTree.GetRetryHandlerName(CurrentNode);
        }

        public bool IsExpired(IRealTimeProvider realTime)
        {
            return realTime.Time > ConversationExpireTime;
        }

        internal void UpdateSessionStore(InMemoryDataStore newSessionStore)
        {
            SessionStore = newSessionStore;
        }

        private void TransitionCommon(RecoResult lastRecoResult, MultiTurnBehavior nextTurnBehavior, string pluginId, Version pluginVersion, IRealTimeProvider realTime, int maxConversationLength)
        {
            TurnNum += 1;

            // Prune the conversation carryover history to a reasonable limit
            PreviousConversationTurns.Add(lastRecoResult);
            if (PreviousConversationTurns.Count > maxConversationLength)
            {
                // Item 0 in the list is always the oldest turn
                PreviousConversationTurns.RemoveAt(0);
            }

            if (string.IsNullOrEmpty(CurrentPluginDomain))
            {
                CurrentPluginDomain = lastRecoResult.Domain;
            }
            if (string.IsNullOrEmpty(CurrentPluginId))
            {
                CurrentPluginId = pluginId;
            }
            if (CurrentPluginVersion.Major == 0 &&
                CurrentPluginVersion.Minor == 0 &&
                pluginVersion != null)
            {
                CurrentPluginVersion = pluginVersion;
            }

            LastMultiturnState = nextTurnBehavior;
            ConversationExpireTime = realTime.Time.AddSeconds(nextTurnBehavior.ConversationTimeoutSeconds);
        }

        public void TransitionToContinuation(
            RecoResult lastRecoResult,
            MultiTurnBehavior nextTurnBehavior,
            ILogger logger,
            int maxConversationHistoryLength,
            IRealTimeProvider realTime,
            string nextContinuation,
            string pluginId,
            Version pluginVersion)
        {
            TransitionCommon(lastRecoResult, nextTurnBehavior, pluginId, pluginVersion, realTime, maxConversationHistoryLength);

            ExplicitContinuation = nextContinuation;

            // Nuke the convo tree since the user apparently wants to use explicit continuations
            ConversationTree = null;
        }

        public void TransitionToConversationNode(
            RecoResult lastRecoResult,
            MultiTurnBehavior nextTurnBehavior,
            ILogger logger,
            int maxConversationHistoryLength,
            IRealTimeProvider realTime,
            string targetConversationNode = null,
            string pluginId = null, 
            Version pluginVersion = null)
        {
            TransitionCommon(lastRecoResult, nextTurnBehavior, pluginId, pluginVersion, realTime, maxConversationHistoryLength);

            // If this was a retry (no reco received), increment the retry count
            if (lastRecoResult.Domain.Equals(DialogConstants.COMMON_DOMAIN) && lastRecoResult.Intent.Equals(DialogConstants.NORECO_INTENT))
            {
                RetryNum++;
            }
            else
            {
                RetryNum = 0;
            }

            // And move along in the tree
            if (ConversationTree != null)
            {
                // See if the client wanted to teleport to any specific node
                if (!string.IsNullOrWhiteSpace(targetConversationNode))
                {
                    CurrentNode = targetConversationNode;
                    if (string.IsNullOrEmpty(CurrentNode))
                    {
                        logger.Log(string.Format("Attempted to traverse to conversation node \"{0}\" in tree \"{1}\", but the node does not exist!",
                                                 targetConversationNode, CurrentPluginDomain), LogLevel.Err);
                    }
                }
                else
                {
                    // Just transition based on the set of edges from the current node. The tree will handle this.
                    // If the transition is not possible, the current node will be null and we'll get a warning
                    CurrentNode = ConversationTree.Transition(CurrentNode, lastRecoResult.Domain, lastRecoResult.Intent);
                    if (string.IsNullOrEmpty(CurrentNode) && !DialogConstants.COMMON_DOMAIN.Equals(lastRecoResult.Domain))
                    {
                        logger.Log(string.Format("Attempted an invalid conversation state transition {0}/{1}",
                                                 lastRecoResult.Domain, lastRecoResult.Intent), LogLevel.Wrn);
                    }
                }
            }
        }

        public ConversationState Clone()
        {
            ConversationState returnVal = new ConversationState()
            {
                TurnNum = this.TurnNum,
                ConversationTree = this.ConversationTree,
                CurrentPluginDomain = this.CurrentPluginDomain,
                LastMultiturnState = this.LastMultiturnState,
                CurrentNode = this.CurrentNode,
                ConversationExpireTime = this.ConversationExpireTime,
                RetryNum = this.RetryNum,
                CurrentPluginId = this.CurrentPluginId,
                CurrentPluginVersion = new Version(this.CurrentPluginVersion.Major, this.CurrentPluginVersion.Minor)
            };

            returnVal.PreviousConversationTurns.FastAddRangeList(this.PreviousConversationTurns);
            return returnVal;
        }

        internal void SetConversationTree(IConversationTree tree)
        {
            this.ConversationTree = tree;
        }

        #region Helpers for serialization

        public static List<T> StackToList<T>(Stack<T> stack)
        {
            List<T> returnVal = new List<T>();
            foreach (var x in stack)
            {
                returnVal.Add(x);
            }

            return returnVal;
        }

        public static Stack<T> ListToStack<T>(List<T> list)
        {
            Stack<T> returnVal = new Stack<T>();
            for (int c = list.Count - 1; c >= 0; c--)
            {
                returnVal.Push(list[c]);
            }

            return returnVal;
        }

        public static Stack<SerializedConversationState> ConvertStack(Stack<ConversationState> stack)
        {
            List<ConversationState> linearStates = StackToList(stack);
            List<SerializedConversationState> linearInternalStates = new List<SerializedConversationState>();
            foreach (var state in linearStates)
            {
                linearInternalStates.Add(state.Serialize());
            }

            Stack<SerializedConversationState> returnVal = ListToStack(linearInternalStates);
            return returnVal;
        }

        public static Stack<ConversationState> ConvertStack(Stack<SerializedConversationState> stack)
        {
            List<SerializedConversationState> linearStates = StackToList(stack);
            List<ConversationState> linearInternalStates = new List<ConversationState>();
            foreach (var state in linearStates)
            {
                linearInternalStates.Add(ConversationState.Deserialize(state, NullLogger.Singleton));
            }

            Stack<ConversationState> returnVal = ListToStack(linearInternalStates);
            return returnVal;
        }

        #endregion
    }
}
