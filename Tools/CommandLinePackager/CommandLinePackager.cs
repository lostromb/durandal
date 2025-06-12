using Durandal.Common.Packages;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Net;
using Durandal.Common.NLP.Train;
using Durandal.Common.File;
using Durandal.Common.Tasks;
using Durandal.Common.Net.Http;
using Durandal.Common.Compression;
using Durandal.Common.Compression.Zip;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.NLP.Language;
using Durandal.Common.Utils;
using Durandal.Common.Instrumentation;
using Durandal.API;
using Durandal.Common.Net.Http2;
using Durandal.Common.ServiceMgmt;

namespace CommandLinePackager
{
    public class CommandLinePackager
    {
        public static int Main(string[] args)
        {
            // Echo args
            Console.WriteLine(Environment.CommandLine);
            if (args.Length == 4 && args[0].Equals("/CreateFromProject", StringComparison.OrdinalIgnoreCase))
            {
                DirectoryInfo projectPath = new DirectoryInfo(args[1]);
                FileInfo pluginDllPath = new FileInfo(args[2]);
                FileInfo outputFilename = new FileInfo(args[3]);
                return CreateProjectPackage(projectPath, pluginDllPath, outputFilename).Await();
            }
            else if (args.Length == 3 && args[0].Equals("/CreateLU", StringComparison.OrdinalIgnoreCase))
            {
                DirectoryInfo projectPath = new DirectoryInfo(args[1]);
                LanguageCode locale = LanguageCode.Parse(args[2]);
                return CreateLUPackages(projectPath, locale).Await();
            }
            else if (args.Length == 3 && args[0].Equals("/Upload", StringComparison.OrdinalIgnoreCase))
            {
                FileInfo packageFileName = new FileInfo(args[1]);
                string remoteHost = args[2];
                return UploadPackage(packageFileName, remoteHost).Await();
            }
            //else if (args.Length == 3 && args[0].Equals("/Bundle", StringComparison.OrdinalIgnoreCase))
            //{
            //    DirectoryInfo sourcePath = new DirectoryInfo(args[1]);
            //    FileInfo targetFileName = new FileInfo(args[2]);
            //    return CreateBundle(sourcePath, targetFileName).Await();
            //}
            else
            {
                Console.WriteLine("Usage: CommandLinePackager.exe");
                Console.WriteLine("    /CreateFromProject (PROJECT_PATH) (PLUGIN DLL PATH) (OUTPUT FILENAME)");
                Console.WriteLine("        Creates a new package based around a specific plugin file and all its dependents");
                Console.WriteLine("        This is typically used by MSBuild to automatically package projects during build");
                Console.WriteLine("    /CreateLU (PROJECT_PATH) (LOCALE)");
                Console.WriteLine("        Constructs a set of LU-only packages divided by domain.");
                Console.WriteLine("        You would typically use this if you are working on a model that is hosted remotely");
                //Console.WriteLine("    /Upload (PACKAGE_FILE_PATH) (REMOTE_HOST)");
                //Console.WriteLine("        Uploads a single package file to an LU host, possibly reloading it depending on the remote config");
                Console.WriteLine("    /Bundle (DIRECTORY) (OUTPUT_PATH)");
                Console.WriteLine("        Bundles a single directory recursively into a single output file (very similar to a .zip file)");
                Console.WriteLine();
                Console.WriteLine("Args recieved:");
                for (int c = 0; c < args.Length; c++)
                {
                    Console.WriteLine((c + 1).ToString() + ": " + args[c]);
                }

                return -1;
            }
        }

        private static async Task<int> CreateProjectPackage(DirectoryInfo projectPath, FileInfo pluginDllPath, FileInfo outputFileName)
        {
            if (!pluginDllPath.Exists)
            {
                Console.WriteLine("Plugin file " + pluginDllPath.FullName + " doesn't exist!");
                return -2;
            }
            if (!projectPath.Exists)
            {
                Console.WriteLine("Project directory " + projectPath.FullName + " doesn't exist!");
                return -3;
            }

            // For now, the plugin DLL path must be in a subdirectory of the project path
            if (!pluginDllPath.FullName.StartsWith(projectPath.FullName))
            {
                Console.WriteLine("The plugin file " + pluginDllPath.FullName + " must be within a subdirectory of " + projectPath.FullName);
                return -4;
            }

            ILogger logger = new ConsoleLogger("DurandalPackager");
            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Starting to build package for the project {0}...", projectPath.FullName);

            IFileSystem pluginResourceManager = new RealFileSystem(logger, pluginDllPath.DirectoryName);
            VirtualPath pluginVirtualFile = new VirtualPath(pluginDllPath.Name);

            IFileSystem projectFileSystem = new RealFileSystem(logger, projectPath.FullName);

            ManifestFactory manifestFactory = new ManifestFactory(logger, projectFileSystem, VirtualPath.Root, new PluginReflector());
            PackageManifest manifest = await manifestFactory.BuildManifest(pluginResourceManager, pluginVirtualFile).ConfigureAwait(false);
            PackageFactory factory = new PackageFactory(logger, projectFileSystem, VirtualPath.Root);
            if (!outputFileName.Directory.Exists)
            {
                outputFileName.Directory.Create();
            }

            VirtualPath pluginFile = new VirtualPath(pluginDllPath.FullName.Substring(projectPath.FullName.Length));
            RealFileSystem targetFileSystem = new RealFileSystem(NullLogger.Singleton, outputFileName.DirectoryName);
            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Building package file {0}", outputFileName.FullName);
            factory.BuildPackage(manifest, targetFileSystem, new VirtualPath(outputFileName.Name), pluginFile).Await();
            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Finished packing {0}", outputFileName.FullName);
            return 0;
        }

        private static async Task<int> CreateLUPackages(DirectoryInfo projectPath, LanguageCode locale)
        {
            if (!projectPath.Exists)
            {
                Console.WriteLine("Project directory " + projectPath.FullName + " doesn't exist!");
                return -3;
            }

            ILogger logger = new ConsoleLogger();
            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Starting to build LU package for directory {0}...", projectPath.FullName);
            
            // Find the set of all domains in the project
            IFileSystem projectFileSystem = new RealFileSystem(logger, projectPath.FullName);
            ISet<string> allDomains = TrainingDataManager.GetAllKnownDomains(locale, projectFileSystem, logger);
            logger.Log("Building LU packages for the following domains: " + string.Join(",", allDomains));
            foreach (string domain in allDomains)
            {
                ManifestFactory manifestFactory = new ManifestFactory(logger, projectFileSystem, VirtualPath.Root, new PluginReflector());
                PackageManifest manifest = await manifestFactory.BuildLUManifest(domain).ConfigureAwait(false);
                PackageFactory factory = new PackageFactory(logger, projectFileSystem, VirtualPath.Root);

                FileInfo outputFileName = new FileInfo(domain + "_" + locale.ToBcp47Alpha2String() + ".dupkg");

                RealFileSystem targetFileSystem = new RealFileSystem(NullLogger.Singleton, outputFileName.DirectoryName);
                logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Building package file {0}", outputFileName.FullName);
                factory.BuildPackage(manifest, targetFileSystem, new VirtualPath(outputFileName.Name)).Await();
            }

            logger.Log("Finished!");

            return 0;
        }

        private static async Task<int> UploadPackage(FileInfo packageFileName, string remoteHost)
        {
            ConsoleLogger logger = new ConsoleLogger();
            
            if (!packageFileName.Exists)
            {
                logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "Error: The file {0} doesn't exist!", packageFileName.FullName);
                return -1;
            }

            byte[] packageBytes = File.ReadAllBytes(packageFileName.FullName);

            // Upload an LU package to the local server
            HttpRequest req = HttpRequest.CreateOutgoing("/install", "POST");
            req.SetContent(packageBytes, HttpConstants.MIME_TYPE_OCTET_STREAM);
            string packageNameWithoutExtensions = packageFileName.Name;
            packageNameWithoutExtensions = packageFileName.Name.Remove(Math.Max(1, packageFileName.Name.Length - packageFileName.Extension.Length));

            req.GetParameters.Add("package", packageNameWithoutExtensions);
            req.RequestHeaders.Add("Host", remoteHost);

            TcpConnectionConfiguration tcpConfig = new TcpConnectionConfiguration()
            {
                DnsHostname = remoteHost,
                Port = 62291
            };

            using (ISocketFactory socketFactory = new TcpClientSocketFactory(logger.Clone("PackagerSocketFactory")))
            {
                SocketHttpClient client = new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(socketFactory),
                    tcpConfig,
                    logger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    Http2SessionManager.Default,
                    new Http2SessionPreferences());
                logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Uploading {0} to {1}...", packageFileName.Name, client.ServerAddress);
                using (NetworkResponseInstrumented<HttpResponse> response = await client.SendInstrumentedRequestAsync(req, CancellationToken.None, queryLogger: logger).ConfigureAwait(false))
                {
                    if (response.Response == null)
                    {
                        logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Got response: {0}", response.Response.ResponseCode);
                    }
                    else if (response.Response.ResponseCode != 200)
                    {
                        logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "Got error response {0}: {1}", response.Response.ResponseCode, response.Response.ResponseMessage);
                        logger.Log(await response.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false), LogLevel.Err);
                    }
                    else
                    {
                        logger.Log("Upload OK!");
                    }

                    await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }
            }

            return 0;
        }

        private static async Task<int> CreateBundle(DirectoryInfo inputPath, FileInfo outputFilename)
        {
            if (!inputPath.Exists)
            {
                Console.WriteLine("Directory " + inputPath.FullName + " doesn't exist!");
                return -1;
            }

            ILogger logger = new ConsoleLogger();
            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Starting to bundle the directory {0} to package {1}", inputPath.FullName, outputFilename.FullName);
            
            if (File.Exists(outputFilename.FullName))
            {
                File.Delete(outputFilename.FullName);
            }

            RealFileSystem inputRealFileSystem = new RealFileSystem(logger.Clone("RealFileSystem") , inputPath.FullName);
            RealFileSystem outputRealFileSystem = new RealFileSystem(logger.Clone("RealFileSystem"), outputFilename.DirectoryName);
            using (ZipFileFileSystem zipFileSystem = new ZipFileFileSystem(logger.Clone("ZipFileSystem"), outputRealFileSystem, new VirtualPath(outputFilename.Name)))
            {
                await FileHelpers.CopyAllFilesAsync(inputRealFileSystem, VirtualPath.Root, zipFileSystem, VirtualPath.Root, logger, true).ConfigureAwait(false);
            }

            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Finished packing {0}", outputFilename.FullName);
            return 0;
        }
    }
}
