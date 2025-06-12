using Durandal.Common.Config;
using Durandal.Common.LG;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// The few objects that persist in remoted plugin services in between invocations of a plugin
    /// </summary>
    public class CachedRemotePluginServicesConstants
    {
        public ILGEngine LanguageGenerator { get; set; }
        public IConfiguration PluginConfiguration { get; set; }
    }
}
