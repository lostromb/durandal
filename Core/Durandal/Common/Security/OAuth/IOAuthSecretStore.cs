using Durandal.API;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.OAuth
{
    public interface IOAuthSecretStore : IDisposable
    {
        /// <summary>
        /// Upserts oauth state for a particular user + domain + config.
        /// The store is keyed on user ID + domain + unique ID + config name + config hash.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="traceId"></param>
        /// <returns></returns>
        Task SaveState(OAuthState state, Guid? traceId = null);

        /// <summary>
        /// Deletes the oauth sate with the specified ID
        /// </summary>
        /// <param name="stateId">The unique ID of the state to delete</param>
        /// <param name="traceId"></param>
        /// <returns></returns>
        Task DeleteState(string stateId, Guid? traceId = null);

        /// <summary>
        /// Attempts to retrieve the oauth state with the specified ID
        /// </summary>
        /// <param name="stateId">The unique ID of the state to retrieve</param>
        /// <param name="traceId"></param>
        /// <returns></returns>
        Task<RetrieveResult<OAuthState>> RetrieveState(string stateId, Guid? traceId = null);

        /// <summary>
        /// Attempts to retrieve the oauth state for the specified user, domain, and configuration.
        /// The retrieve will only succeed if the given config exactly matches the config that was previously saved (by hash comparison)
        /// </summary>
        /// <param name="durandalUserId">The user ID that is associated with the secret</param>
        /// <param name="durandalPlugin">The domain name that owns the secret</param>
        /// <param name="config">The oauth config that generated the state</param>
        /// <param name="traceId"></param>
        /// <returns></returns>
        Task<RetrieveResult<OAuthState>> RetrieveState(string durandalUserId, PluginStrongName durandalPlugin, OAuthConfig config, Guid? traceId = null);
    }
}
