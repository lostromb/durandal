using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Common.Time;
using Durandal.Common.Client;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices.ChannelProxy.Connectors
{
    public class TwilioConnector : IConnector
    {
        private readonly ILogger _logger;
        private readonly WeakPointer<IThreadPool> _threadPool;

        public TwilioConnector(ILogger logger, WeakPointer<IThreadPool> threadPool)
        {
            _logger = logger;
            _threadPool = threadPool;
        }

        public string Prefix
        {
            get
            {
                return "/Connectors/Twilio";
            }
        }

        public async Task<HttpResponse> HandleRequest(IDialogClient client, HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpFormParameters formParts = await request.ReadContentAsFormDataAsync(cancelToken, realTime).ConfigureAwait(false);
            string userPhoneNumber = formParts["From"];
            string twilioPhoneNumber = formParts["To"];
            string message = formParts["Body"];
            _logger.Log(userPhoneNumber + " => " + twilioPhoneNumber + " : " + message);
            _threadPool.Value.EnqueueUserAsyncWorkItem(() => FireDialogProcessing(client, userPhoneNumber, twilioPhoneNumber, message, realTime));
            await DurandalTaskExtensions.NoOpTask;
            HttpResponse response = HttpResponse.OKResponse();
            return response;
        }

        private async Task FireDialogProcessing(IDialogClient client, string userPhoneNumber, string twilioPhoneNumber, string message, IRealTimeProvider realTime)
        {
            string clientName = "Twilio Client";
            string userIdHash = StringUtils.HashToGuid(userPhoneNumber).ToString("N");

            DialogRequest dialogRequest = new DialogRequest()
            {
                ClientContext = new ClientContext()
                {
                    ClientId = userIdHash,
                    UserId = userIdHash,
                    Locale = LanguageCode.EN_US,
                    ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ClientName = clientName,
                },
                InteractionType = InputMethod.Typed,
                TextInput = message,
                TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid())
            };

            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Messenger.ToString();
            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.ClientType] = "SMS";
            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;
            dialogRequest.ClientContext.SetCapabilities(ClientCapabilities.DisplayBasicText);

            NetworkResponseInstrumented<DialogResponse> response = await client.MakeQueryRequest(dialogRequest, _logger, CancellationToken.None, realTime);

            if (response == null || response.Response == null)
            {
                await SendSMS(twilioPhoneNumber, userPhoneNumber, "Error: Could not connect to dialog server");
            }
            else if (response.Response.ExecutionResult == Result.Success)
            {
                if (!string.IsNullOrEmpty(response.Response.ResponseText))
                {
                    await SendSMS(twilioPhoneNumber, userPhoneNumber, response.Response.ResponseText);
                }
            }
            else if (response.Response.ExecutionResult == Result.Failure)
            {
                if (!string.IsNullOrEmpty(response.Response.ErrorMessage))
                {
                    await SendSMS(twilioPhoneNumber, userPhoneNumber, "Error: " + response.Response.ErrorMessage);
                }
                else
                {
                    await SendSMS(twilioPhoneNumber, userPhoneNumber, "Sorry, an unknown error occurred");
                }
            }
        }

        private async Task SendSMS(string sourceNumber, string targetNumber, string text)
        {
            if (text.Length > 160)
                text = text.Substring(0, 160);

            try
            {
                HttpClient client = new HttpClient()
                {
                    BaseAddress = new Uri("https://api.twilio.com")
                };
                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, "/2010-04-01/Accounts/ACfff69aa5bafa82e493afccf48d7ca380/Messages.json");
                IDictionary<string, string> parts = new Dictionary<string, string>();
                parts.Add("To", targetNumber);
                parts.Add("From", sourceNumber);
                parts.Add("Body", text);
                message.Content = new FormUrlEncodedContent(parts);
                string httpUser = "ACfff69aa5bafa82e493afccf48d7ca380:7ed9f49f5a70501b36601b56e80e8e79";
                string encodedUser = Convert.ToBase64String(Encoding.UTF8.GetBytes(httpUser));
                message.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedUser);
                _logger.Log(sourceNumber + " => " + targetNumber + " : " + text);
                await client.SendAsync(message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
