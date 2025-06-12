using Durandal.API;
using Durandal.Common.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Client.Actions;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Client
{
    public class JsonClientActionDispatcher : IClientActionDispatcher
    {
        private readonly HashSet<string> _knownActions = new HashSet<string>();
        private IList<IJsonClientActionHandler> _handlers;
        private int _disposed = 0;

        public JsonClientActionDispatcher(IEnumerable<IJsonClientActionHandler> handlers = null)
        {
            if (handlers != null)
            {
                _handlers = new List<IJsonClientActionHandler>(handlers);
            }
            else
            {
                _handlers = new List<IJsonClientActionHandler>();
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~JsonClientActionDispatcher()
        {
            Dispose(false);
        }
#endif

        public void AddHandler(IJsonClientActionHandler handler)
        {
            if (handler != null)
            {
                _handlers.Add(handler);
                _knownActions.UnionWith(handler.GetSupportedClientActions());
            }
        }

        public void RemoveHandler(Type handlerType)
        {
            List<IJsonClientActionHandler> culledList = new List<IJsonClientActionHandler>();
            _knownActions.Clear();
            foreach (var handler in _handlers)
            {
                if (!handler.GetType().Equals(handlerType))
                {
                    culledList.Add(handler);
                    _knownActions.UnionWith(handler.GetSupportedClientActions());
                }
            }

            _handlers = culledList;
        }

        public HashSet<string> GetSupportedClientActions()
        {
            return _knownActions;
        }

        public async Task InterpretClientAction(string actionString, ClientCore source, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                List<JObject> listOfActions = null;
                // See if it's an array
                if (actionString.StartsWith("["))
                {
                    try
                    {
                        listOfActions = JsonConvert.DeserializeObject<List<JObject>>(actionString);
                        foreach (JObject action in listOfActions)
                        {
                            string actionName = action["Name"].Value<string>();
                            if (string.IsNullOrEmpty(actionName))
                            {
                                queryLogger.Log("A client action came back with no \"Name\" field, I'm assuming it's non-standard: " + actionString, LogLevel.Wrn);
                            }
                            else
                            {
                                await HandleSingleAction(actionName, action, queryLogger, source, cancelToken, realTime).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (JsonException e)
                    {
                        queryLogger.Log("A client action appeared to be a json array, but was not parseable as such: " + actionString, LogLevel.Wrn);
                        queryLogger.Log(e, LogLevel.Wrn);
                    }
                }

                if (listOfActions == null)
                {
                    // Otherwise fall back to it being a single action
                    JObject singleAction = JsonConvert.DeserializeObject<JObject>(actionString);
                    string actionName = singleAction["Name"].Value<string>();
                    if (string.IsNullOrEmpty(actionName))
                    {
                        queryLogger.Log("A client action came back with no \"Name\" field, I'm assuming it's non-standard: " + actionString, LogLevel.Wrn);
                    }
                    else
                    {
                        await HandleSingleAction(actionName, singleAction, queryLogger, source, cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                queryLogger.Log("Error while handling client action", LogLevel.Err);
                queryLogger.Log("Action string was " + actionString, LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
            }
        }

        private async Task<bool> HandleSingleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Iterate through our handlers and pick the first one that can handle this action
            foreach (IJsonClientActionHandler handler in _handlers)
            {
                if (handler.GetSupportedClientActions().Contains(actionName))
                {
                    await handler.HandleAction(actionName, action, queryLogger, source, cancelToken, realTime).ConfigureAwait(false);
                    return true;
                }
            }

            queryLogger.Log("Unknown client action encountered: " + actionName, LogLevel.Wrn);

            return false;
        }
    }
}
