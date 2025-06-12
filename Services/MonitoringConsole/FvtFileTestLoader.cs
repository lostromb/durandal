using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Monitoring;
using Durandal.Common.Monitoring.Monitors;
using Durandal.Common.Tasks;
using Durandal.Common.Test.FVT;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MonitorConsole
{
    public class FvtFileTestLoader
    {
        //private readonly IFileSystem _fvtFileSystem;
        //private readonly VirtualPath _fvtDirectory;
        private readonly Uri _dialogServiceUri;
        private readonly IDialogTransportProtocol _transportProtocol;
        private readonly ILogger _dialogInstrumentationLogger;
        private readonly IFunctionalTestIdentityStore _testIdentityStore;
        private readonly float? _defaultPassRateThreshold;
        private readonly TimeSpan? _defaultLatencyPerTurnThreshold;

        public FvtFileTestLoader(
            Uri dialogServiceUri,
            IDialogTransportProtocol transportProtocol,
            ILogger dialogInstrumentationLogger,
            IFunctionalTestIdentityStore testIdentityStore)
        {
            _dialogServiceUri = dialogServiceUri;
            _transportProtocol = transportProtocol;
            _dialogInstrumentationLogger = dialogInstrumentationLogger;
            _testIdentityStore = testIdentityStore;
            _defaultPassRateThreshold = 0.7f;
            _defaultLatencyPerTurnThreshold = TimeSpan.FromMilliseconds(500);
        }

        public async Task Load(IList<IServiceMonitor> monitors, ILogger logger)
        {
            await DurandalTaskExtensions.NoOpTask;
            //foreach (VirtualPath fvtFile in await _fvtFileSystem.ListFilesAsync(_fvtDirectory))
            //{
            //    if (!string.Equals(".json", fvtFile.Extension, StringComparison.OrdinalIgnoreCase))
            //    {
            //        continue;
            //    }

            //    FunctionalTestMonitor monitor = new FunctionalTestMonitor(
            //       fvtFile.Name,
            //       _dialogServiceUri,
            //       _transportProtocol,
            //       _dialogInstrumentationLogger,
            //       _testIdentityStore,
            //       "ChitChat",
            //       passRateThreshold: _defaultPassRateThreshold,
            //       latencyPerTurnThreshold: _defaultLatencyPerTurnThreshold);

            //    await monitor.Initialize(environmentConfig, machineLocalGuid, _fvtFileSystem, functionalTestHttpClientFactory, testSocketFactory);
            //    monitors.Add(monitor);
            //}

            monitors.Add(new FunctionalTestMonitor(
                "ChitChat Hello Speech.json",
                _dialogServiceUri,
                _transportProtocol,
                _dialogInstrumentationLogger,
                _testIdentityStore,
                "ChitChat",
                passRateThreshold: _defaultPassRateThreshold,
                latencyPerTurnThreshold: _defaultLatencyPerTurnThreshold));
            monitors.Add(new FunctionalTestMonitor(
                "ChitChat Hello.json",
                _dialogServiceUri,
                _transportProtocol,
                _dialogInstrumentationLogger,
                _testIdentityStore,
                "ChitChat",
                passRateThreshold: _defaultPassRateThreshold,
                latencyPerTurnThreshold: _defaultLatencyPerTurnThreshold));
            monitors.Add(new FunctionalTestMonitor(
                "Team Rocket.json",
                _dialogServiceUri,
                _transportProtocol,
                _dialogInstrumentationLogger,
                _testIdentityStore,
                "ChitChat",
                passRateThreshold: _defaultPassRateThreshold,
                latencyPerTurnThreshold: _defaultLatencyPerTurnThreshold));
            monitors.Add(new FunctionalTestMonitor(
                "Weather Basic.json",
                _dialogServiceUri,
                _transportProtocol,
                _dialogInstrumentationLogger,
                _testIdentityStore,
                "Weather",
                passRateThreshold: _defaultPassRateThreshold,
                latencyPerTurnThreshold: _defaultLatencyPerTurnThreshold));
        }
    }
}
