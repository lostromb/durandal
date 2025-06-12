

using Durandal.Common.Utils;

namespace Durandal
{
    using Durandal.Common.Net.Http;
    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Extensions.BondProtocol;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using System.Windows.Input;
    using Newtonsoft.Json;
    using Durandal.Common.Time;
    using System.Threading;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Interaction logic for DebugView.xaml
    /// </summary>
    public partial class DebugView : Page
    {
        private ILogger _coreLogger;
        
        public DebugView()
        {
            InitializeComponent();
            _coreLogger = ((App)App.Current).GetLogger();
        }

        private async void debugInput_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(Key.Enter))
            {
                string text = debugInput.Text;
                string response = await ThreadedDebugQuery(text);
                debugOutput.Text = response;
            }
        }

        private async Task<string> ThreadedDebugQuery(string query)
        {
            ILogger clientLogger = _coreLogger.Clone("DebugDialogHttpClient");
            DialogHttpClient client = new DialogHttpClient(new PortableHttpClient("localhost", 62292, false, clientLogger, NullMetricCollector.WeakSingleton, DimensionSet.Empty), clientLogger, new DialogBondTransportProtocol());
            client.SetReadTimeout(TimeSpan.FromMilliseconds(3000));
            DialogRequest request = new DialogRequest();
            request.InteractionType = Durandal.API.InputMethod.Typed;
            request.ClientContext.ClientId = "0";
            request.ClientContext.UserId = "0";
            request.PreferredAudioCodec = "opus";
            request.ClientContext.Locale = LanguageCode.EN_US;
            request.ClientContext.ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            request.ClientContext.ClientName = "debugclient";
            
            // todo: use geoip
            request.ClientContext.Latitude = 47.617108;
            request.ClientContext.Longitude = -122.191346;
            request.ClientContext.SetCapabilities(
                ClientCapabilities.DisplayUnlimitedText |
                ClientCapabilities.DisplayHtml5 |
                ClientCapabilities.HasSpeakers |
                ClientCapabilities.SupportsCompressedAudio |
                ClientCapabilities.ServeHtml |
                ClientCapabilities.SupportsStreamingAudio);
            request.TextInput = query;
            
            NetworkResponseInstrumented<DialogResponse> netResponse = await client.MakeQueryRequest(request, clientLogger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            StringBuilder message = new StringBuilder();
            if (netResponse == null || netResponse.Response == null)
            {
                message.AppendLine("Got null response! The dialog engine is not properly configured");
                message.AppendLine("See the log tab for more details.");
            }
            else
            {
                DialogResponse response = netResponse.Response;
                message.AppendLine("Got dialog response:");
                message.AppendLine("Protocol version = " + response.ProtocolVersion);
                message.AppendLine("Result = " + response.ExecutionResult);
                if (!string.IsNullOrEmpty(response.ResponseText))
                {
                    message.AppendLine("Response text = " + response.ResponseText);
                }
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    message.AppendLine("Response error message = " + response.ErrorMessage);
                }
                if (!string.IsNullOrEmpty(response.ResponseUrl))
                {
                    message.AppendLine("Response Url = " + response.ResponseUrl);
                    message.AppendLine("Url scope = " + response.UrlScope.ToString());
                }
                if (response.ResponseAudio != null && response.ResponseAudio.Data != null &&
                    response.ResponseAudio.Data.Count > 0)
                {
                    string audioInfo = string.Format("Response audio = length {0} codec {1} {2}", response.ResponseAudio.Data.Count, response.ResponseAudio.Codec, response.ResponseAudio.CodecParams);
                    message.AppendLine(audioInfo);
                }
                else if (!string.IsNullOrEmpty(response.StreamingAudioUrl))
                {
                    message.AppendLine("Recieved streaming audio over this URL: " + response.StreamingAudioUrl);
                }
                else
                {
                    message.AppendLine("No response audio");
                }
                message.AppendLine("Continue Immediately = " + response.ContinueImmediately);
                if (response.ResponseData != null && response.ResponseData.Count > 0)
                {
                    message.AppendLine("Got custom response data:");
                    foreach (var line in response.ResponseData)
                    {
                        message.AppendLine(string.Format("    \"{0}\" = \"{1}\"", line.Key, line.Value));
                    }
                }
                if (!string.IsNullOrEmpty(response.AugmentedFinalQuery))
                {
                    message.AppendLine("Augmented Query = " + response.AugmentedFinalQuery);
                }
                if (response.SelectedRecoResult != null)
                {
                    string json = JsonConvert.SerializeObject(response.SelectedRecoResult);
                    json = StringUtils.RegexReplace(new System.Text.RegularExpressions.Regex("\\s+"), json, " ");
                    message.AppendLine("Selected Reco Result = " + json);
                }
            }

            message.AppendLine("Latency was " + netResponse.EndToEndLatency);

            if (netResponse != null && netResponse.Response != null && !string.IsNullOrEmpty(netResponse.Response.ResponseHtml))
            {
                message.AppendLine("\r\n--- RESPONSE HTML FOLLOWS ---\r\n");
                message.Append(netResponse.Response.ResponseHtml);
            }

            return message.ToString();
        }
    }
}
