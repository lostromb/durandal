using Durandal.API;
using Durandal.API.Utils;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Security;
using Durandal.Common.Utils;
using Durandal.Common.Utils.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog
{
    public class PeerToPeerManager
    {
        private IDictionary<string, IDialogClient> _p2pClients = new Dictionary<string, IDialogClient>();
        private ClientAuthenticator _authenticator;
        private ILogger _coreLogger;

        public PeerToPeerManager(string clientId, string clientName, ILogger coreLogger, IResourceManager resourceManager)
        {
            _coreLogger = coreLogger;
            _authenticator = new ClientAuthenticator(clientId, clientName, _coreLogger, ClientAuthenticationScope.Client, new StandardRSADelegates());

            ResourceName savedKey = new ResourceName("p2p_auth.xml");
            if (!_authenticator.LoadPrivateKeyFromFile(savedKey, resourceManager, string.Empty))
            {
                _authenticator.GenerateNewKey(string.Empty);
                _authenticator.SavePrivateKeyToFile(savedKey, resourceManager, string.Empty);
            }
        }

        public bool PrepareP2P(string host, int port, ILogger queryLogger)
        {
            string key = host + ":" + port;
            lock (_p2pClients)
            {
                if (_p2pClients.ContainsKey(key))
                {
                    return true;
                }
            }

            IDialogClient client = new DialogHttpClient(new PortableHttpClient(host, 62292, _coreLogger), _coreLogger);

            client.MakeAuthHelloRequest(_authenticator, ClientAuthenticationScope.Client, null, null, queryLogger);
            client.MakeAuthAnswerRequest(_authenticator, ClientAuthenticationScope.Client, null, queryLogger);

            lock (_p2pClients)
            {
                _p2pClients[key] = client;
            }

            return true;
        }

        public DialogResult CallP2P(string host, int port, ClientContext context, InputMethod inputType, RecoResult luData, ILogger queryLogger, AudioData inputAudio = null)
        {
            string key = host + ":" + port;
            IDialogClient client;
            lock (_p2pClients)
            {
                if (!_p2pClients.ContainsKey(key))
                {
                    throw new InvalidOperationException("You cannot make a P2P call to " + key + " because you have not initialized that connection yet!");
                }

                client = _p2pClients[key];
            }

            ClientRequest p2pRequest = new ClientRequest()
            {
                ClientContext = context,
                DomainScope = new List<string>(),
                InputType = inputType,
                QueryAudio = inputAudio,
                TraceId = queryLogger.TraceId,
                UnderstandingData = new List<RecoResult>()
            };

            // audio must be uncompressed I guess.... TODO fix that?
            p2pRequest.ClientContext.RemoveCapabilities(ClientCapabilities.SupportsStreamingAudio | ClientCapabilities.SupportsCompressedAudio);
            p2pRequest.ClientContext.AddCapabilities(ClientCapabilities.ServeHtml);
            //p2pRequest.DomainScope.Add(this.Domain);
            p2pRequest.UnderstandingData.Add(luData);

            Task<NetworkResponseInstrumented<ClientResponse>> req = client.MakeQueryRequest(p2pRequest, queryLogger);
            NetworkResponseInstrumented<ClientResponse> r = req.Await();

            ClientResponse response = r.Response;

            return new DialogResult(response.ExecutionResult)
            {
                ResponseText = response.TextToDisplay,
                ResponseHtml = response.HtmlToDisplay,
                ErrorMessage = response.ErrorMessage,
                MultiTurnResult = response.ContinueImmediately ? MultiTurnBehavior.ContinueBasic : MultiTurnBehavior.ContinuePassively,
                SuggestedQueries = response.SuggestedQueries,
                ResponseSSML = response.ResponseSSML,
                ResponseData = response.ResponseData,
                AugmentedQuery = DialogHelpers.BuildTaggedDataFromPlainString(response.AugmentedFinalQuery),
                ClientAction = response.ResponseAction,
                TriggerKeywords = response.TriggerKeywords,
                TriggerKeywordExpireTimeSeconds = response.TriggerKeywordExpireTimeSeconds,
            };
        }
    }
}
