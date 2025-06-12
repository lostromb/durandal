using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.LU
{
    /// <summary>
    /// An LU client that simply wraps around a local LanguageUnderstandingEngine
    /// </summary>
    public class NativeLuClient : ILUClient
    {
        private readonly LanguageUnderstandingEngine _core;

        public NativeLuClient(LanguageUnderstandingEngine core)
        {
            _core = core;
        }

        public Task<IDictionary<string, string>> GetStatus(ILogger queryLogger = null, CancellationToken cancelToken = default(CancellationToken), IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            IDictionary<string, string> responseParams = new Dictionary<string, string>();
            responseParams["Version"] = SVNVersionInfo.VersionString;
            responseParams["Initialized"] = _core.Initialized.ToString();
            responseParams["LoadedModels"] = string.Join(",", _core.LoadedModels);
            responseParams["LastReloadTime"] = _core.LastModelLoadTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            responseParams["Packages"] = _core.Packages;
            LURequest defaultRequest = new LURequest();
            responseParams["ProtocolVersion"] = defaultRequest.ProtocolVersion.ToString();
            return Task.FromResult<IDictionary<string, string>>(responseParams);
        }

        public async Task<NetworkResponseInstrumented<LUResponse>> MakeQueryRequest(LURequest request, ILogger queryLogger = null, CancellationToken cancelToken = default(CancellationToken), IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            Stopwatch timer = Stopwatch.StartNew();
            LUResponse response = await _core.Classify(request, realTime, queryLogger).ConfigureAwait(false);
            timer.Stop();
            return new NetworkResponseInstrumented<LUResponse>(response, 0, 0, 0, timer.ElapsedMillisecondsPrecise(), 0);
        }
    }
}
