using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;

namespace Durandal.Plugins.Reminder
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
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    public class ReminderPlugin : DurandalPlugin
    {
        private const string ReminderSubjectKey = "ReminderSubject";
        private const string ReminderPhrasingKey = "ReminderPhrasing";
        private const string ReminderTimeKey = "ReminderTime";
        private const string EnabledThanksKey = "EnabledThanks";

        public ReminderPlugin()
            : base("reminder")
        {
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode startCreateReminderNode = returnVal.CreateNode(this.SetReminder);
            IConversationNode cancelReminderNode = returnVal.CreateNode(this.CancelReminder);
            IConversationNode inputTimeNode = returnVal.CreateNode(this.ClarifyTime);
            IConversationNode inputTitleNode = returnVal.CreateNode(this.ClarifyTitle);
            IConversationNode thanksNode = returnVal.CreateNode(this.AcknowledgeThanks);

            startCreateReminderNode.CreateNormalEdge("date_time_input", inputTimeNode);
            startCreateReminderNode.CreateNormalEdge("title_input", inputTitleNode);
            startCreateReminderNode.CreateCommonEdge("thanks", thanksNode);
            startCreateReminderNode.CreateCommonEdge("deny", cancelReminderNode);

            inputTitleNode.CreateNormalEdge("date_time_input", inputTimeNode);

            inputTimeNode.EnableRetry(this.RetryTimeEntry);

            returnVal.AddStartState("set_reminder", startCreateReminderNode);
            return returnVal;
        }

        public async Task<PluginResult> SetReminder(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Clear any past context
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            services.SessionStore.ClearAll();

            string reminderSubject = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "title");
            string reminderPhrasing = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "phrasing");

            if (string.IsNullOrWhiteSpace(reminderSubject))
            {
                // TODO: Do a second pass to ask for the subject
                services.Logger.Log("no subject extracted", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }
            if (string.IsNullOrWhiteSpace(reminderPhrasing))
            {
                reminderPhrasing = "to";
            }

            reminderSubject = ConvertFirstToSecondPerson(reminderSubject);

            ExtendedDateTime reminderTime = this.TryExtractTime(queryWithContext.Understanding, queryWithContext.ClientContext.ExtraClientContext);

            services.SessionStore.Put(ReminderSubjectKey, reminderSubject);
            services.SessionStore.Put(ReminderPhrasingKey, reminderPhrasing);
            
            // Is second turn required?
            if (reminderTime == null)
            {
                // Missing a time
                return this.CreateStandardResult("When did you want to be reminded?", queryWithContext.ClientContext, MultiTurnBehavior.ContinueBasic);
            }

            if (!reminderTime.SetParts.HasFlag(DateTimeParts.Hour))
            {
                StoreExtendedDateTime(ReminderTimeKey, reminderTime, services.SessionStore);
                // Underspecified time
                return this.CreateStandardResult("At what time should I remind you?", queryWithContext.ClientContext, MultiTurnBehavior.ContinueBasic);
            }

            if (!this.SetReminder(reminderSubject, reminderTime, services))
            {
                return this.CreateStandardResult("I'm sorry. I couldn't create the reminder", queryWithContext.ClientContext);
            }

            services.SessionStore.Put(EnabledThanksKey, true);
            return this.CreateStandardResult("OK. I'll remind you " + reminderPhrasing + " " + reminderSubject,
                queryWithContext.ClientContext, MultiTurnBehavior.ContinuePassively);
        }
        
        private void StoreExtendedDateTime(string key, ExtendedDateTime time, IDataStore store)
        {
            IList<string> pairs = new List<string>();
            foreach (var pair in time.OriginalTimexDictionary)
            {
                pairs.Add(pair.Key + "=" + pair.Value);
            }
            store.Put(key, string.Join(";", pairs));
        }

        // TODO fix this method
        private ExtendedDateTime RetrieveExtendedDateTime(string key, IDataStore store)
        {
            string rawVal = store.GetString(key);
            string[] parts = rawVal.Split(';');
            return null;
        }

        private ExtendedDateTime TryExtractTime(RecoResult luResult, IDictionary<string, string> clientData)
        {
            SlotValue queryTag = DialogHelpers.TryGetSlot(luResult, "reminder_time");

            if (queryTag == null)
            {
                return null;
            }

            // TODO: FIX
            DateTime referenceTime = DateTime.Now;

            // Try and extract the time
            TimexContext newTimexContext = new TimexContext()
            {
                Normalization = Normalization.Future,
                TemporalType = TemporalType.All,
                UseInference = true,
                WeekdayLogicType = WeekdayLogic.SimpleOffset,
                ReferenceDateTime = referenceTime
            };

            IList<TimexMatch> matchList = queryTag.GetTimeMatches(
                TemporalType.Date | TemporalType.Time, 
                newTimexContext);

            // TODO: Clean up this code
            TimexMatch timexMatch = null;
            if (matchList.Count > 0)
                timexMatch = matchList[0];

            if (timexMatch != null)
            {
                return timexMatch.ExtendedDateTime;
            }

            return null;
        }

        public async Task<PluginResult> CancelReminder(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            services.SessionStore.ClearAll();
            return this.CreateStandardResult("I cancelled that reminder", queryWithContext.ClientContext);
        }

        public async Task<PluginResult> ClarifyTime(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            ExtendedDateTime newTime = this.TryExtractTime(queryWithContext.Understanding, queryWithContext.ClientContext.ExtraClientContext);
            if (newTime == null)
            {
                return this.CreateStandardResult("At what time should I remind you?", queryWithContext.ClientContext, MultiTurnBehavior.ContinueBasic);
            }

            ExtendedDateTime reminderTime = RetrieveExtendedDateTime(ReminderTimeKey, services.SessionStore);
            string reminderSubject = services.SessionStore.GetString(ReminderSubjectKey, string.Empty);
            string reminderPhrasing = services.SessionStore.GetString(ReminderPhrasingKey, string.Empty);

            if (reminderTime == null)
            {
                reminderTime = newTime;
            }
            else
            {
                IList<TimexMatch> matches = new List<TimexMatch>();
                matches.Add(new TimexMatch()
                    {
                        ExtendedDateTime = newTime
                    });
                matches.Add(new TimexMatch()
                    {
                        ExtendedDateTime = reminderTime
                    });
                matches = DateTimeProcessors.MergePartialTimexMatches(matches);
                reminderTime = matches[0].ExtendedDateTime;
            }
            services.Logger.Log(reminderTime.FormatValue());
            
            if (!this.SetReminder(reminderSubject, reminderTime, services))
            {
                return this.CreateStandardResult("I'm sorry. I couldn't create the reminder", queryWithContext.ClientContext);
            }

            services.SessionStore.Put(EnabledThanksKey, true);
            return this.CreateStandardResult("OK. I'll remind you " + reminderPhrasing + " " + reminderSubject,
                queryWithContext.ClientContext, MultiTurnBehavior.ContinuePassively);
        }

        public async Task<PluginResult> ClarifyTitle(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            string reminderSubject = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "title");
            string reminderPhrasing = "to";

            ExtendedDateTime reminderTime = RetrieveExtendedDateTime(ReminderTimeKey, services.SessionStore);

            if (reminderTime == null)
            {
                services.SessionStore.Put(ReminderSubjectKey, reminderSubject);
                services.SessionStore.Put(ReminderPhrasingKey, reminderPhrasing);
                return this.CreateStandardResult("At what time should I remind you?", queryWithContext.ClientContext, MultiTurnBehavior.ContinueBasic);
            }

            if (!this.SetReminder(reminderSubject, reminderTime, services))
            {
                services.SessionStore.ClearAll();
                return this.CreateStandardResult("I'm sorry. I couldn't create the reminder", queryWithContext.ClientContext);
            }

            services.SessionStore.Put(EnabledThanksKey, true);
            return this.CreateStandardResult("OK. I'll remind you " + reminderPhrasing + " " + reminderSubject,
                queryWithContext.ClientContext, MultiTurnBehavior.ContinuePassively);
        }

        public async Task<PluginResult> RetryTimeEntry(QueryWithContext input, IPluginServices services)
        {
            // Todo fix
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            return null;
        }

        private bool SetReminder(string title, ExtendedDateTime time, IPluginServices services)
        {
            // Test that the time can be parsed.
            DateTime parsedTime;
            if (!DateTimeParsers.TryParseISOIntoLocalDateTime(time.FormatValue(), out parsedTime))
            {
                services.Logger.Log("Could not parse time " + time.FormatValue(), LogLevel.Err);
                return false;
            }

            // Send a post request to the remote server
            string timeString = string.Format(
                "value=\"{0}\" quant=\"{1}\" freq=\"{2}\" mod=\"{3}\" comment=\"{4}\"",
                time.FormatValue(),
                time.FormatQuantity(),
                time.FormatFrequency(),
                time.FormatMod(),
                time.FormatComment());
            services.Logger.Log(timeString);
            return this.CreateReminder(title, timeString);
        }

        //private HttpStatusCode PostRequest(string url, string parameterString)
        //{
        //    byte[] postData = Encoding.UTF8.GetBytes(parameterString);

        //    // Construct the request
        //    HttpWebRequest webRequest = HttpWebRequest.Create(url) as HttpWebRequest;
        //    webRequest.Method = "POST";
        //    webRequest.ContentType = "application/x-www-form-urlencoded";
        //    webRequest.Headers[HttpRequestHeader.ContentEncoding] = "UTF-8";
        //    webRequest.Headers[HttpRequestHeader.AcceptCharset] = "UTF-8";
        //    webRequest.ContentLength = postData.Length;
        //    // Write the playload
        //    Stream postDataStream = webRequest.GetRequestStream();
        //    postDataStream.Write(postData, 0, postData.Length);
        //    postDataStream.Close();
        //    // Get the response code
        //    HttpWebResponse response = webRequest.GetResponse() as HttpWebResponse;

        //    /*for debug
        //    Stream Answer = response.GetResponseStream();
        //    StreamReader _Answer = new StreamReader(Answer);
        //    Console.WriteLine(_Answer.ReadToEnd());*/

        //    return response.StatusCode;
        //}

        private bool CreateReminder(string reminderText, string timeString)
        {
            return true;
            /*string id = Guid.NewGuid().ToString();
            string parameterString = "id=" + id + "&text=" + reminderText + "&time=" + timeString;
            string url = GetConfig().GetString("serverURL", "localhost") + "/create_reminder.php";
            return PostRequest(url, parameterString) == HttpStatusCode.OK;*/
        }

        public async Task<PluginResult> AcknowledgeThanks(QueryWithContext input, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            if (services.SessionStore.GetBool(EnabledThanksKey, false))
            {
                services.SessionStore.Remove(EnabledThanksKey);
                return this.CreateStandardResult("I aim to please", input.ClientContext);
            }
            
            return new PluginResult(Result.Skip);
        }

        /// <summary>
        /// Returns a simple one-line message rendered as audio, text, and html
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <param name="multiturn"></param>
        /// <returns></returns>
        private PluginResult CreateStandardResult(string message, ClientContext context, MultiTurnBehavior multiturn = null)
        {
            return new PluginResult(Result.Success)
            {
                ResponseText = message,
                ResponseSsml = message,
                MultiTurnResult = multiturn == null ? MultiTurnBehavior.None : multiturn,
                ResponseHtml = new MessageView()
                {
                    Content = message,
                    ClientContextData = context.ExtraClientContext
                }.Render()
            };
        }

        /// <summary>
        /// Converts phrases so as to change the reference direction
        /// "remind ME to get MY things" => "remind YOU to get YOUR things"
        /// TODO: What the heck is going on with this function
        /// TODO: Move these functions to EnglishLanguageHelpers
        /// </summary>
        /// <param name="firstPersonString"></param>
        private static string ConvertFirstToSecondPerson(string firstPersonString)
        {
            string secondPersonString = firstPersonString;
            secondPersonString = secondPersonString.Replace(" my ", " your ");
            secondPersonString = secondPersonString.Replace(" I ", " you ");
            secondPersonString = secondPersonString.Replace(" me ", " you ");
            secondPersonString = secondPersonString.Replace(" mine ", " yours ");
            return secondPersonString;
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            using (MemoryStream pngStream = new MemoryStream())
            {
                if (pluginDataDirectory != null && pluginDataManager != null)
                {
                    VirtualPath iconFile = pluginDataDirectory + "\\icon.png";
                    if (pluginDataManager.Exists(iconFile))
                    {
                        using (Stream iconStream = pluginDataManager.OpenStream(iconFile, FileOpenMode.Open, FileAccessMode.Read))
                        {
                            iconStream.CopyTo(pngStream);
                        }
                    }
                }

                PluginInformation returnVal = new PluginInformation()
                {
                    InternalName = "Reminder",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Reminders",
                    ShortDescription = "Reminds you of things",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Remind me to take out the garbage at 6:00");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Remind me about my appointment tomorrow");

                return returnVal;
            }
        }
    }
}
