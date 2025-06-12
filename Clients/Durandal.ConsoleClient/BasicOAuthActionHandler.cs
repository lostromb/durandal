using Durandal;
using Durandal.Common.Client;
using Durandal.Common.Client.Actions;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.ConsoleClient
{
    public class BasicOAuthActionHandler : IJsonClientActionHandler
    {
        private ISet<string> _supportedActions = new HashSet<string>();

        public BasicOAuthActionHandler()
        {
            _supportedActions.Add(OAuthLoginAction.ActionName);
        }

        public async Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Parse the duration from the action schema
            try
            {
                OAuthLoginAction parsedAction = action.ToObject<OAuthLoginAction>();
                queryLogger.Log("Got an OAuth card with URL: " + parsedAction.LoginUrl);

                // Invoke the client's local browser using shell execute to open the auth URL
                Process.Start(parsedAction.LoginUrl);
            }
            catch (JsonException e)
            {
                queryLogger.Log("Couldn't parse OAuthLoginAction action", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                await DurandalTaskExtensions.NoOpTask;
            }
        }

        public ISet<string> GetSupportedClientActions()
        {
            return _supportedActions;
        }
    }
}
