namespace Durandal.Common.Dialog.Web
{
    using Durandal.API;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.Net;
    using Durandal.Common.NLP;
    using Durandal.Common.Utils;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Dialog web service which adds an asynchronous layer so that the main processing happens on a separate thread and fires events when dialog things happen.
    /// This is generally desired for cases in which the dialog engine is embedded into an interactive program such as a desktop app where it needs to seem responsive
    /// </summary>
    public class ThreadedDialogWebService : IDisposable
    {
        private ILogger _coreLogger;
        private DialogWebService _dialogService;
        private DialogProcessingEngine _dialogCore;
        private readonly DialogEngineParameters _dialogParameters;
        private readonly DialogWebParameters _webServiceParameters;
        private Task _loadTask;
        private int _disposed = 0;

        public ThreadedDialogWebService(ILogger logger, DialogEngineParameters dialogParameters, DialogWebParameters dialogWebParams)
        {
            _coreLogger = logger;
            _dialogParameters = dialogParameters;
            _webServiceParameters = dialogWebParams;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ThreadedDialogWebService()
        {
            Dispose(false);
        }
#endif

        public void Run()
        {
            _loadTask = Task.Run(() => InitializeEngineInternal());
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
                _dialogService?.Dispose();
                _dialogCore?.Dispose();
            }
        }

        public bool IsStopped
        {
            get
            {
                if (_dialogService != null)
                {
                    return _dialogService.IsStopped;
                }

                return true;
            }
        }

        private async Task InitializeEngineInternal()
        {
            this.OnEngineInitializing();
            _coreLogger.Log(string.Format("Durandal Dialog Engine {0} built on {1}",
                SVNVersionInfo.VersionString,
                SVNVersionInfo.BuildDate));

            _dialogParameters.Logger = _coreLogger.Clone("DialogEngine");
            _dialogCore = new DialogProcessingEngine(_dialogParameters);
            _webServiceParameters.Logger = _coreLogger.Clone("DialogWebService");
            _webServiceParameters.CoreEngine = new WeakPointer<DialogProcessingEngine>(_dialogCore);

            // Initialize the engine
            _dialogService = await DialogWebService.Create(_webServiceParameters, CancellationToken.None).ConfigureAwait(false);

            // Hook up events from the dialog engine
            _dialogService.PluginRegistered += AnswerRegistered;
            _dialogService.EngineStopped += EngineStopped;
            
            ISet<string> domains = new HashSet<string>();
            foreach (string enabledDomain in _webServiceParameters.ServerConfig.PluginIdsToLoad)
            {
                domains.Add(enabledDomain);
            }

            // And load the answers
            await _dialogCore.LoadPlugins(domains, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

            _coreLogger.Log("The dialog server is loaded and ready to process queries");
            _coreLogger.Log("Web view is accessible at " + string.Join("|", _webServiceParameters.ServerConfig.DialogServerEndpoints));
            this.OnEngineInitialized();
        }

        public DialogWebConfiguration GetConfiguration()
        {
            return _webServiceParameters.ServerConfig;
        }

#region Events

        // Passthrough from DialogWebService
        public event EventHandler<PluginRegisteredEventArgs> AnswerRegistered;
        public event EventHandler EngineStopped;

        // Generated from this class
        public event EventHandler EngineInitializing;
        public event EventHandler EngineInitialized;

        private void OnEngineInitializing()
        {
            if (EngineInitializing != null)
            {
                EngineInitializing(this, null);
            }
        }

        private void OnEngineInitialized()
        {
            if (EngineInitialized != null)
            {
                EngineInitialized(this, null);
            }
        }

#endregion
    }
}
