using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;

namespace Durandal.Plugins.Plugins.OnDevice
{
    using Common.Client.Actions;
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class OnDevicePlugin : DurandalPlugin
    {
        public OnDevicePlugin() : base("ondevice") { }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            returnVal.AddStartState("stop_listening", StopListening);
            return returnVal;
        }

        public async Task<PluginResult> StopListening(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            // Does the client support the StopListening action?
            if (queryWithContext.ClientContext.SupportedClientActions != null &&
                !queryWithContext.ClientContext.SupportedClientActions.Contains(StopListeningAction.ActionName))
            {
                return new PluginResult(Result.Success)
                {
                    ResponseText = "Unfortunately, I can't turn my microphone off",
                    ResponseSsml = "Unfortunately, I can't turn my microphone off",
                    ResponseHtml = new MessageView()
                    {
                        Content = "Unfortunately, I can't turn my microphone off",
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                };
            }

            // Default to a 5-minute delay
            int delaySeconds = 300;

            SlotValue durationSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "duration");
            if (durationSlot != null)
            {
                TimexContext timeContext = new TimexContext()
                {
                    Normalization = Normalization.Present,
                    TemporalType = TemporalType.Duration,
                    UseInference = true
                };

                IList<TimexMatch> timexMatches = durationSlot.GetTimeMatches(TemporalType.Duration, timeContext);
                if (timexMatches.Count > 0)
                {
                    DurationValue complexDuration = timexMatches[0].ExtendedDateTime.Duration;
                    services.Logger.Log("Got duration " + complexDuration.FormatValue());
                }
            }

            // Build the action
            StopListeningAction action = new StopListeningAction()
            {
                DurationSeconds = delaySeconds
            };

            return new PluginResult(Result.Success)
            {
                ResponseText = "OK. I'll be quiet",
                ResponseSsml = "OK. I'll be quiet",
                ResponseHtml = new MessageView()
                {
                    Content = "OK. I'll be quiet.",
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render(),
                ClientAction = JsonConvert.SerializeObject(action)
            };
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "ondevice",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "On-device Actions",
                ShortDescription = "Handles several tasks relating to your local device, such as turning features on or off",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Stop listening for 10 minutes");

            return returnVal;
        }
    }
}
