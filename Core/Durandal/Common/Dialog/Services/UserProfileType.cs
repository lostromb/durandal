using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// Identifies one or more specific types of profile that may be associated with a user
    /// </summary>
    [Flags]
    public enum UserProfileType
    {
        None = 0x0,

        /// <summary>
        /// The user profile associated with a particular plugin domain
        /// </summary>
        PluginLocal = 0x1 << 1,

        /// <summary>
        /// The user profile that is global to all plugins
        /// </summary>
        PluginGlobal = 0x1 << 2,

        /// <summary>
        /// The entity history that is global to all plugins
        /// </summary>
        EntityHistoryGlobal = 0x1 << 3,
    }
}
