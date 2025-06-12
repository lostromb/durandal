using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Durandal.API;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Durandal.Common.Net;

namespace Durandal.Common.Client
{
    public class DurandalTextClient
    {
        private Configuration clientConfig;
        private string _clientId;
        private DialogHttpClient _clientInterface;
        private Cache<string> _pageCache;
        private PresentationWebServer _webServer;
        private string clientLatitude;
        private string clientLongitude;

        public DurandalTextClient(Configuration config, DialogHttpClient clientInterface)
        {
            clientConfig = config;
            _clientId = Guid.NewGuid().ToString("N");
            _clientInterface = clientInterface;
            _pageCache = new Cache<string>(10);
            _webServer = new PresentationWebServer(config, _pageCache, null);
            GetGeoIPCoords();
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
            request.ClientData["ClientLatitude"] = clientLatitude;
            request.ClientData["ClientLongitude"] = clientLongitude;
            
            // Send the current screen size
            request.ClientData["ClientScreenWidth"] = ((int)SystemParameters.VirtualScreenWidth).ToString();
            request.ClientData["ClientScreenHeight"] = ((int)SystemParameters.VirtualScreenHeight).ToString();
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
            DataLogger.Log("Initializing text client");
            DataLogger.Log("Running client");
            _webServer.StartServer("Text Client Presentation Server");

            while (true)
            {
                Console.WriteLine("Input:");
                string recoResult = Console.ReadLine();
                ClientRequest request = new ClientRequest();
                request.Queries = new List<SpeechHypothesis>();
                request.Queries.Add(new SpeechHypothesis()
                {
                    Utterance = recoResult,
                    Confidence = 1.0f
                });
                PopulateClientContext(ref request);

                ServerResponse durandalResult = _clientInterface.MakeQueryRequest(request);

                if (durandalResult == null)
                {
                    Console.WriteLine("Response was null");
                }
                else if (durandalResult.ResponseCode == Result.Success)
                {
                    Console.WriteLine("Client response OK!");
                    if (!string.IsNullOrWhiteSpace(durandalResult.ResponseText))
                        Console.WriteLine(durandalResult.ResponseText);

                    string clientURL = string.Empty;
                    // Did the client return an action URL?
                    if (durandalResult.ResponseData.ContainsKey("ActionURL"))
                    {
                        // Set top open that URL
                        clientURL = durandalResult.ResponseData["ActionURL"];
                    }

                    // Did the client return an HTML page?
                    if (durandalResult.ResponseData.ContainsKey("HTMLPage"))
                    {
                        // Store the page in the cache and generate a URL to access it
                        string pageKey = _pageCache.Store(durandalResult.ResponseData["HTMLPage"]);
                        clientURL = _webServer.GetBaseURL() + "/dialog?page=" + pageKey;
                    }

                    if (!string.IsNullOrWhiteSpace(clientURL))
                    {
                        //OpenURL(clientURL);
                    }
                }
                else if (durandalResult.ResponseCode == Result.Skip)
                {
                    Console.WriteLine("Input ignored");
                }
                else
                {
                    Console.WriteLine("An error occurred");
                }
            }
        }

        private void GetGeoIPCoords()
        {
            // Default to Seattle
            clientLatitude = clientConfig.GetString("clientLatitude", "47.617108");
            clientLongitude = clientConfig.GetString("clientLongitude", "-122.191346");
            /// http://geoip.prototypeapp.com/api/locate
            /*string json = DurandalUtils.HTTPGet("http://www.telize.com/geoip");
            if (!string.IsNullOrWhiteSpace(json))
            {
                JsonSerializer ser = JsonSerializer.Create(new JsonSerializerSettings());
                JObject result = ser.Deserialize(new JsonTextReader(new StringReader(json))) as JObject;
                clientLatitude = result["latitude"].Value<string>();
                clientLongitude = result["longitude"].Value<string>();
            }*/
        }
    }
}
