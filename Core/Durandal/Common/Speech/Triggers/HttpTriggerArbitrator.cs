using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public class HttpTriggerArbitrator : ITriggerArbitrator
    {
        private readonly IHttpClient _httpClient;
        private readonly string _triggeringGroup;

        public HttpTriggerArbitrator(IHttpClient httpClient, TimeSpan timeout, string triggeringGroup)
        {
            if (string.IsNullOrEmpty(triggeringGroup))
            {
                throw new ArgumentNullException(nameof(triggeringGroup));
            }

            _httpClient = httpClient;
            _httpClient.SetReadTimeout(timeout);
            _triggeringGroup = triggeringGroup;
        }
        
        public async Task<bool> ArbitrateTrigger(ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (_httpClient == null)
            {
                return true;
            }

            using (HttpRequest request = HttpRequest.CreateOutgoing("/arbitrate"))
            {
                request.GetParameters["group"] = _triggeringGroup;
                queryLogger.Log("Sending request to trigger arbitrator...");

                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, realTime, queryLogger).ConfigureAwait(false))
                {
                    try
                    {
                        queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_TriggerArbitration, netResponse.EndToEndLatency), LogLevel.Ins);
                        if (netResponse.Response == null)
                        {
                            // Remote server did not return in time; assume we are OK to trigger
                            queryLogger.Log("Trigger arbitrator request timed out after " + netResponse.EndToEndLatency + "ms", LogLevel.Wrn);
                            return true;
                        }
                        else
                        {
                            queryLogger.Log("Arbitrator returned code " + netResponse.Response.ResponseCode, LogLevel.Std);
                            return netResponse.Response.ResponseCode == 200;
                        }
                    }
                    finally
                    {
                        if (netResponse != null)
                        {
                            await netResponse.FinishAsync(CancellationToken.None, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
    }
}
