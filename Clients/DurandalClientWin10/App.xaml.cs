using Durandal.Common.Logger;
using DurandalWinRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
using Windows.UI.Xaml.Navigation;

namespace DurandalClientWin10
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private DurandalApp _durandalApp;
        private Frame _rootFrame;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += OnResuming;

            _durandalApp = new DurandalApp();
        }

        public DurandalApp Durandal => _durandalApp;
        public Frame RootFrame => _rootFrame;

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
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
            _rootFrame = Window.Current.Content as Frame;

            bool initializationNeeded = false;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
            ApplicationView.GetForCurrentView().FullScreenSystemOverlayMode = FullScreenSystemOverlayMode.Minimal;
            //ApplicationView.GetForCurrentView().TryResizeView(new Size(400, 800));
            ApplicationView.TerminateAppOnFinalViewClose = true;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (_rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                _rootFrame = new Frame();

                _rootFrame.NavigationFailed += OnNavigationFailed;
                
                // Place the frame in the current Window
                Window.Current.Content = _rootFrame;
                
                if (e.PreviousExecutionState != ApplicationExecutionState.Running)
                {
                    initializationNeeded = true;
                }
                else
                {
                    initializationNeeded = false;
                }
            }
            else
            {
                initializationNeeded = false;
            }

            // Disable the status bar and overlays
            ApplicationView.GetForCurrentView().SuppressSystemOverlays = true;
            _rootFrame.Unloaded += ExitAppOnWindowClose; // This doesn't always work and, EVEN WITH TerminateAppOnFinalViewClose, and it's really frustrating

            if (e.PrelaunchActivated == false)
            {
                if (_rootFrame.Content == null)
                {
                    WindowsLocalConfiguration localConfig = new WindowsLocalConfiguration(new DebugLogger("BootstrapConfig"));
                    bool showPrivacyPage = localConfig.GetBool("firstLaunch", true);

                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    if (showPrivacyPage)
                    {
                        _rootFrame.Navigate(typeof(PrivacyPage), e.Arguments);
                    }
                    else
                    {
                        _rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    }
                }

                // Ensure the current window is active
                Window.Current.Activate();
            }

            if (initializationNeeded)
            {
                await _durandalApp.Initialize(_rootFrame.Dispatcher);
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            _durandalApp.SuspendApp();
            deferral.Complete();
        }

        private void OnResuming(object sender, object e)
        {
            _durandalApp.ResumeApp();
        }

        private void ExitAppOnWindowClose(object sender, RoutedEventArgs args)
        {
            this.Exit();
        }
    }
}
