using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// Parameters which are sent to containerization guests and used to help establish its identity and connect it with the host.
    /// Think of it like, if the container is a CLI program, these are its command line parameters (in the case of process-isolated containers, this is literally what happens).
    /// </summary>
    public class ContainerGuestInitializationParameters
    {
        /// <summary>
        /// The friendly name of this container, e.g. "MyPlugin"
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// A connection string that specifies how this container should communicate with its host
        /// </summary>
        public string SocketConnectionString { get; set; }

        /// <summary>
        /// The post office box ID to use for container-level communication between the host and guest
        /// </summary>
        public uint ContainerLevelMessageBoxId { get; set; }

        /// <summary>
        /// If true, extend all timeouts to allow for developers to step through code and debug.
        /// </summary>
        public bool UseDebugTimeouts { get; set; }

        /// <summary>
        /// The base dimensions to use for all metrics reported by the container.
        /// </summary>
        public DimensionSet ContainerDimensions { get; set; }

        /// <summary>
        /// The directory path for the container-specific libraries; in other words, the custom plugin file that this container is loading.
        /// </summary>
        public string ContainerDirectory { get; set; }

        /// <summary>
        /// The directory path for the root of the durandal environment.
        /// </summary>
        public string DurandalBaseDirectory { get; set; }

        /// <summary>
        /// If true, use a dedicated background thread for post office reads.
        /// </summary>
        public bool UseDedicatedPostOfficeThread { get; set; }

        /// <summary>
        /// The name of the remoting protocol in use, usually either bond or json
        /// </summary>
        public string RemoteProtocolName { get; set; }
    }
}
