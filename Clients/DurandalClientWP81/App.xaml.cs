using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Utils.MathExt;
using DurandalWinRT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The WebView Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641

namespace DurandalClientWP81
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App : Application
    {
        private TransitionCollection transitions;

        private DurandalApp _durandalApp;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.Application_Suspending;
            this.Resuming += this.Application_Resuming;

            _durandalApp = new DurandalApp();
        }

        public DurandalApp Durandal
        {
            get
            {
                return _durandalApp;
            }
        }

        /// <summary>
        /// Provides easy access to the root frame of the Phone Application.
        /// </summary>
        /// <returns>The root frame of the Phone Application.</returns>
        public static Frame RootFrame { get; private set; }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            RootFrame = Window.Current.Content as Frame;
            bool initializationNeeded = false;

            WindowsLocalConfiguration localConfig = new WindowsLocalConfiguration(new DebugLogger("BootstrapConfig"));
            bool showPrivacyPage = localConfig.GetBool("firstLaunch", true);

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (RootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                RootFrame = new Frame();

                // TODO: change this value to a cache size that is appropriate for your application
                RootFrame.CacheSize = 1;

                // Set the default language
                RootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
                
                // Place the frame in the current Window
                Window.Current.Content = RootFrame;
                
                // TODO Figure out if these need to be tweaked. Can I detect app launch crashes here and try to run in "safe mode"?
                if (e.PreviousExecutionState != ApplicationExecutionState.Running)
                {
                    initializationNeeded = true;
                }
                else
                {
                    initializationNeeded = true;
                }
            }
            else
            {
                initializationNeeded = false;
            }
            
            // Disable the phone status bar and overlays
            var statusBarHideTask = StatusBar.GetForCurrentView().HideAsync();
            ApplicationView.GetForCurrentView().SuppressSystemOverlays = true;

            if (RootFrame.Content == null)
            {
                // Removes the turnstile navigation for startup.
                if (RootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in RootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                RootFrame.ContentTransitions = null;
                RootFrame.Navigated += this.RootFrame_FirstNavigated;

                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                Type startPage = showPrivacyPage ? typeof(PrivacyPage) : typeof(MainPage);

                if (!RootFrame.Navigate(startPage, e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();

            if (initializationNeeded)
            {
                await _durandalApp.Initialize(App.RootFrame.Dispatcher);
            }
        }

        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }

        private void Application_Resuming(object sender, object e)
        {
            _durandalApp.ResumeApp();
        }

        /// <summary>
        /// Invoked when application execution is being suspended. Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        private void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            _durandalApp.SuspendApp();

            deferral.Complete();
        }
    }
}
