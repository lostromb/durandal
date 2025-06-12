using Durandal.API;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog.Services
{
    public class UserProfileCollection
    {
        /// <summary>
        /// This is a persistent cache for data for this particular user. It is isolated by userId + domain,
        /// so each user within each domain will have a unique profile. It is normally used to store things like
        /// user preferences, access tokens, query history, and the like.
        /// </summary>
        public InMemoryDataStore LocalProfile;

        /// <summary>
        /// This is a persistent cache for data that is unique to this user and shared between all domains.
        /// This might contain things like the user's name and profile information, or global access tokens
        /// that can be used to connect to a user's online accounts.
        /// Generally it is read-only for all plugins except for reflection (internal)
        /// </summary>
        public InMemoryDataStore GlobalProfile;

        /// <summary>
        /// This object stores all entities that have been shared by plugins for a specific user.
        /// Entities are shared globally to all plugins and are available for the next few turns while
        /// they are assumed to be "relevant" (something like 10 turns usually)
        /// </summary>
        public InMemoryEntityHistory EntityHistory;

        public UserProfileCollection(InMemoryDataStore local, InMemoryDataStore global, InMemoryEntityHistory entityHistory)
        {
            LocalProfile = local;
            GlobalProfile = global;
            EntityHistory = entityHistory;
        }
    }
}
