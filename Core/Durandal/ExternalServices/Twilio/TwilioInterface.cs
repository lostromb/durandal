using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Twilio
{
    public class TwilioInterface
    {
        private readonly string _apiKey;
        private readonly string _accountSid;
        private readonly IHttpClient _httpClient;

        public TwilioInterface(string apiKey, string accountSid, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _apiKey = apiKey;
            _accountSid = accountSid;
            _httpClient = httpClientFactory.CreateHttpClient(new Uri("https://api.twilio.com"), logger);
        }

        public async Task MakeCall(string sourceNumber, string targetNumber, string twiMLUrl, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            queryLogger.Log("Sending call from " + sourceNumber + " to " + targetNumber);
            HttpRequest request = HttpRequest.CreateOutgoing("/2010-04-01/Accounts/" + _accountSid + "/Calls.json", "POST");
            IDictionary<string, string> parts = new Dictionary<string, string>();
            parts.Add("To", targetNumber);
            parts.Add("From", sourceNumber);
            parts.Add("Url", twiMLUrl);
            request.SetContent(parts);

            string httpUser = _accountSid + ":" + _apiKey;
            string encodedUser = Convert.ToBase64String(Encoding.UTF8.GetBytes(httpUser));
            request.RequestHeaders["Authorization"] = "Basic " + encodedUser;
            HttpResponse responseMessage = await _httpClient.SendRequestAsync(request, CancellationToken.None, realTime, queryLogger).ConfigureAwait(false);

            try
            {
                if (responseMessage == null)
                {
                    queryLogger.Log("Null response from Twilio service!", LogLevel.Err);
                }
                else if (responseMessage.ResponseCode >= 300)
                {
                    queryLogger.Log("Non-success response " + responseMessage.ResponseCode + " from Twilio service!", LogLevel.Err);
                    ArraySegment<byte> responsePayload = await responseMessage.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                    if (responsePayload.Count > 0)
                    {
                        string errorMessage = Encoding.UTF8.GetString(responsePayload.Array, responsePayload.Offset, responsePayload.Count);
                        queryLogger.Log(errorMessage, LogLevel.Err);
                    }
                }
            }
            finally
            {
                if (responseMessage != null)
                {
                    await responseMessage.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }
            }
        }

        public async Task SendSMS(string sourceNumber, string targetNumber, string text, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            queryLogger.Log("Sending SMS from " + sourceNumber + " to " + targetNumber);
            HttpRequest request = HttpRequest.CreateOutgoing("/2010-04-01/Accounts/" + _accountSid + "/Messages.json", "POST");
            IDictionary<string, string> parts = new Dictionary<string, string>();
            parts.Add("To", targetNumber);
            parts.Add("From", sourceNumber);
            parts.Add("Body", text);
            request.SetContent(parts);

            string httpUser = _accountSid + ":" + _apiKey;
            string encodedUser = Convert.ToBase64String(Encoding.UTF8.GetBytes(httpUser));
            request.RequestHeaders["Authorization"] = "Basic " + encodedUser;
            HttpResponse responseMessage = await _httpClient.SendRequestAsync(request, CancellationToken.None, realTime, queryLogger).ConfigureAwait(false);
            try
            {
                if (responseMessage == null)
                {
                    queryLogger.Log("Null response from Twilio service!", LogLevel.Err);
                }
                else if (responseMessage.ResponseCode >= 300)
                {
                    queryLogger.Log("Non-success response " + responseMessage.ResponseCode + " from Twilio service!", LogLevel.Err);
                    ArraySegment<byte> responsePayload = await responseMessage.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                    if (responsePayload.Count > 0)
                    {
                        string errorMessage = Encoding.UTF8.GetString(responsePayload.Array, responsePayload.Offset, responsePayload.Count);
                        queryLogger.Log(errorMessage, LogLevel.Err);
                    }
                }
            }
            finally
            {
                if (responseMessage != null)
                {
                    await responseMessage.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    responseMessage.Dispose();
                }
            }
        }
    }
}
