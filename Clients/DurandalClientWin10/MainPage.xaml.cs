using Durandal;
using Durandal.API;
using Durandal.Common.Client;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.CommonViews;
using DurandalWinRT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace DurandalClientWin10
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private delegate void GUITextDelegate(string input);

#if DEBUG
        private static QueryFlags DefaultQueryFlags = QueryFlags.Debug;
#else
        private static QueryFlags DefaultQueryFlags = QueryFlags.None;
#endif

        private InputMethod _lastInputMethod = InputMethod.Unknown;

        private Task _backgroundRefreshTask = null;
        private CancellationTokenSource _backgroundRefreshCancelizer = null;
        private DateTimeOffset _nextRefreshTime = DateTimeOffset.MaxValue;
        private TaskFactory _longRunningTaskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.LongRunning);
        private const int DEFAULT_REFRESH_TIME = 30;

        private DateTimeOffset _lastMicButtonPressTime = default(DateTimeOffset);
        private const int FORCE_AUDIO_BUTTON_HOLD_TIME_MS = 600;

        // used to prevent us from overwriting the text box while the user is editing it
        private bool _textBoxBeingEdited = false;
        private bool _pressedSettingsButton = false; // used to prevent multiple clicks to the settings tab

        public MainPage()
        {
            this.InitializeComponent();
        }

        private DurandalApp MainApp
        {
            get
            {
                return ((App)App.Current).Durandal;
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs args)
        {
            Stopwatch loadTimer = Stopwatch.StartNew();
            await MainApp.WaitForAppInit();
            loadTimer.Stop();
            MainApp.Logger.Log("Time spent waiting on core initialize was " + loadTimer.ElapsedMilliseconds);

            MainApp.Logger.Log("Connecting main page UI to underlying client...");

            // Connect the core to the UI
            ClientCore core = await MainApp.GetClient();
            core.NavigateUrl.Subscribe(OpenUrl);
            core.Success.Subscribe(Success);
            core.Fail.Subscribe(Fail);
            core.Skip.Subscribe(Skip);
            core.ShowErrorOutput.Subscribe(ShowErrorOutput);
            core.ResponseReceived.Subscribe(ResponseReceived);
            core.SpeechPrompt.Subscribe(SpeechPrompt);
            core.SpeechCaptureFinished.Subscribe(SpeechFinished);
            core.SpeechCaptureIntermediate.Subscribe(SpeechIntermediate);
            core.UpdateQuery.Subscribe(UpdateQuery);
            //core.RetryEvent += Retry;
            core.Linger.Subscribe(Linger);
            core.SpeechRecoError.Subscribe(SpeechRecoError);

            _backgroundRefreshCancelizer = new CancellationTokenSource();
            _backgroundRefreshTask = _longRunningTaskFactory.StartNew(async () =>
            {
                using (ManualResetEventSlim blocker = new ManualResetEventSlim(false))
                {
                    while (!_backgroundRefreshCancelizer.Token.IsCancellationRequested)
                    {
                        if (DateTimeOffset.UtcNow > _nextRefreshTime)
                        {
                            _nextRefreshTime = DateTimeOffset.MaxValue;
                            ClientCore refreshClient = await MainApp.GetClient();
                            await refreshClient.Greet(MainApp.BuildClientContext(), DefaultQueryFlags);
                            await UpdateTextBox(string.Empty);
                            await UpdatePlaceholderText("Ask me anything");
                        }

                        blocker.Wait(TimeSpan.FromMilliseconds(1000), _backgroundRefreshCancelizer.Token);
                    }

                    blocker.Dispose();
                }
            });

            MainApp.Logger.Log("UI is now hooked up to app. Starting greet...");

            ClientCore client = await MainApp.GetClient();
            await client.Greet(MainApp.BuildClientContext(), DefaultQueryFlags);
        }

        private async void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_backgroundRefreshCancelizer != null)
            {
                _backgroundRefreshCancelizer.Cancel();
            }

            if (MainApp != null)
            {
                ClientCore core = await MainApp.GetClient();

                if (core != null)
                {
                    MainApp.Logger.Log("Disconnecting main page from underlying core...");
                    core.NavigateUrl.TryUnsubscribe(OpenUrl);
                    core.Success.TryUnsubscribe(Success);
                    core.Fail.TryUnsubscribe(Fail);
                    core.Skip.TryUnsubscribe(Skip);
                    core.ShowErrorOutput.TryUnsubscribe(ShowErrorOutput);
                    core.ResponseReceived.TryUnsubscribe(ResponseReceived);
                    core.SpeechPrompt.TryUnsubscribe(SpeechPrompt);
                    core.SpeechCaptureFinished.TryUnsubscribe(SpeechFinished);
                    core.SpeechCaptureIntermediate.TryUnsubscribe(SpeechIntermediate);
                    core.UpdateQuery.TryUnsubscribe(UpdateQuery);
                    //core.RetryEvent -= Retry;
                    core.Linger.TryUnsubscribe(Linger);
                    core.SpeechRecoError.TryUnsubscribe(SpeechRecoError);
                }
            }
        }

        public async Task Success(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            // TODO On success, we will get the Success event, and then the server may have returned audio.
            // If we PlaySound with async = true, it will queue up the two sounds so they don't both play at once.
            // However, that's technically a race condition, so it may need a better design.
            if (_lastInputMethod == InputMethod.Spoken)
            {
                MainApp.PlaySuccessSound();
            }

            _lastInputMethod = InputMethod.Unknown;

            // Set the refresh timeout to the default value.
            // After the client core sends Success() it should follow up with a Linger() event that will convey the actual amount of time the client is expected to keep the page open
            _nextRefreshTime = DateTimeOffset.UtcNow.AddSeconds(DEFAULT_REFRESH_TIME);
        }

        public async Task Fail(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            if (_lastInputMethod == InputMethod.Spoken)
            {
                MainApp.PlayFailSound();
            }

            _lastInputMethod = InputMethod.Unknown;
            await UpdatePlaceholderText("Ask me anything");
        }

        public async Task Skip(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            if (_lastInputMethod == InputMethod.Spoken)
            {
                MainApp.PlayFailSound();
            }

            _lastInputMethod = InputMethod.Unknown;
            await UpdatePlaceholderText("Ask me anything");
        }

        public async Task ResponseReceived(object sender, EventArgs args, IRealTimeProvider realTime)
        {
        }

        public async Task ShowErrorOutput(object sender, TextEventArgs args, IRealTimeProvider realTime)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() => ShowErrorMessageInternal(args.Text)));
        }

        public async Task OpenUrl(object sender, UriEventArgs args, IRealTimeProvider realTime)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() => OpenUrlInternal(args.Url)));
        }

        private void OpenUrlInternal(Uri url)
        {
            string currentBrowserUri = null;
            if (WebViewControl.Source != null)
            {
                currentBrowserUri = WebViewControl.Source.AbsoluteUri;
            }

            if (!string.Equals(currentBrowserUri, url))
            {
                //Debug.WriteLine("Navigating to " + url);
                WebViewControl.Navigate(url);
            }
        }

        private void ShowErrorMessageInternal(string error)
        {
            ErrorPage view = new ErrorPage()
            {
                ErrorDetails = error,
                ClientContextData = null // todo: this should specify theme data defined by the client
            };

            WebViewControl.NavigateToString(view.Render());
        }

        public async Task UpdateTextBox(string text)
        {
            if (!_textBoxBeingEdited)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() => UpdateTextBoxInternal(text)));
            }
        }

        private void UpdateTextBoxInternal(string text)
        {
            InputTextBox.Text = text;
        }

        public async Task UpdatePlaceholderText(string text)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() => UpdatePlaceholderTextInternal(text)));
        }

        private void UpdatePlaceholderTextInternal(string text)
        {
            InputTextBox.PlaceholderText = text;
        }

        public async Task SendTextToBackground(string text)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() => SendTextToBackgroundInternal(text)));
        }

        private void SendTextToBackgroundInternal(string text)
        {
            InputTextBox.Text = string.Empty;
            InputTextBox.PlaceholderText = text;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //HardwareButtons.BackPressed += this.MainPage_BackPressed;
        }

        /// <summary>
        /// Invoked when this page is being navigated away.
        /// </summary>
        /// <param name="e">Event data that describes how this page is navigating.</param>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            //HardwareButtons.BackPressed -= this.MainPage_BackPressed;
        }

        /// <summary>
        /// Overrides the back button press to navigate in the WebView's back stack instead of the application's.
        /// </summary>
        //private void MainPage_BackPressed(object sender, BackPressedEventArgs e)
        //{
        //    if (WebViewControl.CanGoBack)
        //    {
        //        WebViewControl.GoBack();
        //        e.Handled = true;
        //    }
        //}

        private void Browser_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                MainApp.Logger.Log("Navigation to web page failed: " + args.Uri.AbsoluteUri + " Error: " + args.WebErrorStatus.ToString());
            }
        }

        /// <summary>
        /// Navigates to the initial home page.
        /// </summary>
        private async void HomeAppBarButton_Click(object sender, RoutedEventArgs args)
        {
            ClientCore client = await MainApp.GetClient();
            await client.Greet(MainApp.BuildClientContext(), DefaultQueryFlags);
        }

        private void SettingsAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_pressedSettingsButton)
            {
                _pressedSettingsButton = true;
                this.Frame.Navigate(typeof(SettingsPage));
            }
        }

        private void DebugAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_pressedSettingsButton)
            {
                _pressedSettingsButton = true;
                this.Frame.Navigate(typeof(DebugPage));
            }
        }

        private void AccountsAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_pressedSettingsButton)
            {
                _pressedSettingsButton = true;
                this.Frame.Navigate(typeof(AccountsPage));
            }
        }

        public async Task SpeechPrompt(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            _lastInputMethod = InputMethod.Spoken;
            MainApp.Logger.Log("Speech Prompt Triggered...");
            MainApp.PlayPromptSound();
            await UpdatePlaceholderText("Listening...");
        }

        public async Task SpeechIntermediate(object sender, TextEventArgs args, IRealTimeProvider realTime)
        {
            if (!string.IsNullOrEmpty(args.Text))
            {
                await UpdateTextBox(args.Text);
            }
        }

        public async Task SpeechFinished(object sender, SpeechCaptureEventArgs args, IRealTimeProvider realTime)
        {
        }

        public async Task UpdateQuery(object sender, TextEventArgs args, IRealTimeProvider realTime)
        {
            await SendTextToBackground(args.Text);
        }

        private void MicButton_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            MicButtonReleased((Rectangle)sender);
            e.Handled = true;
        }

        private void MicButton_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
        {
            MicButtonPressed((Rectangle)sender);
            e.Handled = true;
        }

        private void MicButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MicButtonReleased((Rectangle)sender);
            e.Handled = true;
        }

        private async void MicButtonPressed(Rectangle button)
        {
            button.Fill = this.Resources["MicBrushPressed"] as ImageBrush;
            ClientCore client = await MainApp.GetClient();

            MainApp.SilenceAudio();
            bool requestHonored = await client.TryMakeAudioRequest(MainApp.BuildClientContext(), DefaultQueryFlags);
            if (!requestHonored)
            {
                MainApp.Logger.Log("Couldn't honor microphone request", LogLevel.Wrn);
            }
            else
            {
                _lastMicButtonPressTime = HighPrecisionTimer.GetCurrentUTCTime();
            }
        }

        private async void MicButtonReleased(Rectangle button)
        {
            button.Fill = this.Resources["MicBrushNormal"] as ImageBrush;

            // Force early audio termination if possible
            if (_lastMicButtonPressTime != default(DateTime) &&
                (int)((HighPrecisionTimer.GetCurrentUTCTime() - _lastMicButtonPressTime).TotalMilliseconds) > FORCE_AUDIO_BUTTON_HOLD_TIME_MS)
            {
                ClientCore client = await MainApp.GetClient();
                await client.ForceRecordingFinish(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                MainApp.Logger.Log("Forcing recording to finish");
            }

            _lastMicButtonPressTime = default(DateTime);
        }

        //fixme should use TextChanged
        private async void InputTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key.Equals(Windows.System.VirtualKey.Enter))
            {
                _lastInputMethod = InputMethod.Typed;
                WebViewControl.Focus(FocusState.Programmatic);
                string inputText = InputTextBox.Text;
                ClientCore client = await MainApp.GetClient();
                ClientContext context = MainApp.BuildClientContext();
                context.RemoveCapabilities(ClientCapabilities.HasSpeakers | ClientCapabilities.HasMicrophone);
                bool requestHonored = await client.TryMakeTextRequest(inputText, context, DefaultQueryFlags);

                if (!requestHonored)
                {
                    MainApp.Logger.Log("Couldn't honor text request", LogLevel.Wrn);
                }
            }
        }

        public async Task Linger(object sender, TimeSpanEventArgs args, IRealTimeProvider realTime)
        {
            _nextRefreshTime = HighPrecisionTimer.GetCurrentUTCTime() + args.Time;
            MainApp.Logger.Log("Will linger on this page until " + _nextRefreshTime.ToString("yyyy-MM-dd HH:mm:ss") + " (" + args.Time.TotalSeconds + " seconds from now)", LogLevel.Vrb);
        }

        public async Task SpeechRecoError(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            if (_lastInputMethod == InputMethod.Spoken)
            {
                MainApp.PlayFailSound();
            }

            _lastInputMethod = InputMethod.Unknown;
            await UpdatePlaceholderText("A listening error occurred");
        }

        /// <summary>
        /// These methods are kind of a hackish solution to implement proper "dark" styles and have them
        /// behave properly when text is being entered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            //InputTextBox.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            _textBoxBeingEdited = true;
        }

        private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            //InputTextBox.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            _textBoxBeingEdited = false;
        }
    }
}
