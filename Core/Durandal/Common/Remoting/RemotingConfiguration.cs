using Durandal.Common.Config;
using Durandal.Common.Config.Accessors;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// Provides a view over an <see cref="IConfiguration"/> object that contains options specific to remoting and containerization
    /// </summary>
    public class RemotingConfiguration
    {
        private readonly WeakPointer<IConfiguration> _internal;

        public RemotingConfiguration(WeakPointer<IConfiguration> container)
        {
            _internal = container;
        }

        /// <summary>
        /// Determines the plugin loading method, which affects how plugin DLLs are discovered, hot-swapped, installed, etc. Valid options are "basic" (no containers), "locally_remoted" (basic but with remoting), "appdomain_isolated" (.Net Framework only), "loadcontext_isolated" (.Net Core only), "containerized" (selects either app domain or load context) and "process_isolated"
        /// </summary>
        public string PluginLoader
        {
            get
            {
                return _internal.Value.GetString("pluginLoader", "basic");
            }
            set
            {
                _internal.Value.Set("pluginLoader", value);
            }
        }

        /// <summary>
        /// Implementation to use for interprocess communication with containers. Valid options are "mmio", "pipe", or "tcp"
        /// </summary>
        public string RemotingPipeImplementation
        {
            get
            {
                return _internal.Value.GetString("remotingPipeImplementation", "mmio");
            }
            set
            {
                _internal.Value.Set("remotingPipeImplementation", value);
            }
        }

        /// <summary>
        /// The interval between each keepalive ping to each remoting container, or 0 to disable health monitoring
        /// </summary>
        public TimeSpan KeepAlivePingInterval
        {
            get
            {
                return _internal.Value.GetTimeSpan("keepAlivePingInterval", TimeSpan.FromSeconds(1));
            }
            set
            {
                _internal.Value.Set("keepAlivePingInterval", value);
            }
        }

        public IConfigValue<TimeSpan> KeepAlivePingIntervalAccessor(ILogger logger)
        {
            return _internal.Value.CreateTimeSpanAccessor(logger, "keepAlivePingInterval", TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// The interval between each keepalive ping to each remoting container, or 0 to disable health monitoring
        /// </summary>
        public TimeSpan KeepAlivePingTimeout
        {
            get
            {
                return _internal.Value.GetTimeSpan("keepAlivePingTimeout", TimeSpan.FromSeconds(1));
            }
            set
            {
                _internal.Value.Set("keepAlivePingTimeout", value);
            }
        }

        public IConfigValue<TimeSpan> KeepAlivePingTimeoutAccessor(ILogger logger)
        {
            return _internal.Value.CreateTimeSpanAccessor(logger, "keepAlivePingTimeout", TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// The rate that container health must fall below (as measured by keepalive ping failures) before attempting to recycle a container.
        /// </summary>
        public float KeepAliveFailureThreshold
        {
            get
            {
                return _internal.Value.GetFloat32("keepAliveFailureThreshold", 0.2f);
            }
            set
            {
                _internal.Value.Set("keepAliveFailureThreshold", value);
            }
        }

        public IConfigValue<float> KeepAliveFailureThresholdAccessor(ILogger logger)
        {
            return _internal.Value.CreateFloat32Accessor(logger, "keepAliveFailureThreshold", 0.2f);
        }

        /// <summary>
        /// Protocol to use for interprocess communication with containers. Valid options are "bond" or "json"
        /// </summary>
        public string IpcProtocol
        {
            get
            {
                return _internal.Value.GetString("ipcProtocol", "bond");
            }
            set
            {
                _internal.Value.Set("ipcProtocol", value);
            }
        }

        /// <summary>
        /// Indicates whether IPC to containers should use a dedicated thread.
        /// </summary>
        public bool UseDedicatedIpcThreads
        {
            get
            {
                return _internal.Value.GetBool("useDedicatedIpcThreads", true);
            }
            set
            {
                _internal.Value.Set("useDedicatedIpcThreads", value);
            }
        }
    }
}
