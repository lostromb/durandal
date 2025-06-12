using Durandal.API;
using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Logger;
using Durandal.Common.LU;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Test
{
    public class InqueTestParameters
    {
        public ILogger Logger { get; set; }
        public IList<DurandalPlugin> Plugins { get; set; }
        public IFileSystem PackageFileSystem { get; set; }
        public IList<VirtualPath> PackageFiles { get; set; }
        public IRealTimeProvider TimeProvider { get; set; }
        public IList<FakeLUModel> FakeLUModels { get; set; }
        public string SideSpeechDomain { get; set; }
        public ILGScriptCompiler LGScriptCompiler { get; set; }
        public IFileSystem CacheFileSystem { get; set; }
        public IDialogTransportProtocol DialogTransportProtocol { get; set; }
        public ILUTransportProtocol LUTransportProtocol { get; set; }
        public InqueTestDriver.BuildPluginProviderDelegate PluginProviderFactory { get; set; }

        public InqueTestParameters()
        {
            Logger = NullLogger.Singleton;
            Plugins = new List<DurandalPlugin>();
            PackageFileSystem = NullFileSystem.Singleton;
            PackageFiles = new List<VirtualPath>();
            TimeProvider = DefaultRealTimeProvider.Singleton;
            FakeLUModels = new List<FakeLUModel>();
            SideSpeechDomain = DialogConstants.SIDE_SPEECH_DOMAIN;
            LGScriptCompiler = null;
            CacheFileSystem = NullFileSystem.Singleton;
            DialogTransportProtocol = new DialogJsonTransportProtocol();
            LUTransportProtocol = new LUJsonTransportProtocol();
            PluginProviderFactory = InqueTestDriver.BuildLocallyRemotedPluginProvider;
        }
    }
}
