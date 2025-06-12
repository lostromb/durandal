using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Collections;

namespace Durandal.Common.Dialog.Services
{
    public class InMemoryProfileStorage : IUserProfileStorage
    {
        private FastConcurrentDictionary<string, InMemoryDataStore> _localProfiles;
        private FastConcurrentDictionary<string, InMemoryDataStore> _globalProfiles;
        private FastConcurrentDictionary<string, InMemoryEntityHistory> _entityHistories;

        public InMemoryProfileStorage()
        {
            _localProfiles = new FastConcurrentDictionary<string, InMemoryDataStore>();
            _globalProfiles = new FastConcurrentDictionary<string, InMemoryDataStore>();
            _entityHistories = new FastConcurrentDictionary<string, InMemoryEntityHistory>();
        }

        /// <summary>
        /// Upserts the specified user profile objects. Writing a null value is equivalent to deletion.
        /// </summary>
        /// <param name="profilesToUpdate">The set of one or more profiles to update.</param>
        /// <param name="profiles">The collection of profiles.</param>
        /// <param name="userId">The user ID of the profile we're updating</param>
        /// <param name="domain">If updating a plugin-local profile, this is the domain associated with that profile.. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>True if the write succeeded</returns>
        public Task<bool> UpdateProfiles(UserProfileType profilesToUpdate, UserProfileCollection profiles, string userId, string domain, ILogger queryLogger)
        {
            if (profilesToUpdate.HasFlag(UserProfileType.PluginLocal))
            {
                string localKey = userId + "_" + domain;
                if (_localProfiles.ContainsKey(localKey))
                {
                    _localProfiles.Remove(localKey);
                }

                if (profiles.LocalProfile != null)
                {
                    _localProfiles[localKey] = profiles.LocalProfile;
                }
            }

            if (profilesToUpdate.HasFlag(UserProfileType.PluginGlobal))
            {
                if (_globalProfiles.ContainsKey(userId))
                {
                    _globalProfiles.Remove(userId);
                }

                if (profiles.GlobalProfile != null)
                {
                    _globalProfiles[userId] = profiles.GlobalProfile;
                }
            }

            if (profilesToUpdate.HasFlag(UserProfileType.EntityHistoryGlobal))
            {
                if (_entityHistories.ContainsKey(userId))
                {
                    _entityHistories.Remove(userId);
                }

                if (profiles.EntityHistory != null)
                {
                    _entityHistories[userId] = profiles.EntityHistory;
                }
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Gets one or more profiles for the given user
        /// </summary>
        /// <param name="profilesToFetch">The set of profiles (local, global, etc.) to fetch</param>
        /// <param name="userId">The user ID of the profile we're fetching</param>
        /// <param name="domain">If retrieving plugin-local profile, this is the domain we want. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>A user profile collection containing the results</returns>
        public Task<RetrieveResult<UserProfileCollection>> GetProfiles(UserProfileType profilesToFetch, string userId, string domain, ILogger queryLogger)
        {
            string localKey = userId + "_" + domain;

            InMemoryDataStore local = null;
            InMemoryDataStore global = null;
            InMemoryEntityHistory globalHistory = null;

            if (profilesToFetch.HasFlag(UserProfileType.PluginLocal) &&
                _localProfiles.ContainsKey(localKey))
            {
                local = _localProfiles[localKey];
            }

            if (profilesToFetch.HasFlag(UserProfileType.PluginGlobal) &&
                _globalProfiles.ContainsKey(userId))
            {
                global = _globalProfiles[userId];
            }

            if (profilesToFetch.HasFlag(UserProfileType.EntityHistoryGlobal) &&
                _entityHistories.ContainsKey(userId))
            {
                globalHistory = _entityHistories[userId];
            }

            return Task.FromResult(new RetrieveResult<UserProfileCollection>(new UserProfileCollection(local, global, globalHistory)));
        }

        /// <summary>
        /// Clears one or more profiles for a given user
        /// </summary>
        /// <param name="profilesToDelete">The set of profiles to delete</param>
        /// <param name="userId">The user ID</param>
        /// <param name="domain">If clearing a plugin-local profile, this is the domain associated with that profile.. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>True if the write succeeded</returns>
        public Task<bool> ClearProfiles(UserProfileType profilesToDelete, string userId, string domain, ILogger queryLogger)
        {
            if (profilesToDelete.HasFlag(UserProfileType.PluginLocal))
            {
                string localKey = userId + "_" + domain;
                if (_localProfiles.ContainsKey(localKey))
                {
                    _localProfiles.Remove(localKey);
                }
            }

            if (profilesToDelete.HasFlag(UserProfileType.PluginGlobal) && _globalProfiles.ContainsKey(userId))
            {
                _globalProfiles.Remove(userId);
            }

            if (profilesToDelete.HasFlag(UserProfileType.EntityHistoryGlobal) && _entityHistories.ContainsKey(userId))
            {
                _entityHistories.Remove(userId);
            }

            return Task.FromResult(true);
        }

        public void ClearAllProfiles()
        {
            _localProfiles.Clear();
            _globalProfiles.Clear();
            _entityHistories.Clear();
        }
    }
}
