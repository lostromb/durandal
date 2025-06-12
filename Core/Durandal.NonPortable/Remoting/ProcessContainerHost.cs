namespace Durandal.Common.Remoting
{
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.Common.Utils.NativePlatform;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class ProcessContainerHost : GenericContainerHost
    {
        private const int PROCESS_GRACEFUL_SHUTDOWN_TIME_MS = 10000;
        private static readonly string CONTAINER_EXE_NAME = "Durandal.ContainerizedRuntime.exe";
        private static readonly string CONTAINER_DLL_NAME = "Durandal.ContainerizedRuntime.dll";

        private readonly ContainerRuntimeInformation _runtimeInfo;
        private readonly DirectoryInfo _durandalRootDirectory;
        private readonly DirectoryInfo _containerRuntimeDirectory;
        private readonly DirectoryInfo _containerDirectory;
        private readonly string _serviceNameForDebug;
        private readonly bool _useDedicatedPostOfficeThread;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly CancellationTokenSource _cancelTokenSource;
#pragma warning restore CA2213 // This gets disposed in DestroyInternalContainer()
        private readonly CancellationToken _cancelToken;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2213:Dispose objects before losing scope",
            Justification = "Disposal semantics are different here, the base class calls DestroyInternalContainer() to dispose of things here")]
        private Process _container;
        private StreamWriter _containerStdIn;
        private StreamReader _containerStdOut;

        public ProcessContainerHost(
            ContainerHostInitializationParameters containerParameters,
            DirectoryInfo dialogServiceDirectory,
            DirectoryInfo containerRuntimeDirectory,
            DirectoryInfo containerDirectory)
            : base(containerParameters)
        {
            _durandalRootDirectory = dialogServiceDirectory;
            _containerDirectory = containerDirectory;
            _containerRuntimeDirectory = containerRuntimeDirectory;
            _serviceNameForDebug = containerParameters.ServiceName;
            _useDedicatedPostOfficeThread = containerParameters.RemotingConfig.UseDedicatedIpcThreads;
            _runtimeInfo = containerParameters.RuntimeInformation.AssertNonNull(nameof(containerParameters.RuntimeInformation));
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = _cancelTokenSource.Token;

            if (!_containerDirectory.Exists)
            {
                _containerDirectory.Create();
            }
        }

        ~ProcessContainerHost()
        {
            Dispose(false);
        }

        protected override Task<bool> CreateInternalContainer(
            ILogger logger,
            string containerName,
            string serviceName,
            DimensionSet containerDimensions,
            uint containerPermanentMailboxId,
            string socketConnectionString,
            bool useDebugTimeouts,
            string remoteProtocolName)
        {
            ProcessStartInfo setup = new ProcessStartInfo();

            setup.CreateNoWindow = true;
            setup.WindowStyle = ProcessWindowStyle.Normal;
            setup.RedirectStandardInput = true;
            setup.RedirectStandardOutput = true;
            setup.UseShellExecute = false;
            setup.WorkingDirectory = _containerRuntimeDirectory.FullName;

            string containerExecutablePath;
            if (string.Equals(_runtimeInfo.RuntimeFramework, DialogRuntimeFramework.RUNTIME_NETFRAMEWORK))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Run the container .exe directly.
                    containerExecutablePath = _containerRuntimeDirectory.FullName + Path.DirectorySeparatorChar + CONTAINER_EXE_NAME;
                    setup.FileName = containerExecutablePath;
                    logger.Log($"Running NetFramework process container {containerExecutablePath}");
                }
                else
                {
                    // Hmmmmm, NetFramework container but not running on Windows....
                    // Assume Mono runtime is intended here and make a best effort
                    containerExecutablePath = _containerRuntimeDirectory.FullName + Path.DirectorySeparatorChar + CONTAINER_EXE_NAME;
                    setup.FileName = "mono";
                    setup.Arguments = containerExecutablePath;
                    logger.Log($"Using Mono to run NetFramework process container {containerExecutablePath}");
                }
            }
            else
            {
                // Outside of windows, use dotnet exec on the container .dll
                containerExecutablePath = _containerRuntimeDirectory.FullName + Path.DirectorySeparatorChar + CONTAINER_DLL_NAME;
                setup.FileName = "dotnet";
                setup.Arguments = $"exec \"{containerExecutablePath}\"";
                logger.Log($"Running NetCore process container using dotnet exec \"{containerExecutablePath}\"");
            }

            if (!File.Exists(containerExecutablePath))
            {
                logger.Log("Could not find entry point program " + containerExecutablePath + " to use to create container guest! It looks like this runtime version is broken or does not exist", LogLevel.Err);
                return Task.FromResult(false);
            }

            List<MetricDimension> modifiedDimensions = new List<MetricDimension>();
            foreach (var dimension in containerDimensions)
            {
                // Don't pass service name or service version to the container; it will set those itself
                if (!string.Equals(CommonInstrumentation.Key_Dimension_ServiceName, dimension.Key, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(CommonInstrumentation.Key_Dimension_ServiceVersion, dimension.Key, StringComparison.OrdinalIgnoreCase))
                {
                    modifiedDimensions.Add(dimension);
                }
            }

            ProcessContainerGuestInitializationParams initParameters = new ProcessContainerGuestInitializationParams()
            {
                ContainerName = serviceName,
                SocketConnectionString = socketConnectionString,
                ContainerLevelMessageBoxId = containerPermanentMailboxId,
                UseDebugTimeouts = useDebugTimeouts,
                ContainerDimensions = new DimensionSet(modifiedDimensions.ToArray()),
                ContainerDirectory = _containerDirectory.FullName,
                DurandalBaseDirectory = _durandalRootDirectory.FullName,
                ParentProcessId = Process.GetCurrentProcess().Id,
                UseDedicatedPostOfficeThread = _useDedicatedPostOfficeThread,
                RemoteProtocolName = remoteProtocolName
            };

            string base64Args = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(initParameters)));
            if (string.IsNullOrEmpty(setup.Arguments))
            {
                setup.Arguments = setup.Arguments + " " + base64Args;
            }
            else
            {
                setup.Arguments = base64Args;
            }

            _container = Process.Start(setup);
            if (_container != null && !_container.HasExited)
            {
                _containerStdIn = _container.StandardInput;
                _containerStdOut = _container.StandardOutput;

                // Start a diagnostic background task to read stdout from the process
                DurandalTaskExtensions.LongRunningTaskFactory.StartNew(() =>
                {
                    return MonitorProcessStdOut(_container, _cancelToken, _containerStdOut, logger.Clone($"ContainerGuest-{initParameters.ContainerName}"));
                });
                

                logger.Log("Process guest \"" + setup.FileName + "\" started successfully with pid " + _container.Id);
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        protected override void StopInternalContainer(ILogger logger)
        {
            try
            {
                logger.Log("Stopping process " + _serviceNameForDebug + " with pid " + _container.Id + "...");
                _cancelTokenSource.Cancel();
                _containerStdIn.Write("EXIT");
                _container.WaitForExit(PROCESS_GRACEFUL_SHUTDOWN_TIME_MS);
                _container.Refresh();
                if (!_container.HasExited)
                {
                    logger.Log("Forcibly killing process " + _serviceNameForDebug + " with pid " + _container.Id + "...", LogLevel.Wrn);
                    _container.Kill();
                }

                logger.Log("Container process stopped");
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Wrn);
            }
        }

        // We don't need a Dispose method on this class because the base class will invoke this method to dispose of things for us.
        protected override void DestroyInternalContainer(ILogger logger)
        {
            try
            {
                _cancelTokenSource.Cancel();
                _container?.Dispose();
                _container = null;
                _containerStdIn?.Dispose();
                _containerStdIn = null;
                _containerStdOut?.Dispose();
                _containerStdOut = null;
                logger.Log("Attempting to delete container directory " + _containerDirectory.Name + "...");
                _containerDirectory.Delete(true);
                _cancelTokenSource.Dispose();
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Wrn);
            }
        }

        private static async Task MonitorProcessStdOut(Process container, CancellationToken cancelToken, StreamReader stdOut, ILogger guestConsoleLogger)
        {
            Regex consoleLogExtractor = new Regex("\\[[\\d:]+\\] \\[(.):.+?\\] (.+)");
            try
            {
                while (!container.HasExited &&
                    !cancelToken.IsCancellationRequested &&
                    !stdOut.EndOfStream)
                {
                    string programOutputLine = await stdOut.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(programOutputLine))
                    {
                        continue;
                    }

                    Match regexMatch = consoleLogExtractor.Match(programOutputLine);
                    if (regexMatch.Success)
                    {
                        LogLevel level = LoggingLevelManipulators.ParseLevelChar(regexMatch.Groups[1].Value);
                        if (level == LogLevel.None)
                        {
                            level = LogLevel.Vrb;
                        }

                        guestConsoleLogger.Log(regexMatch.Groups[2].Value, level);
                    }
                    else
                    {
                        //guestConsoleLogger.Log("The process logger output regex doesn't seem to be matching properly", LogLevel.Wrn);
                        guestConsoleLogger.Log(programOutputLine, LogLevel.Vrb);
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
