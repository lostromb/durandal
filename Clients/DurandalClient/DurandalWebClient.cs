using System;
using System.Collections.Generic;
using System.Windows;
using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Net;

namespace Durandal.Common.Client
{
    public class DurandalWebClient
    {
        private Configuration clientConfig;
        private string _clientId;
        private DialogHttpClient _clientInterface;
        private Cache<string> _pageCache;
        private PresentationWebServer _webServer;

        public DurandalWebClient(Configuration config, DialogHttpClient clientInterface)
        {
            clientConfig = config;
            _clientId = Guid.NewGuid().ToString("N");
            _clientInterface = clientInterface;
            _pageCache = new Cache<string>(10);
            _webServer = new PresentationWebServer(config, _pageCache, null);
        }

        public DialogHttpClient GetRawClient()
        {
            return _clientInterface;
        }

        private void PopulateClientContext(ref ClientRequest request)
        {
            request.ClientFlags = (uint)(ClientCapabilities.IsOnLocalMachine |
                    ClientCapabilities.CanRecognizeSpeech |
                    ClientCapabilities.CanSynthesizeSpeech |
                    ClientCapabilities.HasDisplay);
            request.ClientId = _clientId;
            request.ClientData["ClientName"] = "Local text client";
            request.ClientData["ClientLocale"] = "en-us";
            request.ClientData["ClientTime"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            request.ClientData["ClientLatitude"] = clientConfig.GetFloat("clientLatitude", 0).ToString();
            request.ClientData["ClientLongitude"] = clientConfig.GetFloat("clientLongitude", 0).ToString();

            // Send the current screen size
            //request.ClientData["ClientScreenWidth"] = ((int)SystemParameters.VirtualScreenWidth).ToString();
            //request.ClientData["ClientScreenHeight"] = ((int)SystemParameters.VirtualScreenHeight).ToString();
        }

        public ServerResponse MakeCustomDialogRequest(string actionKey)
        {
            ClientRequest request = new ClientRequest();
            PopulateClientContext(ref request);
            request.ClientData["ActionKey"] = actionKey;
            ServerResponse response = _clientInterface.MakeDialogActionRequest(request);
            if (response == null)
            {
                Console.WriteLine("Response was null");
            }
            else if (response.ResponseCode == Result.Success)
            {
                Console.WriteLine("Client response OK!");
                if (!string.IsNullOrWhiteSpace(response.ResponseText))
                    Console.WriteLine(response.ResponseText);
            }
            else if (response.ResponseCode == Result.Skip)
            {
                Console.WriteLine("Input ignored (Shouldn't happen during a dialog action!)");
            }
            else
            {
                Console.WriteLine("An error occurred");
            }

            return response;
        }

        public void Run()
        {
            DataLogger.Log("Running web client");
            _webServer.StartServer("WebClient presentation server");
        }
    }
}
