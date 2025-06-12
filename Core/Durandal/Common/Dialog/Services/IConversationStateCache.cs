using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Time;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// Defines a cache for storing conversation state. Conversation states are divided into two subtypes:
    /// a "roaming" conversation state tied only to a User ID, and a "client-specific" conversation state that
    /// is associated with a user ID + client ID together. When fetching states, the client-specific one is preferred.
    /// <para>
    /// RETRIEVE
    /// Before dialog processing starts, call retrieve method with user ID and client ID
    /// If a state matches both, RETURN client-specific state
    /// If a state only matches user ID and empty client ID(roaming), then RETURN the roaming state
    /// Otherwise, RETURN an empty state
    /// </para>
    /// <para>
    /// STORE
    /// When a conversation turn completes successfully:
    /// - UPSERT client-specific state
    /// - If the input method is written/spoken, or the conversation does not continue, UPSERT roaming user state as well
    /// If the conversation turn fails with an error:
    /// - DELETE the client-specific state
    /// </para>
    /// <para>
    /// CLEAR
    /// When a client resets its UI, send a reset message to dialog service
    /// Dialog will then DELETE client-specific states only, leaving roaming state untouched
    /// </para>
    /// </summary>
    public interface IConversationStateCache : IDisposable
    {
        /// <summary>
        /// Sets a new client-associated conversation state
        /// </summary>
        /// <param name="userId">The User ID (not the client ID) for the user making the current request</param>
        /// <param name="clientId">The client id of the _device_ making the request.</param>
        /// <param name="newState">The updated conversation state</param>
        /// <param name="queryLogger">A logger for this particular query</param>
        /// <param name="fireAndForget">If true, ignore the result of the write</param>
        /// <returns>True if the write succeeded</returns>
        Task<bool> SetClientSpecificState(string userId, string clientId, Stack<ConversationState> newState, ILogger queryLogger, bool fireAndForget);

        /// <summary>
        /// Sets a new roaming conversation state
        /// </summary>
        /// <param name="userId">The User ID (not the client ID) for the user making the current request</param>
        /// <param name="newState">The updated conversation state</param>
        /// <param name="queryLogger">A logger for this particular query</param>
        /// <param name="fireAndForget">If true, ignore the result of the write</param>
        /// <returns>True if the write succeeded</returns>
        Task<bool> SetRoamingState(string userId, Stack<ConversationState> newState, ILogger queryLogger, bool fireAndForget);

        /// <summary>
        /// Attempt to retrieve the conversation state associated with the given user ID. If it is not found, this method will STORE a newly created
        /// conversation state and then return it along with FALSE.
        /// </summary>
        /// <param name="userId">The User ID (not the client ID) for the user making the current request</param>
        /// <param name="clientId">The client id of the _device_ making the request. If a client-specific state exists it will be retrieved, otherwise the cross-client state is used</param>
        /// <param name="queryLogger">A logger for this particular query</param>
        /// <param name="realTime">Definition of real time, used to determine when states expire</param>
        /// <returns>True if an existing state was found in the store, false if a new one was created</returns>
        Task<RetrieveResult<Stack<ConversationState>>> TryRetrieveState(string userId, string clientId, ILogger queryLogger, IRealTimeProvider realTime);

        /// <summary>
        /// Deletes the roaming user state, but leaves client-specific states intact
        /// </summary>
        /// <param name="userId">The user ID to be deleted</param>
        /// <param name="queryLogger">A logger</param>
        /// <param name="fireAndForget">If true, ignore the result of the write</param>
        /// <returns>True if the state was cleared successfully, false if one didn't exist in the first place</returns>
        Task<bool> ClearRoamingState(string userId, ILogger queryLogger, bool fireAndForget);

        /// <summary>
        /// Deletes the client-specific user state only, leaving roaming state intact
        /// </summary>
        /// <param name="userId">The user ID of the state to delete</param>
        /// <param name="clientId">The client ID of the state to delete</param>
        /// <param name="queryLogger">A logger</param>
        /// <param name="fireAndForget">If true, ignore the result of the write</param>
        /// <returns>True if the state was cleared successfully, false if one didn't exist in the first place</returns>
        Task<bool> ClearClientSpecificState(string userId, string clientId, ILogger queryLogger, bool fireAndForget);

        /// <summary>
        /// Deletes both the client-specific user state as well as the roaming user state
        /// </summary>
        /// <param name="userId">The user ID of the state to delete</param>
        /// <param name="clientId">The client ID of the state to delete</param>
        /// <param name="queryLogger">A logger</param>
        /// <param name="fireAndForget">If true, ignore the result of the write</param>
        /// <returns>True if the state was cleared successfully, false if one didn't exist in the first place</returns>
        Task<bool> ClearBothStates(string userId, string clientId, ILogger queryLogger, bool fireAndForget);
    }
}
