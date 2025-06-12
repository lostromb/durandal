using Durandal;
using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Logger;
using Durandal.Common.Test;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DialogTests.Plugins
{
    public static class PluginTestCommon
    {
        public static InqueTestParameters CreateTestParameters(DurandalPlugin plugin, string packageName, DirectoryInfo durandalEnvironmentDirectory)
        {
            ILogger logger = new AggregateLogger("UnitTest", null, new ConsoleLogger("UnitTest", LogLevel.All), new DebugLogger("UnitTest", LogLevel.All));
            if (!durandalEnvironmentDirectory.Exists)
            {
                throw new FileNotFoundException("Durandal environment directory \"" + durandalEnvironmentDirectory.FullName + "\" doesn't exist!");
            }

            DirectoryInfo packageDirectory = new DirectoryInfo(Path.Combine(durandalEnvironmentDirectory.FullName, "packages"));
            IFileSystem packageFileSystem = new WindowsFileSystem(logger, packageDirectory.FullName);
            string tempFolder = Path.Combine(Path.GetTempPath(), "InqueTestCache");
            Directory.CreateDirectory(tempFolder);

            IFileSystem cacheFileSystem = new WindowsFileSystem(logger.Clone("CacheFilesystem"), tempFolder);
            InqueTestParameters testConfig = new InqueTestParameters()
            {
                Logger = logger.Clone("TestDriver"),
                Plugins = new List<DurandalPlugin>() { plugin },
                PackageFileSystem = packageFileSystem,
                PackageFiles = new List<VirtualPath>()
                {
                    new VirtualPath(packageName),
                },
                LGScriptCompiler = new CodeDomLGScriptCompiler(),
                CacheFileSystem = cacheFileSystem
            };

            return testConfig;
        }
    }
}
