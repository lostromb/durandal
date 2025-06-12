using Durandal.API;
using Durandal.Common.Logger;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Client.Actions
{
    /// <summary>
    /// Implements the ExecuteDelayedAction action.
    /// </summary>
    public class ExecuteDelayedActionHandler : IJsonClientActionHandler, IDisposable
    {
        private readonly ISet<string> _supportedActions = new HashSet<string>();

        private CancellationTokenSource _threadCancelizer = null;
        private Task _backgroundTask = null;
        private string _currentActionId = null; // This framework only assumes there can be one delayed action running at a time, otherwise it doesn't make sense.
        private AsyncLockSlim _lock = new AsyncLockSlim();
        private int _disposed = 0;

        public ExecuteDelayedActionHandler()
        {
            ExecuteActionEvent = new AsyncEvent<DialogActionEventArgs>();
            _supportedActions.Add(ExecuteDelayedAction.ActionName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ExecuteDelayedActionHandler()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Fired when a dialog action should be executed. The text arg contains the action ID
        /// </summary>
        public AsyncEvent<DialogActionEventArgs> ExecuteActionEvent { get; private set; }

        public async Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await _lock.GetLockAsync().ConfigureAwait(false);
            try
            {
                try
                {
                    ExecuteDelayedAction parsedAction = action.ToObject<ExecuteDelayedAction>();

                    InputMethod interactionMethod;
                    if (!Enum.TryParse(parsedAction.InteractionMethod, out interactionMethod))
                    {
                        queryLogger.Log(string.Format("Could not execute dialog action {0}. Interaction method {1} must be a valid string value of one of the InputMethod enumerations (Programmatic, Spoken, Typed)", parsedAction.ActionId, parsedAction.InteractionMethod), LogLevel.Err);
                        return;
                    }

                    if (string.IsNullOrEmpty(parsedAction.ActionId))
                    {
                        queryLogger.Log("Could not execute dialog action. Action ID is null or empty", LogLevel.Err);
                        return;
                    }

                    if (parsedAction.DelaySeconds < 1)
                    {
                        queryLogger.Log(string.Format("Could not execute dialog action {0}. The delay is less than 1 second", parsedAction.ActionId), LogLevel.Err);
                        return;
                    }

                    queryLogger.Log("I will be executing dialog action " + parsedAction.ActionId + " in " + parsedAction.DelaySeconds + " seconds", LogLevel.Std);
                    source?.OnLinger(TimeSpan.FromSeconds(parsedAction.DelaySeconds + 10), realTime, queryLogger);

                    // Cancel any delayed action that may be pending
                    _threadCancelizer?.Cancel();
                    _threadCancelizer?.Dispose();

                    _currentActionId = parsedAction.ActionId;
                    _threadCancelizer = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
                    TimeDelayTaskRunner runner = new TimeDelayTaskRunner(
                        TimeSpan.FromSeconds(parsedAction.DelaySeconds),
                        new DialogActionEventArgs(parsedAction.ActionId, interactionMethod),
                        DelayCallback,
                        queryLogger,
                        _threadCancelizer.Token,
                        realTime.Fork("DelayedClientActionThread"));
                    _backgroundTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(runner.Run);
                }
                catch (JsonException e)
                {
                    queryLogger.Log("Failed to parse ExecuteDelayedAction object: " + action.ToString(), LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public ISet<string> GetSupportedClientActions()
        {
            return _supportedActions;
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
                _threadCancelizer?.Cancel();
                _threadCancelizer?.Dispose();
                _lock?.Dispose();
            }
        }

        /// <summary>
        /// Function that is called after the delay has elapsed.
        /// </summary>
        /// <param name="triggeredAction"></param>
        /// <param name="logger"></param>
        /// <param name="realTime"></param>
        private async Task DelayCallback(DialogActionEventArgs triggeredAction, ILogger logger, IRealTimeProvider realTime)
        {
            await _lock.GetLockAsync().ConfigureAwait(false);
            try
            {
                if (_currentActionId != null && string.Equals(_currentActionId, triggeredAction.ActionId))
                {
                    ExecuteActionEvent.FireInBackground(this, triggeredAction, logger, realTime);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Reset()
        {
            _lock.GetLock();
            try
            {
                _currentActionId = null;
            }
            finally
            {
                _lock.Release();
            }
        }

        private class TimeDelayTaskRunner
        {
            private readonly TimeSpan _timeToWait;
            private readonly DialogActionEventArgs _action;
            private readonly Func<DialogActionEventArgs, ILogger, IRealTimeProvider, Task> _callback;
            private readonly CancellationToken _cancelToken;
            private readonly IRealTimeProvider _realTime;
            private readonly ILogger _logger;

            public TimeDelayTaskRunner(
                TimeSpan timeToWait,
                DialogActionEventArgs actionId,
                Func<DialogActionEventArgs, ILogger, IRealTimeProvider, Task> callback,
                ILogger logger,
                CancellationToken cancelToken,
                IRealTimeProvider realTime)
            {
                _timeToWait = timeToWait;
                _action = actionId;
                _callback = callback;
                _cancelToken = cancelToken;
                _realTime = realTime;
                _logger = logger;
            }

            public async Task Run()
            {
                try
                {
                    await _realTime.WaitAsync(_timeToWait, _cancelToken).ConfigureAwait(false);
                    await _callback(_action, _logger, _realTime).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { } // Normal if the delayed action is cancelled
                finally
                {
                    _realTime.Merge();
                }
            }
        }
    }
}
