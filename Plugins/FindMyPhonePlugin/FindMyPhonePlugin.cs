
namespace Durandal.Plugins.FindMyPhone
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class FindMyPhonePlugin : DurandalPlugin
    {
        //private readonly string TwimlUrl = "http://durandal.dnsalias.net/twilio.xml";
        private string _sourceNumber;
        private string _accountSid;
        private string _twilioKey;

        public FindMyPhonePlugin() : base("findmyphone") { }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            returnVal.AddStartState("find_phone", FindMyPhone);
            return returnVal;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _sourceNumber = services.PluginConfiguration.GetString("sourceNumber");
            _accountSid = services.PluginConfiguration.GetString("accountSid");
            _twilioKey = services.PluginConfiguration.GetString("twilioKey");
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
        }

        public async Task<PluginResult> FindMyPhone(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (string.IsNullOrEmpty(_sourceNumber) ||
                string.IsNullOrEmpty(_accountSid) ||
                string.IsNullOrEmpty(_twilioKey))
            {
                services.Logger.Log("Invalid Twilio configuration!", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            //string nameSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "name");
            //string relationSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "relation");
            //if (nameSlot == null && relationSlot == null)
            //{
            //    services.Logger.Log("No slot; skipping", LogLevel.Wrn);
            //    return new PluginResult(Result.Skip);
            //}

            //MakeCall("+14253949364", services.Logger);

            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            return new PluginResult(Result.Success)
            {
                ResponseText = "OK. I'm calling your phone now",
                ResponseSsml = "OK. I'm calling your phone now",
                ResponseHtml = new MessageView() { Content = "OK. I'm calling your phone now" }.Render()
            };
        }

        private async Task MakeCall(string targetNumber, ILogger queryLogger)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            //try
            //{
            //    HttpClient client = new HttpClient()
            //    {
            //        BaseAddress = new Uri("https://api.twilio.com")
            //    };

            //    HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, "/2010-04-01/Accounts/" + _accountSid + "/Calls.json");
            //    IDictionary<string, string> parts = new Dictionary<string, string>();
            //    parts.Add("To", targetNumber);
            //    parts.Add("From", _sourceNumber);
            //    parts.Add("Url", TwimlUrl);
            //    message.Content = new FormUrlEncodedContent(parts);
            //    string httpUser = _accountSid + ":" + _twilioKey;
            //    string encodedUser = Convert.ToBase64String(Encoding.UTF8.GetBytes(httpUser));
            //    message.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedUser);
            //    queryLogger.Log("Sending call from " + _sourceNumber + " to " + targetNumber);
            //    await client.SendAsync(message).ConfigureAwait(false);
            //}
            //catch (Exception e)
            //{
            //    queryLogger.Log(e, LogLevel.Err);
            //}
        }

        protected override PluginInformation GetInformation(IFileSystem resourceManager, VirtualPath pluginDataDirectory)
        {
            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "findmyphone",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Find My Phone",
                ShortDescription = "Calls phones to locate them",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Locate my phone");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Find my wife's phone");

            return returnVal;
        }
    }
}
