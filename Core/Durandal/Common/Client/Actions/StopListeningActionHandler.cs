using Durandal.API;
using Durandal.Common.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Common.Client.Actions
{
    /// <summary>
    /// Implements the StopListening action
    /// </summary>
    public class StopListeningActionHandler : IJsonClientActionHandler
    {
        private ISet<string> _supportedActions = new HashSet<string>();

        public StopListeningActionHandler()
        {
            _supportedActions.Add(StopListeningAction.ActionName);
        }

        public Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Parse the duration from the action schema
            try
            {
                StopListeningAction parsedAction = action.ToObject<StopListeningAction>();
                queryLogger.Log("Server has requested that we stop listening for " + parsedAction.DurationSeconds + " seconds");
                source.StopListening(TimeSpan.FromSeconds(parsedAction.DurationSeconds), realTime);
            }
            catch (JsonException e)
            {
                queryLogger.Log("Couldn't parse StopListening action", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public ISet<string> GetSupportedClientActions()
        {
            return _supportedActions;
        }
    }
}
