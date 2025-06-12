

namespace Durandal.Plugins.Time
{
        using Durandal.Common.Time.Timex;
    using Durandal.Common.Time.Timex.Enums;
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Ontology;
    using Durandal.Common.IO;
    using Durandal.Common.MathExt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Time.TimeZone;
    using Durandal.CommonViews;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Durandal.Common.Statistics;
    using Durandal.Common.NLP.Language;

    public class TimePlugin : DurandalPlugin
    {
        private IRealTimeProvider _realTime;
        private IRandom _rand;
        private TimeZoneResolver _timezoneResolver;

        public TimePlugin() : this(DefaultRealTimeProvider.Singleton, new FastRandom())
        {
        }

        public TimePlugin(IRealTimeProvider timeProvider, IRandom random) : base("time")
        {
            _realTime = timeProvider;
            _rand = random;
        }

        private long GetCurrentEpochTime()
        {
            return (_realTime.Time.UtcTicks - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks) / TimeSpan.TicksPerMillisecond;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode getTimeNode = returnVal.CreateNode(ResolveTime);
            returnVal.AddStartState("query_time", getTimeNode);
            getTimeNode.CreateNormalEdge("query_time_multiturn", getTimeNode);
            returnVal.AddStartState("get_relative_world_time", WorldTimeRelative);

            returnVal.AddStartState("query_timezone", ResolveTimezone);

            IConversationNode changeTimerNode = returnVal.CreateNode(ChangeTimer);
            returnVal.AddStartState("change_timer", changeTimerNode);
            changeTimerNode.CreateNormalEdge("change_timer", changeTimerNode);

            return returnVal;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            services.LanguageGenerator.RegisterCustomCode("StoppedTimer", this.RunLgForTimerResponse, LanguageCode.Parse("en-US"));
            _timezoneResolver = new TimeZoneResolver(services.Logger);
            bool success = await _timezoneResolver.Initialize(services.FileSystem, services.PluginDataDirectory.Combine("IANA")).ConfigureAwait(false);
            if (!success)
            {
                _timezoneResolver = null;
            }
        }

        /// <summary>
        /// Master handler of time queries
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <returns></returns>
        public async Task<PluginResult> ResolveTime(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Collect slots
            string fieldSlot = DialogHelpers.TryGetAnySlotValue(queryWithContext, "field");
            SlotValue timeSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "time");
            SlotValue locationSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "location");

            // See what fields they requested
            if (string.IsNullOrEmpty(fieldSlot))
            {
                if (timeSlot != null)
                {
                    services.Logger.Log("Time slot with no field, assuming DATE", LogLevel.Vrb);
                    fieldSlot = "DATE"; // This can occur for cases like "when is Christmas" where the field to be queried isn't explicit
                }
                else
                {
                    services.Logger.Log("Time query recieved no field slot, skipping");
                    return new PluginResult(Result.Skip);
                }
            }

            // Resolve the user's current time here since it will be used for many things later
            UserTimeContext userTimeContext = TimeHelpers.ExtractUserTimeContext(queryWithContext.ClientContext, services.Logger, _timezoneResolver, _realTime.Time);

            // Did the user request a location?
            if (locationSlot != null)
            {
                IList<ContextualEntity> locations = locationSlot.GetEntities(services.EntityContext);
                if (locations != null && locations.Count > 0 && locations[0].Entity.IsA<SchemaDotOrg.Place>())
                {
                    return await Scenarios.ScenarioWorldTime(queryWithContext.ClientContext, services, locations[0].Entity.As<SchemaDotOrg.Place>(), fieldSlot, _rand, _realTime, _timezoneResolver, userTimeContext).ConfigureAwait(false);
                }
                else
                {
                    services.Logger.Log("Got a location slot but no entity annotation; falling back to user's local time", LogLevel.Err);
                }
            }

            if (timeSlot == null)
            {
                return await Scenarios.ResolveTimeByUserClock(queryWithContext.ClientContext, services, fieldSlot, _rand, _realTime, _timezoneResolver, userTimeContext).ConfigureAwait(false);
            }
            else
            {
                // Extract the resolved timex and pass that as relative
                TimexContext timeContext = new TimexContext()
                {
                    Normalization = Normalization.Future,
                    ReferenceDateTime = _realTime.Time.UtcDateTime,
                    TemporalType = TemporalType.Date | TemporalType.Time,
                    UseInference = true,
                    WeekdayLogicType = WeekdayLogic.SimpleOffset,
                    AmPmInferenceCutoff = 7
                };

                if (userTimeContext != null)
                {
                    timeContext.ReferenceDateTime = userTimeContext.UserLocalTime.DateTime;
                }

                IList<TimexMatch> timexMatches = timeSlot.GetTimeMatches(TemporalType.Date | TemporalType.Time, timeContext);
                timexMatches = DateTimeProcessors.MergePartialTimexMatches(timexMatches);
                if (timexMatches.Count == 0)
                {
                    services.Logger.Log("A time slot was mentioned, but no timex was extracted! The slot value was " + timeSlot.Value, LogLevel.Wrn);
                    return new PluginResult(Result.Skip)
                    {
                        ResponseText = "I don't know when " + timeSlot.Value + " is."
                    };
                }

                return await Scenarios.ResolveTimeRelative(
                    queryWithContext,
                    services,
                    fieldSlot,
                    timexMatches[0].ExtendedDateTime,
                    timeSlot.Value,
                    _rand,
                    _realTime,
                    _timezoneResolver,
                    userTimeContext).ConfigureAwait(false);
            }
        }

        public async Task<PluginResult> WorldTimeRelative(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Collect slots
            SlotValue timeSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "basis_time");
            SlotValue basisLocationSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "basis_location");
            SlotValue queryLocationSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "query_location");
            
            if (timeSlot == null)
            {
                services.Logger.Log("No basis_time slot for relative world time scenario");
                return new PluginResult(Result.Skip);
            }

            if (basisLocationSlot == null)
            {
                services.Logger.Log("No basis_location slot for relative world time scenario");
                return new PluginResult(Result.Skip);
            }

            if ( queryLocationSlot == null)
            {
                services.Logger.Log("No query_location slot for relative world time scenario");
                return new PluginResult(Result.Skip);
            }

            // Extract the resolved timex and pass that as relative
            TimexContext timeContext = new TimexContext()
            {
                Normalization = Normalization.Present,
                ReferenceDateTime = _realTime.Time.UtcDateTime,
                TemporalType = TemporalType.Time,
                UseInference = true,
                WeekdayLogicType = WeekdayLogic.SimpleOffset,
                AmPmInferenceCutoff = 7
            };

            IList<TimexMatch> timexMatches = timeSlot.GetTimeMatches(TemporalType.Date | TemporalType.Time, timeContext);
            timexMatches = DateTimeProcessors.MergePartialTimexMatches(timexMatches);
            
            IList<Hypothesis<SchemaDotOrg.Place>> contextPlaces = services.EntityHistory.FindEntities<SchemaDotOrg.Place>();

            SchemaDotOrg.Place basisLocation;
            IList<ContextualEntity> placeEntities = basisLocationSlot.GetEntities(services.EntityContext);
            if (string.Equals("CURRENT_LOCATION", basisLocationSlot.Value, StringComparison.Ordinal))
            {
                basisLocation = null;
            }
            else if (string.Equals("ANAPHORA", basisLocationSlot.Value, StringComparison.Ordinal))
            {
                // Try and pull from context
                if (contextPlaces.Count == 0)
                {
                    services.Logger.Log("Basis location refers to anaphora but no location entities are in context; falling back to current location");
                    basisLocation = null;
                }
                else
                {
                    basisLocation = contextPlaces[0].Value;
                }
            }
            else if (placeEntities.Count > 0)
            {
                basisLocation = placeEntities[0].Entity.As<SchemaDotOrg.Place>();
            }
            else
            {
                services.Logger.Log("Basis location has no attached place entity; falling back to current location");
                basisLocation = null;
            }

            SchemaDotOrg.Place queryLocation;
            placeEntities = queryLocationSlot.GetEntities(services.EntityContext);
            if (string.Equals("CURRENT_LOCATION", queryLocationSlot.Value, StringComparison.Ordinal))
            {
                queryLocation = null;
            }
            else if (string.Equals("ANAPHORA", queryLocationSlot.Value, StringComparison.Ordinal))
            {
                // Try and pull from context
                if (contextPlaces.Count == 0)
                {
                    services.Logger.Log("Query location refers to anaphora but no location entities are in context; falling back to current location");
                    queryLocation = null;
                }
                else
                {
                    queryLocation = contextPlaces[0].Value;
                }
            }
            else if (placeEntities.Count > 0)
            {
                queryLocation = placeEntities[0].Entity.As<SchemaDotOrg.Place>();
            }
            else
            {
                services.Logger.Log("Query location has no attached place entity; falling back to current location");
                queryLocation = null;
            }

            return await Scenarios.ScenarioWorldTimeDifference(queryWithContext.ClientContext, services, basisLocation, queryLocation, _rand, _realTime, _timezoneResolver, timexMatches[0].ExtendedDateTime).ConfigureAwait(false);
        }

        public async Task<PluginResult> ResolveTimezone(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Collect slots
            SlotValue locationSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "location");
            
            // Resolve the user's current time here since it will be used for many things later
            UserTimeContext userTimeContext = TimeHelpers.ExtractUserTimeContext(queryWithContext.ClientContext, services.Logger, _timezoneResolver, _realTime.Time);

            // Did the user request a location?
            if (locationSlot != null)
            {
                IList<ContextualEntity> locations = locationSlot.GetEntities(services.EntityContext);
                if (locations != null && locations.Count > 0 && locations[0].Entity.IsA<SchemaDotOrg.Place>())
                {
                    return await Scenarios.ScenarioWorldTimezone(queryWithContext.ClientContext, services, locations[0].Entity.As<SchemaDotOrg.Place>(), _rand, _realTime, _timezoneResolver).ConfigureAwait(false);
                }
            }

            services.Logger.Log("Query timezone intent was given with no location; falling back to user's current timezone", LogLevel.Wrn);
            return new PluginResult(Result.Skip)
            {
                ResponseText = "I don't know what time zone you're asking for"
            };
        }

        #region Timer stuff

        public async Task<PluginResult> ChangeTimer(QueryWithContext queryWithContext, IPluginServices services)
        {
            string action = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "action");
            if (string.Equals(action, "STOP") || string.Equals(action, "PAUSE"))
            {
                return await StopTimer(queryWithContext, services).ConfigureAwait(false);
            }
            else if (string.Equals(action, "START"))
            {
                return await StartTimer(queryWithContext, services).ConfigureAwait(false);
            }
            else
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Unknown timer action string " + action
                };
            }
        }

        private async Task<PluginResult> StartTimer(QueryWithContext queryWithContext, IPluginServices services)
        {
            TimerState timerState = new TimerState();
            timerState.TargetTimeEpoch = GetCurrentEpochTime();
            timerState.IsRunning = true;
            timerState.Valid = true;
            
            // Cache the state in the object store
            services.SessionStore.Put<TimerState>("timerState", timerState);

            PluginResult result = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
            
            // Render the HTML
            TimerView responseHtml = new TimerView();
            responseHtml.countDown = timerState.CountsDown;
            responseHtml.targetTimeEpoch = timerState.TargetTimeEpoch;
            result.ResponseHtml = responseHtml.Render();
            
            return await services.LanguageGenerator.GetPattern("TimerStarted", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt())
                .ApplyToDialogResult(result).ConfigureAwait(false);
        }

        private async Task<PluginResult> StopTimer(QueryWithContext queryWithContext, IPluginServices services)
        {
            TimerState timerState = null;
            
            // First, try and see if there is custom data fields passed by the client. This will give us the most
            // accurate snapshot of the current timer state
            IDictionary<string, string> clientData = queryWithContext.ClientContext.ExtraClientContext;
            if (clientData.ContainsKey("timer_targetTimeEpoch"))
            {
                timerState = new TimerState();
                timerState.Valid = true;
                timerState.TargetTimeEpoch = long.Parse(clientData["timer_targetTimeEpoch"]);
                timerState.MsOnTimer = long.Parse(clientData["timer_msOnTimer"]);
                timerState.IsPaused = bool.Parse(clientData["timer_paused"]);
                timerState.IsRunning = bool.Parse(clientData["timer_running"]);
                timerState.IsElapsed = bool.Parse(clientData["timer_elapsed"]);
                timerState.CountsDown = bool.Parse(clientData["timer_countsDown"]);
            }

            // If that fails, try and look in the local object store
            if (services.SessionStore.ContainsKey("timerState"))
            {
                timerState = services.SessionStore.GetObject<TimerState>("timerState");
            }

            if (timerState == null || !timerState.Valid)
            {
                ILGPattern lg = services.LanguageGenerator.GetPattern("TimerNotStarted", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt());
                return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
            }

            // Apply the stop status
            timerState.IsRunning = false;
            timerState.IsElapsed = false;
            if (timerState.CountsDown)
            {
                // Fix the ms remaining for a countdown timer
                timerState.MsOnTimer = timerState.TargetTimeEpoch - GetCurrentEpochTime();
            }
            else
            {
                // Fix the ms remaining for a countup timer
                timerState.MsOnTimer = GetCurrentEpochTime() - timerState.TargetTimeEpoch;
            }

            TimeSpan spannedTime = TimeSpan.FromMilliseconds(timerState.MsOnTimer);// DateTime.Now.Subtract(services.SessionStore.GetStruct<DateTime>("lastStartTime"));

            ILGPattern pattern = services.LanguageGenerator.GetPattern("StoppedTimer", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt())
                .Sub("time", spannedTime);

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }

        private RenderedLG RunLgForTimerResponse(
            IDictionary<string, object> substitutions,
            ILogger logger, 
            ClientContext clientContext)
        {
            RenderedLG returnVal = new RenderedLG();
            if (!substitutions.ContainsKey("time") || substitutions["time"].GetType() != new TimeSpan().GetType())
            {
                logger.Log("Cannot run custom LG - no time span is provided", LogLevel.Err);
                return returnVal;
            }

            TimeSpan spannedTime = (TimeSpan)substitutions["time"];

            if (clientContext.Locale.Equals(LanguageCode.Parse("en-US")))
            {
                string text;
                if (spannedTime.Hours > 0)
                {
                    text = "Stopped timer after " + spannedTime.Hours +
                           " hours " + spannedTime.Minutes +
                           " minutes and " + spannedTime.Seconds + " seconds";
                }
                else if (spannedTime.Minutes > 0)
                {
                    text = "Stopped timer after " + spannedTime.Minutes +
                           " minutes and " + spannedTime.Seconds + " seconds";
                }
                else
                    text = "Stopped timer after " + spannedTime.Seconds + " seconds";

                returnVal.Text = text;
                returnVal.Spoken = text;
                // FIXME what's the deal here?
                //returnVal.ExtraFields = ClientContext.ExtraClientContext;
                //input.ResponseHtml = new MessageView()
                //{
                //    Content = text,
                //    RequestData = ClientContext.ExtraClientContext
                //}.Render();
            }

            return returnVal;
        }

        #endregion

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
                    InternalName = "Time",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Time",
                    ShortDescription = "Tells you the time, and more!",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What time is it?");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What day is Easter?");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("what time is it in Istanbul?");

                return returnVal;
            }
        }
    }
}
