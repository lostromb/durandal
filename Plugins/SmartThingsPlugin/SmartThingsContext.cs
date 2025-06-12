using Durandal.Answers.SmartThingsAnswer.Devices;
using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Security.OAuth;
using Durandal.Common.NLP;
using Durandal.Common.Statistics;
using Durandal.Common.NLP.Language;

namespace Durandal.Answers.SmartThingsAnswer
{
    public class SmartThingsContext
    {
        private readonly ILogger _logger;
        private readonly OAuthToken _authToken;
        private EndpointInfo _resolvedEndpoint;
        private UserDeviceCollection _devices;

        public SmartThingsContext(ILogger logger, OAuthToken authToken)
        {
            _logger = logger;
            _authToken = authToken;
        }

        public async Task<bool> Initialize(IDataStore userProfile, string oauthClientId)
        {
            _resolvedEndpoint = await ResolveEndpoint(userProfile, oauthClientId);
            _devices = await PopulateDevices(userProfile);
            return true;
        }

        private async Task<EndpointInfo> ResolveEndpoint(IDataStore userProfile, string oauthClientId)
        {
            // OPT: cache the endpoint in userprofile rather than querying it each time

            WebClient client = new WebClient();
            client.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + _authToken.Token);
            string endpointJson = await client.DownloadStringTaskAsync("https://graph.api.smartthings.com/api/smartapps/endpoints");
            List<EndpointInfo> endpoints = JsonConvert.DeserializeObject<List<EndpointInfo>>(endpointJson);

            foreach (EndpointInfo endpoint in endpoints)
            {
                // Look for the endpoint that matches the current oauth client id
                if (string.Equals(endpoint.oauthClient.clientId, oauthClientId, StringComparison.OrdinalIgnoreCase))
                {
                    return endpoint;
                }
            }

            return null;
        }

        public async Task<UserDeviceCollection> PopulateDevices(IDataStore userProfile)
        {
            UserDeviceCollection returnVal = new UserDeviceCollection()
                {
                    Devices = new Dictionary<string, SmartDevice>(),
                    LastUpdateTime = new DateTime(1900, 1, 1, 0, 0, 0)
                };

            // First look in the user's data store
            if (userProfile.ContainsKey("devices"))
            {
                returnVal = userProfile.GetObject("devices", returnVal);
            }

            CommandResult statusResult = await SendCommandAsync("GET", "/status");
            if (!statusResult.Success)
            {
                _logger.Log("Failed to populate devices: " + statusResult.ErrorMessage, LogLevel.Err);
            }
            else
            {
                _logger.Log("Device json: " + statusResult.DataRecieved);

                // Parse the result from the SmartApp and union it with what we already know
                var reader = new JsonTextReader(new StringReader(statusResult.DataRecieved));
                JArray devices = JArray.Load(reader);
                foreach (JObject device in devices.Children<JObject>())
                {
                    if (device["type"] == null)
                    {
                        _logger.Log("Device has no type: " + device.ToString(), LogLevel.Wrn);
                        continue;
                    }

                    if (device["id"] == null)
                    {
                        _logger.Log("Device has no id: " + device.ToString(), LogLevel.Wrn);
                        continue;
                    }

                    string id = device["id"].Value<string>();
                    string type = device["type"].Value<string>();
                    DeviceCapability typeCapability = DeviceCapability.None;
                    if (string.Equals(type, "switch", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCapability |= DeviceCapability.Switch;
                    }
                    if (string.Equals(type, "dimmer", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCapability |= DeviceCapability.SwitchLevel;
                    }
                    if (string.Equals(type, "colorControl", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCapability |= DeviceCapability.ColorControl;
                    }

                    SmartDevice targetDevice;
                    // Is there already a device with this ID?
                    if (returnVal.Devices.ContainsKey(id))
                    {
                        // Then just add the new capabilities to the existing device entry
                        targetDevice = returnVal.Devices[id];
                        targetDevice.AddCapability(typeCapability);
                    }
                    else
                    {
                        string name = device["name"].Value<string>() ?? device["label"].Value<string>() ?? "Unknown Device";
                        IList<string> knownAs = new List<string>();
                        targetDevice = new SmartDevice(name, id, typeCapability, knownAs);
                        returnVal.Devices[id] = targetDevice;
                    }

                    // Set the current state of all devices in the context according to their capabilities
                    if (typeCapability.HasFlag(DeviceCapability.Switch) && device["state.value"] != null)
                    {
                        targetDevice.StateSwitch = string.Equals("on", device["state.value"].Value<string>(), StringComparison.OrdinalIgnoreCase);
                    }
                    if (typeCapability.HasFlag(DeviceCapability.SwitchLevel) && device["state.value"] != null)
                    {
                        targetDevice.StateLevel = float.Parse(device["state.value"].Value<string>());
                    }
                }
            }

            userProfile.Put("devices", returnVal);

            return returnVal;
        }

        public SmartDevice GetDeviceById(string id)
        {
            if (_devices.Devices.ContainsKey(id))
            {
                return _devices.Devices[id];
            }

            return null;
        }

        public async Task<SmartDevice> GetDeviceByName(LexicalString name, IPluginServices services)
        {
            IList<NamedEntity<SmartDevice>> switchNames = new List<NamedEntity<SmartDevice>>();
            foreach (SmartDevice device in _devices.Devices.Values)
            {
                List<LexicalString> knownAs = new List<LexicalString>();
                foreach (string knownAsPlaintext in device.KnownAs)
                {
                    knownAs.Add(new LexicalString(knownAsPlaintext));
                }

                knownAs.Add(new LexicalString(device.Name));
                switchNames.Add(new NamedEntity<SmartDevice>(device, knownAs));
            }

            IList<Hypothesis<SmartDevice>> selectedSwitch = await services.EntityResolver.ResolveEntity(name, switchNames, LanguageCode.Parse("en-US"), services.Logger);

            if (selectedSwitch.Count == 0 || selectedSwitch[0].Conf < 0.5)
            {
                return null;
            }

            return selectedSwitch[0].Value;
        }

        public CommandResult SendCommand(string method, string url, string commandJson = null)
        {
            CommandResult result = new CommandResult()
            {
                DataSent = method + " " + url + (commandJson != null ? " " + commandJson : string.Empty),
                Success = false
            };

            string completeUrl = _resolvedEndpoint.uri + url;
            WebClient client = new WebClient();
            client.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + _authToken.Token);
            try
            {
                if (!string.IsNullOrEmpty(commandJson))
                {
                    result.DataRecieved = client.UploadString(completeUrl, method, commandJson);
                    result.Success = true;
                    return result;
                }
                else if (method.Equals("GET"))
                {
                    result.DataRecieved = client.DownloadString(completeUrl);
                    result.Success = true;
                    return result;
                }
                else
                {
                    result.ErrorMessage = "Unknown method, or no POST body supplied";
                    return result;
                }
            }
            catch (WebException e)
            {
                result.ErrorMessage = e.Message;
                _logger.Log("Command error: " + result.DataSent + " : " + e.Message, LogLevel.Err);
                if (e.Response != null)
                {
                    Stream responseStream = e.Response.GetResponseStream();
                    if (responseStream != null && responseStream.CanRead)
                    {
                        MemoryStream data = new MemoryStream();
                        responseStream.CopyTo(data);
                        byte[] rawPayload = data.ToArray();
                        result.DataRecieved = Encoding.UTF8.GetString(rawPayload);
                        _logger.Log("Response from service: " + result.DataRecieved, LogLevel.Err);
                    }
                }
            }

            return result;
        }

        public async Task<CommandResult> SendCommandAsync(string method, string url, string commandJson = null)
        {
            CommandResult result = new CommandResult()
            {
                DataSent = method + " " + url + " " + commandJson,
                Success = false
            };
            
            string completeUrl = _resolvedEndpoint.uri + url;
            WebClient client = new WebClient();
            client.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + _authToken.Token);
            try
            {
                if (!string.IsNullOrEmpty(commandJson))
                {
                    result.DataRecieved = await client.UploadStringTaskAsync(completeUrl, method, commandJson);
                    result.Success = true;
                    return result;
                }
                else if (method.Equals("GET"))
                {
                    result.DataRecieved = await client.DownloadStringTaskAsync(completeUrl);
                    result.Success = true;
                    return result;
                }
                else
                {
                    result.ErrorMessage = "Unknown method, or no POST body supplied";
                    return result;
                }
            }
            catch (WebException e)
            {
                result.ErrorMessage = e.Message;
                _logger.Log("Command error: " + result.DataSent + " : " + e.Message, LogLevel.Err);
                if (e.Response != null)
                {
                    Stream responseStream = e.Response.GetResponseStream();
                    if (responseStream != null && responseStream.CanRead)
                    {
                        MemoryStream data = new MemoryStream();
                        responseStream.CopyTo(data);
                        byte[] rawPayload = data.ToArray();
                        result.DataRecieved = Encoding.UTF8.GetString(rawPayload);
                        _logger.Log("Response from service: " + result.DataRecieved, LogLevel.Err);
                    }
                }
            }

            return result;
        }
    }
}
