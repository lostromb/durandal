using Durandal.API;
using Durandal.Common.Logger;
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
    /// Implements the SendAudioNextTurn action
    /// </summary>
    public class SendNextTurnAudioActionHandler : IJsonClientActionHandler
    {
        private ISet<string> _supportedActions = new HashSet<string>();

        public SendNextTurnAudioActionHandler()
        {
            _supportedActions.Add(SendNextTurnAudioAction.ActionName);
        }

        public Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            source.SendAudioNextTurn();
            queryLogger.Log("Server has requested that we send audio on the next turn");
            return DurandalTaskExtensions.NoOpTask;
        }

        public ISet<string> GetSupportedClientActions()
        {
            return _supportedActions;
        }
    }
}
