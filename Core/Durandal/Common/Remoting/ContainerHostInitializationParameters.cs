using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// Common parameters sent to each container host when it is initialized. The specific implementation may pass extra parameters alongside this, but
    /// these are the core ones shared by every implementation.
    /// </summary>
    public class ContainerHostInitializationParameters
    {
        /// <summary>
        /// The name of the package being hosted, e.g. "BasicPlugins". Not necessarily the name of any specific plugin.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// The unique name of this specific instance of the container, for example "BasicPlugins-4c4a2f5a2df84ecd9628e3bc3677329a".
        /// This is also identical to the file path of the container if it is isolated to a specific folder.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// Service logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// File system rooted in the main program directory.
        /// </summary>
        public IFileSystem GlobalFileSystem { get; set; }

        /// <summary>
        /// Directory in which plugin source folders can be found
        /// </summary>
        public VirtualPath PluginDirectory { get; set; }

        /// <summary>
        /// HTTP client factory that containers may use
        /// </summary>
        public IHttpClientFactory GlobalHttpClientFactory { get; set; }

        /// <summary>
        /// The protocol to use when communicating with the remote host, e.g. Bond
        /// </summary>
        public IRemoteDialogProtocol RemotingProtocol { get; set; }

        /// <summary>
        /// The socket implementation to use for IPC communication, e.g. anonymous pipe or memory-mapped file
        /// </summary>
        public IServerSocketFactory ServerSocketFactory { get; set; }

        /// <summary>
        /// If true, extend timeouts to several minutes to allow debuggers to step through.
        /// </summary>
        public bool UseDebugTimeouts { get; set; }

        /// <summary>
        /// Global metric collector for the program host.
        /// </summary>
        public WeakPointer<IMetricCollector> Metrics { get; set; }

        /// <summary>
        /// Metric dimensions specific to this container.
        /// </summary>
        public DimensionSet ContainerDimensions { get; set; }

        /// <summary>
        /// Directory for the runtime that this container should load
        /// </summary>
        public VirtualPath RuntimeDirectory { get; set; }

        /// <summary>
        /// Global configuration for remoting and containers
        /// </summary>
        public RemotingConfiguration RemotingConfig { get; set; }

        /// <summary>
        /// Information about the runtime (netcore, netfx..) which will be running in this container
        /// </summary>
        public ContainerRuntimeInformation RuntimeInformation { get; set; }
    }
}
