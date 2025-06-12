using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using Durandal.Common.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;

namespace DurandalServices.ChannelProxy.Connectors
{
    public class DoradoConnector : IConnector
    {
        private readonly Regex _speakTagRegex = new Regex("<\\/?speak.+?>");
        private readonly ILogger _serviceLogger;

        public DoradoConnector(ILogger serviceLogger)
        {
            _serviceLogger = serviceLogger;
        }

        public string Prefix
        {
            get
            {
                return "/connectors/dorado";
            }
        }

        public async Task<HttpResponse> HandleRequest(IDialogClient client, HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Parse input
            string payload = await request.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
            bot_request botRequest = JsonConvert.DeserializeObject<bot_request>(payload);
            if (botRequest == null || botRequest.data == null || !botRequest.data.ContainsKey("input"))
            {
                return HttpResponse.BadRequestResponse();
            }

            durandal_request doradoRequest = ((JObject)botRequest.data["input"]).ToObject<durandal_request>();
            string user_id = StringUtils.HashToGuid(botRequest.user_id).ToString("N");
            string trace_id = botRequest.trace_id;

            DialogRequest durandalRequest = new DialogRequest()
            {
                InteractionType = InputMethod.Spoken,
                TextInput = doradoRequest.user_input,
                RequestFlags = (uint)QueryFlags.None,
                TraceId = string.IsNullOrEmpty(trace_id) ? CommonInstrumentation.FormatTraceId(Guid.NewGuid()) : trace_id,
                ClientContext = new ClientContext()
                {
                    ClientId = user_id,
                    UserId = user_id,
                    ClientName = "Dorado adapter",
                    Locale = LanguageCode.EN_US,
                    Capabilities = (ClientCapabilities.CanSynthesizeSpeech |
                                ClientCapabilities.DisplayHtml5 |
                                ClientCapabilities.DisplayUnlimitedText |
                                ClientCapabilities.DoNotRenderTextAsHtml)
                }
            };

            // Call durandal
            using (NetworkResponseInstrumented<DialogResponse> durandalHttpResponse = await client.MakeQueryRequest(durandalRequest, _serviceLogger, cancelToken, realTime))
            {
                if (durandalHttpResponse == null ||
                    !durandalHttpResponse.Success)
                {
                    return HttpResponse.ServerErrorResponse();
                }

                DialogResponse durandalResponse = durandalHttpResponse.Response;

                // Convert output
                durandal_response doradoResponse = new durandal_response()
                {
                    continues = durandalResponse.ContinueImmediately,
                    text = durandalResponse.ResponseText,
                    ssml = StringUtils.RegexRemove(_speakTagRegex, durandalResponse.ResponseSsml),
                    card_url = client.GetConnectionString() + durandalResponse.ResponseUrl
                };

                bot_response botResponse = new bot_response()
                {
                    result = new mst_map()
                };
                botResponse.result["output"] = doradoResponse;

                HttpResponse returnVal = HttpResponse.OKResponse();
                returnVal.SetContentJson(botResponse);
                return returnVal;
            }
        }

        private class durandal_request
        {
            [JsonProperty(PropertyName = "@")]
            public static readonly string classType = "msd.lostromb.durandal_request";

            public string user_input { get; set; }
        }

        private class durandal_response
        {
            [JsonProperty(PropertyName = "@")]
            public static readonly string classType = "msd.lostromb.durandal_response";

            public string text { get; set; }
            public string ssml { get; set; }
            public string card_url { get; set; }
            public bool continues { get; set; }
        }

        private class mst_map : Dictionary<string, object>
        {
            [JsonProperty(PropertyName = "@")]
            public static readonly string classType = "mst.map";
        }

        private class bot_request
        {
            [JsonProperty(PropertyName = "@")]
            public static readonly string classType = "mst.bot.request";

            public string user_id { get; set; }
            public string trace_id { get; set; }
            public string session { get; set; }
            public Dictionary<string, object> data { get; set; }
        }

        private class bot_response
        {
            [JsonProperty(PropertyName = "@")]
            public static readonly string classType = "mst.bot.response";
            public mst_map result { get; set; }
        }
    }
}
