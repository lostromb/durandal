using Durandal.Common.Utils;

namespace Durandal.Common.Net
{
    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Common.Dialog;
    using Durandal.Common.Security;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Net;
    using System.Text;
    using Newtonsoft.Json;
    using Durandal.Common.IO;
    using Http;
    using Dialog.Web;
    using Security.Client;
    using Durandal.Common.Time;
    using System.Threading;
    using System.Diagnostics;
    using Durandal.Common.Audio;
    using Audio.Codecs;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// This is the interface that sends a ClientRequest to a dialog HTTP endpoint and returns a ServerResponse
    /// </summary>
    public class DialogHttpClient : IDialogClient
    {
        private readonly WeakPointer<IHttpClient> _clientInternal;
        private readonly ILogger _logger;
        private readonly IDialogTransportProtocol _protocol;
        private int _disposed = 0;

        public DialogHttpClient(IHttpClient client, ILogger logger, IDialogTransportProtocol protocol)
        {
            _clientInternal = new WeakPointer<IHttpClient>(client);
            _logger = logger;
            _logger.Log("Dialog connection string is " + GetConnectionString());
            _protocol = protocol;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DialogHttpClient()
        {
            Dispose(false);
        }
#endif

        public Uri GetConnectionString()
        {
            return new Uri(string.Format("{0}://{1}:{2}", _clientInternal.Value.ServerAddress.Scheme, _clientInternal.Value.ServerAddress.Host, _clientInternal.Value.ServerAddress.Port));
        }

        public void SetReadTimeout(TimeSpan timeout)
        {
            _clientInternal.Value.SetReadTimeout(timeout);
        }

        public IHttpClient InternalClient
        {
            get
            {
                return _clientInternal.Value;
            }
        }

        public async Task<NetworkResponseInstrumented<DialogResponse>> MakeQueryRequest(
            DialogRequest request,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
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
                queryLogger.Log("Null ClientRequest passed to DialogHttpClient", LogLevel.Err);
                return null;
            }
            
            //while (!success) // whoops
            {
                try
                {
                    ArraySegment<byte> result = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);

                    using (HttpRequest httpRequest = HttpRequest.CreateOutgoing("/query", HttpConstants.HTTP_VERB_POST))
                    {
                        httpRequest.GetParameters.Add("format", _protocol.ProtocolName.ToLowerInvariant());
                        httpRequest.SetContent(_protocol.WriteClientRequest(request, queryLogger), _protocol.MimeType);
                        if (!string.IsNullOrEmpty(_protocol.ContentEncoding))
                        {
                            httpRequest.RequestHeaders[HttpConstants.HEADER_KEY_CONTENT_ENCODING] = _protocol.ContentEncoding;
                            httpRequest.RequestHeaders[HttpConstants.HEADER_KEY_ACCEPT_ENCODING] = _protocol.ContentEncoding;
                        }

                        using (NetworkResponseInstrumented<HttpResponse> httpResponse = await _clientInternal.Value.SendInstrumentedRequestAsync(
                            httpRequest, cancelToken, realTime, queryLogger).ConfigureAwait(false))
                        {
                            try
                            {
                                if (httpResponse == null || httpResponse.Response == null)
                                {
                                    queryLogger.Log("Null dialog response", LogLevel.Err);
                                    return null;
                                }

                                if (httpResponse.Response.ResponseCode != 200)
                                {
                                    queryLogger.Log("Error returned from dialog service: " + httpResponse.Response.ResponseCode + " " + httpResponse.Response.ResponseMessage, LogLevel.Err);
                                    string responseMessage = await httpResponse.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                    if (!string.IsNullOrEmpty(responseMessage))
                                    {
                                        queryLogger.Log(responseMessage, LogLevel.Err);
                                    }

                                    return null;
                                }

                                result = await httpResponse.Response.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);

                                if (result.Count == 0)
                                {
                                    queryLogger.Log("Empty response dialog service", LogLevel.Err);
                                    return null;
                                }

                                DialogResponse clientResponse = _protocol.ParseClientResponse(result, queryLogger);
                                return new NetworkResponseInstrumented<DialogResponse>(clientResponse,
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
                                    await httpResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log("Error occurred while making query. Remote connection string is " + GetConnectionString(), LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
            }

            return null;
        }

        public async Task<ResetConversationStateResult> ResetConversationState(
            string userId,
            string clientId,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
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

            ResetConversationStateResult result = new ResetConversationStateResult()
            {
                Success = false,
                ErrorMessage = string.Empty
            };

            using (HttpRequest request = HttpRequest.CreateOutgoing("/reset", HttpConstants.HTTP_VERB_POST))
            {
                IDictionary<string, string> formData = new Dictionary<string, string>();
                formData.Add("clientid", clientId);
                formData.Add("userid", userId);
                request.SetContent(formData);
                try
                {
                    using (NetworkResponseInstrumented<HttpResponse> response = await _clientInternal.Value.SendInstrumentedRequestAsync(
                        request, cancelToken, realTime, queryLogger).ConfigureAwait(false))
                    {
                        try
                        {
                            if (response == null)
                            {
                                result.ErrorMessage = "Web response was null";
                                return result;
                            }
                            else if (response.Response == null)
                            {
                                result.ErrorMessage = "Web response contained null data";
                                return result;
                            }
                            else if (response.Response.ResponseCode != 200)
                            {
                                result.ErrorMessage = "Web response had unexpected status code " + response.Response.ResponseCode;
                                string responseString = await response.Response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(responseString))
                                {
                                    result.ErrorMessage = result.ErrorMessage + ": " + responseString;
                                }
                                else if (!string.IsNullOrEmpty(response.Response.ResponseMessage))
                                {
                                    result.ErrorMessage = result.ErrorMessage + ": " + response.Response.ResponseMessage;
                                }

                                return result;
                            }
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
                    queryLogger.Log("Could not connect to dialog server to reset conversation state", LogLevel.Err);
                    queryLogger.Log("Remote connection string is " + GetConnectionString(), LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    result.ErrorMessage = e.Message;
                    return result;
                }

                result.Success = true;
                return result;
            }
        }

        public async Task<NetworkResponseInstrumented<DialogResponse>> MakeDialogActionRequest(
            DialogRequest request,
            string actionId,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
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
                using (HttpRequest webRequest = HttpRequest.CreateOutgoing("/action", HttpConstants.HTTP_VERB_POST))
                {
                    webRequest.GetParameters.Add("key", actionId);
                    webRequest.GetParameters.Add("client", request.ClientContext.ClientId);
                    webRequest.GetParameters.Add("format", _protocol.ProtocolName.ToLowerInvariant());
                    webRequest.SetContent(_protocol.WriteClientRequest(request, queryLogger), _protocol.MimeType);
                    if (!string.IsNullOrEmpty(_protocol.ContentEncoding))
                    {
                        webRequest.RequestHeaders[HttpConstants.HEADER_KEY_CONTENT_ENCODING] = _protocol.ContentEncoding;
                        webRequest.RequestHeaders[HttpConstants.HEADER_KEY_ACCEPT_ENCODING] = _protocol.ContentEncoding;
                    }

                    using (NetworkResponseInstrumented<HttpResponse> response = await _clientInternal.Value.SendInstrumentedRequestAsync(
                        webRequest, cancelToken, realTime, queryLogger).ConfigureAwait(false))
                    {
                        try
                        {
                            if (response != null && response.Response != null && response.Response.ResponseCode == 200)
                            {
                                ArraySegment<byte> responsePayload = await response.Response.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                                DialogResponse durandalResult = _protocol.ParseClientResponse(responsePayload, queryLogger);
                                return response.Convert(durandalResult);
                            }
                            else
                            {
                                return null;
                            }
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
            }
            catch (Exception e)
            {
                // Could not connect to server.
                queryLogger.Log("Error occurred while making dialog action request", LogLevel.Err);
                queryLogger.Log("Remote connection string is " + GetConnectionString(), LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                return null;
            }
        }

        public async Task<IDictionary<string, string>> GetStatus(
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
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
                using (HttpRequest request = HttpRequest.CreateOutgoing("/status", HttpConstants.HTTP_VERB_GET))
                using (NetworkResponseInstrumented<HttpResponse> response = await _clientInternal.Value.SendInstrumentedRequestAsync(
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
                            await response.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Could not connect to server.
                queryLogger.Log("Could not connect to dialog service to retrieve status", LogLevel.Err);
                queryLogger.Log("Remote connection string is " + _clientInternal.Value.ServerAddress, LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                return null;
            }
        }

        //public async Task Shutdown()
        //{
        //    HttpRequest request = new HttpRequest()
        //    {
        //        RequestFile = "/shutdown",
        //        RequestMethod = "GET"
        //    };

        //    await _clientInternal.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, null);
        //}

        public async Task<HttpResponse> MakeStaticResourceRequest(
            HttpRequest request,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
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

            // clean up things that the proxy will overwrite
            if (request.RequestHeaders.ContainsKey("Host"))
            {
                request.RequestHeaders.Remove("Host");
            }
            if (request.RequestHeaders.ContainsKey("Connection"))
            {
                request.RequestHeaders.Remove("Connection");
            }

            NetworkResponseInstrumented<HttpResponse> response = await _clientInternal.Value.SendInstrumentedRequestAsync(
                request, cancelToken, realTime, queryLogger).ConfigureAwait(false);
            return response.Response;
        }

        public Task<IAudioDataSource> GetStreamingAudioResponse(
            string relativeAudioUrl,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
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

            return GetStreamingAudioResponseOverHttp(_clientInternal.Value, relativeAudioUrl, queryLogger, cancelToken, realTime);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
            }
        }

        /// <summary>
        /// Shared code to fetch a streaming audio response from an HTTP endpoint.
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="relativeAudioUrl"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private static async Task<IAudioDataSource> GetStreamingAudioResponseOverHttp(
            IHttpClient httpClient,
            string relativeAudioUrl,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            using (HttpRequest httpReq = HttpRequest.CreateOutgoing(relativeAudioUrl))
            {
                // explicitly don't put this response into a Using block because we are going to transfer ownership of it to the HttpAudioDataSource
                HttpResponse response = await httpClient.SendRequestAsync(httpReq, cancelToken, realTime, queryLogger).ConfigureAwait(false);

                try
                {
                    if (response.ResponseCode != 200)
                    {
                        queryLogger.Log("Streaming audio response returned an error " + response.ResponseCode, LogLevel.Err);
                        string responseMessage = await response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(responseMessage))
                        {
                            queryLogger.Log(responseMessage, LogLevel.Err);
                        }

                        return null;
                    }

                    string codecName;
                    if (!response.ResponseHeaders.TryGetValue("X-Audio-Codec", out codecName))
                    {
                        codecName = string.Empty;
                    }

                    string encodeParams;
                    if (!response.ResponseHeaders.TryGetValue("X-Audio-Codec-Params", out encodeParams))
                    {
                        encodeParams = string.Empty;
                    }

                    queryLogger.Log(string.Format("Content type is {0}, encode params are {1}", codecName, encodeParams));

                    if (string.IsNullOrEmpty(codecName))
                    {
                        queryLogger.Log("No content type associated with streaming audio! Assuming it is uncompressed PCM", LogLevel.Wrn);
                        codecName = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE;
                        encodeParams = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Mono(16000));
                    }

                    HttpAudioDataSource returnVal = new HttpAudioDataSource(response, codecName, encodeParams);
                    response = null;
                    return returnVal;
                }
                finally
                {
                    if (response != null)
                    {
                        await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        response.Dispose();
                    }
                }
            }
        }
    }
}
