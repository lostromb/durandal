using Durandal.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Event generated when a plugin is registered by the system
    /// </summary>
    public class PluginRegisteredEventArgs : EventArgs
    {
        public PluginStrongName PluginId;
        public int LoadedPluginCount;
    }
}
