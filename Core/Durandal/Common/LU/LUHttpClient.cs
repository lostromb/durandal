namespace Durandal.Common.Net
{
    using Durandal.API;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Time;

    /// <summary>
    /// This is the interface that sends a LURequest to an HTTP endpoint
    /// </summary>
    public class LUHttpClient : ILUClient
    {
        private readonly ILogger _logger;
        private readonly IHttpClient _clientInternal;
        private readonly ILUTransportProtocol _transportProtocol;

        public LUHttpClient(IHttpClient client, ILogger logger, ILUTransportProtocol protocol)
        {
            _logger = logger;
            _clientInternal = client;
            _transportProtocol = protocol;
        }

        public string GetConnectionString()
        {
            return _clientInternal.ServerAddress.AbsoluteUri;
        }

        public void SetReadTimeout(TimeSpan timeout)
        {
            _clientInternal.SetReadTimeout(timeout);
        }

        public async Task<NetworkResponseInstrumented<LUResponse>> MakeQueryRequest(LURequest request, ILogger queryLogger = null, CancellationToken cancelToken = default(CancellationToken), IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }
            else
            {
                queryLogger = queryLogger.Clone(_logger.ComponentName);
            }

            if (request == null)
            {
                queryLogger.Log("Null LURequest passed to LUHttpClient", LogLevel.Err);
                return null;
            }
            
            int retries = 0;
            while (retries++ < 3)
            {
                try
                {
                    ArraySegment<byte> result = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);

                    using (HttpRequest httpRequest = HttpRequest.CreateOutgoing("/query", "POST"))
                    {
                        httpRequest.GetParameters["format"] = _transportProtocol.ProtocolName.ToLowerInvariant();
                        httpRequest.SetContent(_transportProtocol.WriteLURequest(request, queryLogger.Clone("LUTransportProtocol")), _transportProtocol.MimeType);
                        using (NetworkResponseInstrumented<HttpResponse> httpResponse =
                            await _clientInternal.SendInstrumentedRequestAsync(httpRequest, cancelToken, realTime, queryLogger).ConfigureAwait(false))
                        {
                            try
                            {
                                if (httpResponse == null || httpResponse.Response == null)
                                {
                                    queryLogger.Log("Null LU response. Retrying " + retries + "...", LogLevel.Err);
                                    continue;
                                }

                                result = await httpResponse.Response.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);

                                if (httpResponse.Response.ResponseCode != 200)
                                {
                                    queryLogger.Log("Error response from LU: " + httpResponse.Response.ResponseCode, LogLevel.Err);
                                    if (result.Count > 0)
                                    {
                                        string httpErrorMessage = Encoding.UTF8.GetString(result.Array, result.Offset, result.Count);
                                        queryLogger.Log("Response message: " + httpErrorMessage, LogLevel.Err);
                                    }

                                    queryLogger.Log("Retrying " + retries + "...");
                                    continue;
                                }

                                if (result.Count == 0)
                                {
                                    queryLogger.Log("Empty LU response. Retrying " + retries + "...", LogLevel.Err);
                                    continue;
                                }

                                LUResponse luResp = _transportProtocol.ParseLUResponse(result, queryLogger);
                                return new NetworkResponseInstrumented<LUResponse>(luResp,
                                    httpResponse.RequestSize,
                                    result.Count,
                                    httpResponse.SendLatency,
                                    httpResponse.RemoteLatency,
                                    httpResponse.RecieveLatency);
                            }
                            finally
                            {
                                if (httpResponse != null)
                                {
                                    await httpResponse.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // Could not connect to server.
                    queryLogger.Log("Error while calling LU service.", LogLevel.Err);
                    queryLogger.Log("Remote connection string is " + _clientInternal.ServerAddress, LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    queryLogger.Log("Retrying " + retries + "...");
                }
            }

            return null;
        }

        public async Task<IDictionary<string, string>> GetStatus(ILogger queryLogger = null, CancellationToken cancelToken = default(CancellationToken), IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }
            else
            {
                queryLogger = queryLogger.Clone(_logger.ComponentName);
            }

            try
            {
                using (HttpRequest request = HttpRequest.CreateOutgoing("/status", "GET"))
                using (NetworkResponseInstrumented<HttpResponse> response = await _clientInternal.SendInstrumentedRequestAsync(
                    request, cancelToken, realTime, queryLogger).ConfigureAwait(false))
                {
                    try
                    {
                        if (response == null || response.Response == null || response.Response.ResponseCode != 200)
                        {
                            return null;
                        }

                        return (await response.Response.ReadContentAsFormDataAsync(cancelToken, realTime).ConfigureAwait(false)).ToSimpleDictionary();
                    }
                    finally
                    {
                        if (response != null)
                        {
                            await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Could not connect to server.
                queryLogger.Log("Could not connect to LU service to retrieve status", LogLevel.Err);
                queryLogger.Log("Remote connection string is " + _clientInternal.ServerAddress, LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                return null;
            }
        }
    }
}
