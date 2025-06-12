using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;

namespace Durandal.Common.Dialog.Services
{
    public interface IUserProfileStorage
    {
        /// <summary>
        /// Upserts the specified user profile objects. Writing a null value is equivalent to deletion.
        /// </summary>
        /// <param name="profilesToUpdate">The set of one or more profiles to update.</param>
        /// <param name="profiles">The collection of profiles.</param>
        /// <param name="userId">The user ID of the profile we're updating</param>
        /// <param name="domain">If updating a plugin-local profile, this is the domain associated with that profile.. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>True if the write succeeded</returns>
        Task<bool> UpdateProfiles(UserProfileType profilesToUpdate, UserProfileCollection profiles, string userId, string domain, ILogger queryLogger);

        /// <summary>
        /// Gets one or more profiles for the given user
        /// </summary>
        /// <param name="profilesToFetch">The set of profiles (local, global, etc.) to fetch</param>
        /// <param name="userId">The user ID of the profile we're fetching</param>
        /// <param name="domain">If retrieving plugin-local profile, this is the domain we want. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>A user profile collection containing the results</returns>
        Task<RetrieveResult<UserProfileCollection>> GetProfiles(UserProfileType profilesToFetch, string userId, string domain, ILogger queryLogger);

        /// <summary>
        /// Clears one or more profiles for a given user
        /// </summary>
        /// <param name="profilesToDelete">The set of profiles to delete</param>
        /// <param name="userId">The user ID</param>
        /// <param name="domain">If clearing a plugin-local profile, this is the domain associated with that profile.. Otherwise this may be null</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>True if the write succeeded</returns>
        Task<bool> ClearProfiles(UserProfileType profilesToDelete, string userId, string domain, ILogger queryLogger);
    }
}
