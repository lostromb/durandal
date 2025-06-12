using System;
using System.Windows;
using Durandal.Common.Logger;
using Durandal.API;

namespace Durandal
{
    using Durandal.Common.Audio;
    using Durandal.Common.Dialog;
    using Durandal.Common.Speech;
    using System.Diagnostics;
    using Durandal.Common.Dialog.Web;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ThreadedDialogWebService _core;
        private string _statusString = "NOT STARTED";
        private int _loadedAnswerCount = 0;
        
        public MainWindow()
        {
            InitializeComponent();
            string debugString = string.Empty;
#if DEBUG
            debugString = " (DEBUG)";
#endif
            this.Title = string.Format("Durandal DE {0}{1}", SVNVersionInfo.VersionString, debugString);
            label1.Content = string.Format("Durandal Dialog Engine {0}", SVNVersionInfo.VersionString);
            _core = ((App)App.Current).GetDialogEngine();
            _core.AnswerRegistered += AnswerRegisteredEvent;
            _core.EngineInitializing += EngineInitializingEvent;
            _core.EngineInitialized += EngineInitializedEvent;
            _core.Run();
        }

        private void AnswerRegisteredEvent(object source, PluginRegisteredEventArgs args)
        {
            _loadedAnswerCount = args.LoadedPluginCount;
            Dispatcher.Invoke(new CommonDelegates.VoidDelegate(UpdateStatusBox));
        }

        private void EngineInitializingEvent(object source, EventArgs args)
        {
            _statusString = "STARTING";
            Dispatcher.Invoke(new CommonDelegates.VoidDelegate(UpdateStatusBox));
        }

        private void EngineInitializedEvent(object source, EventArgs args)
        {
            _statusString = "RUNNING";
            Dispatcher.Invoke(new CommonDelegates.VoidDelegate(UpdateStatusBox));
        }

        private void UpdateStatusBox()
        {
            label2.Content = string.Format("Status: {0} ({1} answers loaded)", _statusString, _loadedAnswerCount);
        }
    }
}
