using Durandal.API;
using Durandal.Common.Dialog;
using Newtonsoft.Json;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Common.Time;
using Durandal.Common.Client;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices.ChannelProxy.Connectors
{
    public class TelegramConnector : IConnector
    {
        private ILogger _logger;
        private readonly WeakPointer<IThreadPool> _threadPool;

        public TelegramConnector(ILogger logger, WeakPointer<IThreadPool> threadPool)
        {
            _logger = logger;
            _threadPool = threadPool;
        }

        public string Prefix
        {
            get
            {
                return "/connectors/telegram";
            }
        }

        public async Task<HttpResponse> HandleRequest(IDialogClient client, HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpResponse response = HttpResponse.BadRequestResponse();
            string botToken = "265356260:AAHSLzMMX0KIEselRZovw1CQ_2CTQd8dUbA"; // todo get this from config
            try
            {
                TelegramUpdate update = JsonConvert.DeserializeObject<TelegramUpdate>(await request.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false));
                _logger.Log(update.message.from.first_name + " >>> " + update.message.text);
                _threadPool.Value.EnqueueUserAsyncWorkItem(() => FireDialogProcessing(client, botToken, update, realTime));
                await DurandalTaskExtensions.NoOpTask;
                response = HttpResponse.OKResponse();
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
            return response;
        }

        private async Task FireDialogProcessing(IDialogClient client, string botToken, TelegramUpdate update, IRealTimeProvider realTime)
        {
            int chatId = update.message.chat.id;
            string inputMessage = update.message.text;

            DialogRequest request;
            if (inputMessage.Equals("/start"))
            {
                request = BuildGreetRequest(chatId);
            }
            else
            {
                request = BuildRegularClientRequest(chatId, inputMessage);
            }

            NetworkResponseInstrumented<DialogResponse> response = await client.MakeQueryRequest(request, _logger, CancellationToken.None, realTime);

            if (response == null || response.Response == null)
            {
                await SendMessage(chatId, botToken, "Error: Could not connect to dialog server");
            }
            else if (response.Response.ExecutionResult == Result.Success)
            {
                if (!string.IsNullOrEmpty(response.Response.ResponseText))
                {
                    await SendMessage(chatId, botToken, response.Response.ResponseText);
                }
            }
            else if (response.Response.ExecutionResult == Result.Failure)
            {
                if (!string.IsNullOrEmpty(response.Response.ErrorMessage))
                {
                    await SendMessage(chatId, botToken, "Error: " + response.Response.ErrorMessage);
                }
                else
                {
                    await SendMessage(chatId, botToken, "Sorry, an unknown error occurred");
                }
            }
        }

        private DialogRequest BuildRegularClientRequest(int chatId, string inputMessage)
        {
            string clientName = "TelegramClient:" + chatId;
           
            DialogRequest dialogRequest = new DialogRequest()
            {
                ClientContext = new ClientContext()
                {
                    ClientId = StringUtils.HashToGuid(clientName).ToString("N"),
                    UserId = "aa6951e0562fe21866fe7f0424547008",
                    Locale = LanguageCode.EN_US,
                    ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ClientName = clientName
                },
                InteractionType = InputMethod.Typed,
                TextInput = inputMessage,
                TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid())
            };

            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Messenger.ToString();
            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.ClientType] = "TELEGRAM";
            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;
            dialogRequest.ClientContext.SetCapabilities(ClientCapabilities.DisplayUnlimitedText);

            return dialogRequest;
        }

        private DialogRequest BuildGreetRequest(int chatId)
        {
            string clientName = "TelegramClient:" + chatId;

            DialogRequest dialogRequest = new DialogRequest()
            {
                ClientContext = new ClientContext()
                {
                    ClientId = StringUtils.HashToGuid(clientName).ToString("N"),
                    UserId = "aa6951e0562fe21866fe7f0424547008",
                    Locale = LanguageCode.EN_US,
                    ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ClientName = clientName
                },
                InteractionType = InputMethod.Programmatic,
                TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid()),
                LanguageUnderstanding = new List<RecognizedPhrase>()
            };

            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Messenger.ToString();
            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.ClientType] = "TELEGRAM";
            dialogRequest.ClientContext.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;
            dialogRequest.ClientContext.SetCapabilities(ClientCapabilities.DisplayUnlimitedText);
            dialogRequest.LanguageUnderstanding[0].Recognition = new List<RecoResult>()
            {
                new RecoResult()
                {
                    Confidence = 1.0f,
                    Domain = "reflection",
                    Intent = "greet"
                }
            };

            return dialogRequest;
        }

        private async Task SendMessage(int chatId, string botToken, string message)
        {
            _logger.Log("<<< " + message);
            WebClient client = new WebClient();
            client.Headers[HttpConstants.HEADER_KEY_CONTENT_TYPE] = "application/json";
            string payload = "{ \"chat_id\": " + chatId + " ,\"text\": \"" + message.Replace("\"", "\\\"")  + "\" }";
            string address = string.Format("https://api.telegram.org/bot{0}/sendMessage", botToken);
            
            try
            {
                await client.UploadStringTaskAsync(address, payload);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private class TelegramUpdate
        {
            //public int update_id;
            public TelegramMessage message = null;
        }

        private class TelegramMessage
        {
            //public int message_id;
            public TelegramUser from = null;
            public TelegramChat chat = null;
            //public long date;
            public string text = "";
        }

        private class TelegramUser
        {
            //public int id;
            public string first_name = "";
            //public string last_name;
        }

        private class TelegramChat
        {
            public int id = 0;
            //public string first_name;
            //public string last_name;
            //public string type;
        }
    }
}
