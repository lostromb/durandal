using Durandal;
using Durandal.API;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Common
{
    /// <summary>
    /// This stub exists only so that the package manager will pick up that we are defining the common domain LU in this package
    /// as well as installing the browser HTML view elements that are used when opening the dialog server from the web
    /// </summary>
    public class CommonPlugin : DurandalPlugin
    {
        public CommonPlugin() : base(DialogConstants.COMMON_DOMAIN, DialogConstants.COMMON_DOMAIN) { }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            return new PluginInformation()
            {
                InternalName = "Common",
                Creator = "Durandal",
                MajorVersion = 0,
                MinorVersion = 0,
                Hidden = true
            };
        }
    }
}
