using Durandal.Common.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Client.Actions;
using Durandal.Common.Logger;
using Newtonsoft.Json.Linq;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Common.Test
{
    public class FakeClientActionHandler : IJsonClientActionHandler
    {
        private ISet<string> _supportedActions;

        public int TriggerCount { get; set; }

        public FakeClientActionHandler(string action)
        {
            _supportedActions = new HashSet<string>();
            _supportedActions.Add(action);
        }

        public ISet<string> GetSupportedClientActions()
        {
            return _supportedActions;
        }

        public async Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_supportedActions.Contains(actionName))
            {
                TriggerCount++;
            }
            else
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                throw new ArgumentException("The wrong kind of action came to me");
            }
        }
    }
}
