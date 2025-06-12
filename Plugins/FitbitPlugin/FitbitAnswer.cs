using Durandal.Plugins.Fitbit.Html;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Durandal.API;
using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Security.OAuth;
using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Common.Time;
using Durandal.Common.UnitConversion;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.Time.Timex.Client;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Newtonsoft.Json;
using Durandal.Common.File;
using Durandal.Common.Client.Actions;
using System.Threading;

namespace Durandal.Plugins.Fitbit
{
    public class FitbitAnswer : DurandalPlugin
    {
        private static readonly IRandom _random = new FastRandom();
        private readonly IHttpClientFactory _overrideHttpClientFactory = null;
        private IRealTimeProvider _realTimeProvider;

        private FitbitService _service;

        public FitbitAnswer()
            : base(Constants.FITBIT_DOMAIN)
        {
            _overrideHttpClientFactory = null;
            _realTimeProvider = DefaultRealTimeProvider.Singleton;
        }

        public FitbitAnswer(IHttpClientFactory fitbitServiceClient, IRealTimeProvider timeProvider) : base(Constants.FITBIT_DOMAIN)
        {
            _overrideHttpClientFactory = fitbitServiceClient;
            _realTimeProvider = timeProvider;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _service = new FitbitService(_overrideHttpClientFactory ?? services.HttpClientFactory, services.Logger.Clone("FitbitService"));
            await DurandalTaskExtensions.NoOpTask;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            tree.AddStartState(Constants.INTENT_GET_ACTIVITY, ShowActivity);
            tree.AddStartState(Constants.INTENT_GET_MEASUREMENT, ShowMeasurement);
            tree.AddStartState(Constants.INTENT_GET_GOALS, ShowGoal);
            tree.AddStartState(Constants.INTENT_GET_REMAINING, ShowGoalProgress);
            tree.AddStartState(Constants.INTENT_GET_LEADERBOARD, ShowLeaderboard);
            tree.AddStartState(Constants.INTENT_GET_COUNT, ShowExerciseCount);
            //tree.AddStartState(Constants.INTENT_SET_GOAL, SetNewGoal);
            tree.AddStartState(Constants.INTENT_LOGOUT, LogOut);
            tree.AddStartState(Constants.INTENT_HELP, ShowHelp);
            tree.AddStartState(Constants.INTENT_FIND_ALARM, ShowAlarms);

            IConversationNode setAlarmNode = tree.CreateNode(SetAlarm);
            tree.AddStartState(Constants.INTENT_SET_ALARM, setAlarmNode);
            setAlarmNode.EnableRetry(SetAlarm);
            setAlarmNode.CreateNormalEdge(Constants.INTENT_SET_ALARM, setAlarmNode); // ????? is this desired?
            setAlarmNode.CreateNormalEdge(Constants.INTENT_ENTER_TIME, setAlarmNode);
            setAlarmNode.CreateNormalEdge(Constants.INTENT_ENTER_MERIDIAN, setAlarmNode);
            //alarm1Node.CreateNormalEdge("enter_device", alarm1Node);

            IConversationNode logActivityStartNode = tree.CreateNode(LogActivity);
            IConversationNode logFoodPromptConfirmNode = tree.CreateNode(LogActivity, "LogFoodPromptConfirm");
            IConversationNode logFoodConfirmNode = tree.CreateNode(LogFoodConfirm);
            IConversationNode logFoodDenyNode = tree.CreateNode(LogFoodCancel);

            tree.AddStartState(Constants.INTENT_LOG_ACTIVITY, logActivityStartNode);
            logFoodPromptConfirmNode.CreateCommonEdge("confirm", logFoodConfirmNode);
            logFoodPromptConfirmNode.CreateCommonEdge("deny", logFoodDenyNode);
            
            // to handle help / unknown intents I need to run the skill while it is registered as the side speech intent handler
            tree.AddStartState(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, ChitChat);
            tree.AddStartState(DialogConstants.SIDE_SPEECH_INTENT, ChitChat);
            return tree;
        }

        private static readonly OAuthConfig OAUTH_CONFIG = new OAuthConfig()
        {
            AuthUri = "https://www.fitbit.com/oauth2/authorize",
            TokenUri = "https://api.fitbit.com/oauth2/token",
            ClientId = "22CMNM",
            ClientSecret = "f90cccacc40d2b9c21a90c6524509544",
            ConfigName = "default",
            Scope = "activity heartrate location nutrition profile settings sleep social weight",
            Type = OAuthFlavor.OAuth2,
            UsePKCE = false,
            AuthorizationHeader = "Basic MjJDTU5NOmY5MGNjY2FjYzQwZDJiOWMyMWE5MGM2NTI0NTA5NTQ0"
        };

        private async Task<OAuthToken> TryGetOauthToken(QueryWithContext query, IPluginServices services)
        {
            // Look first in context data
            if (query.ClientContext.ExtraClientContext != null && query.ClientContext.ExtraClientContext.ContainsKey("oauth"))
            {
                return new OAuthToken()
                {
                    Token = query.ClientContext.ExtraClientContext["oauth"]
                };
            }

            return await services.TryGetOAuthToken(OAUTH_CONFIG, query.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton);
        }

        public async Task<PluginResult> ShowActivity(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            // Verify authentication level first to prevent spoofing
            //if (queryWithContext.AuthenticationLevel != ClientAuthenticationLevel.Authorized &&
            //    queryWithContext.AuthScope.HasFlag(ClientAuthenticationScope.User))
            //{
            //    return new PluginResult(Result.Success)
            //    {
            //        ResponseText = "You are not authorized"
            //    };
            //}

            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            // Try and extract a time slot and resolve user's local time
            TimeResolutionInfo timeContext = Helpers.ResolveDate(fitbitUserProfile, queryWithContext, _realTimeProvider);

            string orderRefSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_ORDER_REF);
            string statTypeSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_STAT_TYPE);
            string activityTypeSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_ACTIVITY_TYPE);

            if (string.Equals(Constants.CANONICAL_ORDER_REF_PAST, orderRefSlot))
            {
                // if we ask about "my last exercise", show the summary of that fitness activity
                return await Scenarios.ShowLastExercise(_service, queryWithContext, pluginServices, fitbitUserProfile, _realTimeProvider, token.Token);
            }

            if (string.Equals(statTypeSlot, Constants.CANONICAL_STAT_STEPS))
            {
                return await Scenarios.ShowStepsSummary(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(statTypeSlot, Constants.CANONICAL_STAT_DISTANCE) ||
                string.Equals(statTypeSlot, Constants.CANONICAL_STAT_MILES) ||
                string.Equals(statTypeSlot, Constants.CANONICAL_STAT_KILOMETERS))
            {
                return await Scenarios.ShowDistanceSummary(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, statTypeSlot, token.Token);
            }
            else if (string.Equals(statTypeSlot, Constants.CANONICAL_STAT_CALORIES))
            {
                if (string.Equals(Constants.CANONICAL_ACTIVITY_LOG, activityTypeSlot))
                {
                    // Calories logged
                    return await Scenarios.ShowCaloriesLoggedSummary(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
                }
                else
                {
                    // Calories burned
                    return await Scenarios.ShowCaloriesBurnedSummary(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
                }
            }
            else if (string.Equals(statTypeSlot, Constants.CANONICAL_STAT_FLOORS))
            {
                return await Scenarios.ShowFloorsSummary(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(statTypeSlot, Constants.CANONICAL_STAT_ACTIVE_MINUTES))
            {
                return await Scenarios.ShowActiveMinutesSummary(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(statTypeSlot, Constants.CANONICAL_STAT_WATER))
            {
                return await Scenarios.ShowWaterLoggedSummary(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            //else if (string.Equals(statTypeSlot, "SLEEP"))
            //{
            //    return await Scenarios.ShowStepsSummary(_service, queryWithContext, pluginServices, timeContext, token.Token);
            //}
            else
            {
                return await Scenarios.ShowOverallSummary(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
        }

        public async Task<PluginResult> ShowMeasurement(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            // Try and extract a time slot and resolve user's local time
            TimeResolutionInfo timeContext = Helpers.ResolveDate(fitbitUserProfile, queryWithContext, _realTimeProvider);

            string measurementTypeSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_MEASUREMENT);

            //if (string.Equals(measurementTypeSlot, "BATTERY"))
            //{
            //    return await Scenarios.ShowStepsSummary(_service, queryWithContext, pluginServices, timeContext, token.Token);
            //}
            //else if (string.Equals(measurementTypeSlot, "HEART_RATE"))
            //{
            //    return await Scenarios.ShowStepsSummary(_service, queryWithContext, pluginServices, timeContext, token.Token);
            //}
            if (string.Equals(measurementTypeSlot, Constants.CANONICAL_MEASUREMENT_WEIGHT))
            {
                return await Scenarios.ShowMostRecentWeight(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(measurementTypeSlot, Constants.CANONICAL_MEASUREMENT_BMI))
            {
                return await Scenarios.ShowMostRecentBMI(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(measurementTypeSlot, Constants.CANONICAL_MEASUREMENT_HEIGHT))
            {
                return await Scenarios.ShowCurrentHeight(queryWithContext, pluginServices, fitbitUserProfile, token.Token);
            }
            else if (string.Equals(measurementTypeSlot, Constants.CANONICAL_MEASUREMENT_AGE))
            {
                return await Scenarios.ShowCurrentAge(queryWithContext, pluginServices, fitbitUserProfile);
            }
            else if (string.Equals(measurementTypeSlot, Constants.CANONICAL_MEASUREMENT_BATTERY))
            {
                return await Scenarios.ShowBatteryLevel(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else
            {
                return new PluginResult(Result.Success)
                {
                    ResponseText = "I don't know what you mean."
                };
            }
        }

        public async Task<PluginResult> ShowGoal(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            TimeResolutionInfo timeContext = Helpers.ResolveDate(fitbitUserProfile, queryWithContext, _realTimeProvider);

            string goalTypeSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_GOAL_TYPE);
            
            if (string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_STEPS))
            {
                return await Scenarios.ShowStepGoal(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_CALORIES))
            {
                return await Scenarios.ShowCalorieGoal(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_FLOORS))
            {
                return await Scenarios.ShowFloorGoal(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_MILES) ||
                string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_KILOMETERS) ||
                string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_DISTANCE))
            {
                return await Scenarios.ShowDistanceGoal(goalTypeSlot, _service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else
            {
                return await Scenarios.ShowAllGoals(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
        }

        public async Task<PluginResult> ShowGoalProgress(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            // Try and extract a time slot and resolve user's local time
            TimeResolutionInfo timeContext = Helpers.ResolveDate(fitbitUserProfile, queryWithContext, _realTimeProvider);

            string goalTypeSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_GOAL_TYPE);

            if (string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_STEPS))
            {
                return await Scenarios.ShowStepGoalProgress(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_CALORIES))
            {
                return await Scenarios.ShowCalorieGoalProgress(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_FLOORS))
            {
                return await Scenarios.ShowFloorGoalProgress(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_MILES) ||
                string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_KILOMETERS) ||
                string.Equals(goalTypeSlot, Constants.CANONICAL_GOAL_DISTANCE))
            {
                return await Scenarios.ShowDistanceGoalProgress(goalTypeSlot, _service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else
            {
                return await Scenarios.ShowAllGoalProgress(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
        }

        public async Task<PluginResult> LogOut(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token != null)
            {
                // Log out
                await pluginServices.DeleteOAuthToken(OAUTH_CONFIG, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                return await pluginServices.LanguageGenerator.GetPattern("LoggedOut", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }
            else
            {
                // Not logged in
                return await pluginServices.LanguageGenerator.GetPattern("NotLoggedIn", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }
        }
        
        //public async Task<PluginResult> SetNewGoal(QueryWithContext queryWithContext, IPluginServices pluginServices)
        //{
        //    OAuthToken token = await pluginServices.TryGetOAuthToken(OAUTH_CONFIG, queryWithContext.ClientContext.UserId);
        //    if (token == null)
        //    {
        //        return BuildLoginPrompt(queryWithContext, pluginServices);
        //    }

        //    FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
        //    if (fitbitUserProfile == null)
        //    {
        //        return new PluginResult(Result.Success)
        //        {
        //            ResponseText = "Sorry, I was unable to retrieve your Fitbit profile"
        //        };
        //    }

        //    string goalTypeSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "goal_type");

        //    // Make sure goal type slot is valid

        //    // See if user specified a new value immediately, otherwise we have to prompt

        //    return new PluginResult(Result.Success)
        //    {
        //        ResponseText = "Okay, you have a new goal of 12000 steps per day."
        //    };
        //}

        public async Task<PluginResult> ShowLeaderboard(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            return await Scenarios.ShowFriendsLeaderboard(_service, queryWithContext, pluginServices, fitbitUserProfile, token.Token);
        }
        
        public async Task<PluginResult> ShowExerciseCount(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            return await Scenarios.ShowExerciseCount(_service, queryWithContext, pluginServices, fitbitUserProfile, _realTimeProvider, token.Token);
        }

        public async Task<PluginResult> LogActivity(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            // Try and extract a time slot and resolve user's local time
            TimeResolutionInfo timeContext = Helpers.ResolveDate(fitbitUserProfile, queryWithContext, _realTimeProvider);
            
            string statTypeSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_STAT_TYPE);
            string foodSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, Constants.SLOT_FOOD);

            if (string.Equals(statTypeSlot, Constants.CANONICAL_STAT_WATER))
            {
                return await Scenarios.LogWater(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token);
            }
            else if (!string.IsNullOrEmpty(foodSlot))
            {
                PluginResult logFoodResult = await Scenarios.LogFood(_service, queryWithContext, pluginServices, fitbitUserProfile, timeContext, token.Token, foodSlot);
                logFoodResult.ResultConversationNode = "LogFoodPromptConfirm";
                return logFoodResult;
            }
            else
            {
                return new PluginResult(Result.Success)
                {
                    ResponseText = "I don't know what you want to log"
                };
            }
        }

        public async Task<PluginResult> LogFoodConfirm(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            return await Scenarios.LogFoodConfirm(_service, queryWithContext, pluginServices, fitbitUserProfile, token.Token);
        }

        public async Task<PluginResult> LogFoodCancel(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            return await Scenarios.LogFoodCancel(queryWithContext, pluginServices);
        }

        public async Task<PluginResult> ShowAlarms(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            return await Scenarios.ShowAlarms(_service, queryWithContext, pluginServices, fitbitUserProfile, token.Token);
        }

        public async Task<PluginResult> SetAlarm(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            if (queryWithContext.RetryCount >= 3)
            {
                return await pluginServices.LanguageGenerator.GetPattern("AlarmSetStartOver", queryWithContext.ClientContext, pluginServices.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            // Fetch token just to ensure that user is logged in before we start
            // FIXME this only needs to happen on first turn, right?
            OAuthToken token = await TryGetOauthToken(queryWithContext, pluginServices);
            if (token == null)
            {
                return await BuildLoginPrompt(queryWithContext, pluginServices);
            }

            FitbitUser fitbitUserProfile = await Scenarios.GetUserProfile(_service, queryWithContext, pluginServices, token.Token);
            if (fitbitUserProfile == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Sorry, I was unable to retrieve your Fitbit profile"
                };
            }

            return await Scenarios.SetAlarm(_service, queryWithContext, pluginServices, fitbitUserProfile, token.Token, _realTimeProvider);
        }
        
        private async Task<PluginResult> BuildLoginPrompt(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            // First, make sure the user uses some kind of authentication
            if (queryWithContext.AuthenticationLevel.HasFlag(ClientAuthenticationLevel.UserAuthorized))
            {
                Uri authUri = await pluginServices.CreateOAuthUri(OAUTH_CONFIG, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                // Does the client support the oauth login action?
                if (queryWithContext.ClientContext.SupportedClientActions.Contains(OAuthLoginAction.ActionName))
                {
                    OAuthLoginAction loginAction = new OAuthLoginAction()
                    {
                        LoginUrl = authUri.AbsoluteUri,
                        ServiceName = "Fitbit"
                    };

                    return new PluginResult(Result.Success)
                    {
                        ResponseText = "Please login with Fitbit.",
                        ResponseSsml = "Please login with Fitbit",
                        ClientAction = JsonConvert.SerializeObject(loginAction)
                    };
                }
                else
                {
                    return new PluginResult(Result.Success)
                    {
                        ResponseSsml = "Please login with Fitbit",
                        ResponseText = "Please authorize at " + authUri.AbsoluteUri
                    };
                }
            }
            else if (queryWithContext.AuthenticationLevel.HasFlag(ClientAuthenticationLevel.UserUnauthorized))
            {
                string message = "Your device is not authorized to use authenticated services. Log out and reauthenticate your client before proceeding.";
                return new PluginResult(Result.Success)
                {
                    ResponseText = message,
                    ResponseSsml = message
                };
            }
            else
            {
                string message = "Before using authenticated services, you must associate your client with a verified user account such as a Microsoft account. You can do this using the account settings on your device.";
                return new PluginResult(Result.Success)
                {
                    ResponseText = message,
                    ResponseSsml = message
                };
            }
        }

        public async Task<PluginResult> ChitChat(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            StringBuilder responsePhrase = new StringBuilder();
            responsePhrase.Append("Welcome to Fitbit. I can tell you about your steps, calories, distance, and more.");
            int rand = _random.NextInt(0, 4);
            if (rand == 0)
                responsePhrase.Append(" Try asking how many steps you have taken.");
            else if (rand == 1)
                responsePhrase.Append(" Try asking how far you walked yesterday.");
            else if (rand == 2)
                responsePhrase.Append(" Try asking about your fitness goals.");
            else
                responsePhrase.Append(" Try asking for your exercise summary.");

            await DurandalTaskExtensions.NoOpTask;

            return new PluginResult(Result.Success)
            {
                ResponseText = responsePhrase.ToString()
            };
        }

        public async Task<PluginResult> ShowHelp(QueryWithContext queryWithContext, IPluginServices pluginServices)
        {
            return await Scenarios.ShowHelp(queryWithContext, pluginServices);
        }
    }
}
