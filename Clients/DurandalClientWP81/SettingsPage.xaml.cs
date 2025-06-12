using Durandal.Common.Client;
using DurandalClientWP81.Common;
using DurandalWinRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace DurandalClientWP81
{
    /// <summary>
    /// The settings page for the application
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();
        private int _buttonPressed = 0;

        public SettingsPage()
        {
            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

            enableTrigger.Checked += TriggerChecked;
            enableTrigger.Unchecked += TriggerUnchecked;

            speechRecoServiceRemoteButton.Checked += RemoteSpeechRecoChecked;
            speechRecoServiceAzureButton.Checked += AzureSpeechRecoChecked;
        }

        /// <summary>
        /// Gets the <see cref="NavigationHelper"/> associated with this <see cref="Page"/>.
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        /// <summary>
        /// Gets the view model for this <see cref="Page"/>.
        /// This can be changed to a strongly typed view model.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session.  The state will be null the first time a page is visited.</param>
        private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="sender">The source of the event; typically <see cref="NavigationHelper"/></param>
        /// <param name="e">Event data that provides an empty dictionary to be populated with
        /// serializable state.</param>
        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
        }

        #region NavigationHelper registration

        /// <summary>
        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// <para>
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="NavigationHelper.LoadState"/>
        /// and <see cref="NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.
        /// </para>
        /// </summary>
        /// <param name="e">Provides data for navigation methods and event
        /// handlers that cannot cancel the navigation request.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        #endregion
        
        private DurandalApp MainApp
        {
            get
            {
                return ((App)App.Current).Durandal;
            }
        }

        private ClientConfiguration MainConfig
        {
            get
            {
                return MainApp.ClientConfig;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref _buttonPressed, 1, 0) == 0)
            {
                App.RootFrame.Navigate(typeof(MainPage));
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Only allow save to be pressed once, otherwise it crashes the program
            if (Interlocked.CompareExchange(ref _buttonPressed, 1, 0) == 0)
            {
                // Validate inputs and ignore ones that can't parse
                if (!string.IsNullOrEmpty(endpointUrlBox.Text) && Uri.IsWellFormedUriString(endpointUrlBox.Text, UriKind.Absolute))
                    MainConfig.RemoteDialogServerAddress = new Uri(endpointUrlBox.Text);
                else
                    MainConfig.RemoteDialogServerAddress = new Uri("https://durandal-ai.net:62292");

                if (!string.IsNullOrEmpty(authServiceEndpointBox.Text) && Uri.IsWellFormedUriString(authServiceEndpointBox.Text, UriKind.Absolute))
                    MainConfig.AuthenticationEndpoint = new Uri(authServiceEndpointBox.Text);
                else
                    MainConfig.AuthenticationEndpoint = new Uri("https://durandal-ai.net:443");

                if (enableTrigger.IsChecked.HasValue)
                    MainConfig.TriggerEnabled = enableTrigger.IsChecked.Value;
                if (!string.IsNullOrEmpty(triggerPhraseBox.Text))
                    MainConfig.TriggerPhrase = triggerPhraseBox.Text;

                double parsedNumber;
                if (!string.IsNullOrEmpty(triggerSensitivityBox.Text) && double.TryParse(triggerSensitivityBox.Text, out parsedNumber))
                    MainConfig.PrimaryAudioTriggerSensitivity = parsedNumber;
                if (!string.IsNullOrEmpty(secondaryTriggerSensitivityBox.Text) && double.TryParse(secondaryTriggerSensitivityBox.Text, out parsedNumber))
                    MainConfig.SecondaryAudioTriggerSensitivity = parsedNumber;

                if (!string.IsNullOrEmpty(arbitrationUrlBox.Text) && Uri.IsWellFormedUriString(arbitrationUrlBox.Text, UriKind.Absolute))
                    MainConfig.TriggerArbitratorUrl = new Uri(arbitrationUrlBox.Text);
                else
                    MainConfig.TriggerArbitratorUrl = null;

                if (!string.IsNullOrEmpty(arbitrationGroupBox.Text))
                    MainConfig.TriggerArbitratorGroupName = arbitrationGroupBox.Text;

                if (speechRecoServiceAzureButton.IsChecked.GetValueOrDefault(false))
                {
                    MainConfig.SRProvider = "azure";
                }
                else
                {
                    MainConfig.SRProvider = "remote";
                }

                if (!string.IsNullOrEmpty(remoteSpeechRecoEndpointBox.Text) && Uri.IsWellFormedUriString(remoteSpeechRecoEndpointBox.Text, UriKind.Absolute))
                    MainConfig.RemoteSpeechRecoAddress = new Uri(remoteSpeechRecoEndpointBox.Text);
                else
                    MainConfig.RemoteSpeechRecoAddress = null;

                await MainApp.Reinitialize();
                App.RootFrame.Navigate(typeof(MainPage));
            }
        }

        private void TriggerChecked(object source, RoutedEventArgs args)
        {
            EnableKeywordSpotterFields(true);
        }

        private void TriggerUnchecked(object source, RoutedEventArgs args)
        {
            EnableKeywordSpotterFields(false);
        }

        private void RemoteSpeechRecoChecked(object source, RoutedEventArgs args)
        {
            EnableRemoteSpeechRecoFields(true);
        }

        private void AzureSpeechRecoChecked(object source, RoutedEventArgs args)
        {
            EnableRemoteSpeechRecoFields(false);
        }

        private void EnableKeywordSpotterFields(bool keywordSpotterEnabled)
        {
            triggerPhraseBox.IsEnabled = keywordSpotterEnabled;
            triggerSensitivityBox.IsEnabled = keywordSpotterEnabled;
            secondaryTriggerSensitivityBox.IsEnabled = keywordSpotterEnabled;
            arbitrationUrlBox.IsEnabled = keywordSpotterEnabled;
            arbitrationGroupBox.IsEnabled = keywordSpotterEnabled;
        }

        private void EnableRemoteSpeechRecoFields(bool remoteSpeechRecoEnabled)
        {
            remoteSpeechRecoEndpointBox.IsEnabled = remoteSpeechRecoEnabled;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _buttonPressed = 0;
            endpointUrlBox.Text = MainConfig.RemoteDialogServerAddress == null ? string.Empty : MainConfig.RemoteDialogServerAddress.AbsoluteUri;
            authServiceEndpointBox.Text = MainConfig.AuthenticationEndpoint == null ? string.Empty : MainConfig.AuthenticationEndpoint.AbsoluteUri;
            enableTrigger.IsChecked = MainConfig.TriggerEnabled;
            triggerPhraseBox.Text = MainConfig.TriggerPhrase;
            triggerSensitivityBox.Text = MainConfig.PrimaryAudioTriggerSensitivity.ToString();
            secondaryTriggerSensitivityBox.Text = MainConfig.SecondaryAudioTriggerSensitivity.ToString();
            arbitrationUrlBox.Text =  MainConfig.TriggerArbitratorUrl == null ? string.Empty : MainConfig.TriggerArbitratorUrl.AbsoluteUri;
            arbitrationGroupBox.Text = MainConfig.TriggerArbitratorGroupName;

            if (string.Equals("azure", MainConfig.SRProvider) || string.Equals("cortana", MainConfig.SRProvider))
            {
                speechRecoServiceAzureButton.IsChecked = true;
            }
            else
            {
                speechRecoServiceRemoteButton.IsChecked = true;
            }

            remoteSpeechRecoEndpointBox.Text = MainConfig.RemoteSpeechRecoAddress == null ? string.Empty : MainConfig.RemoteSpeechRecoAddress.AbsoluteUri;

            EnableKeywordSpotterFields(MainConfig.TriggerEnabled);
            EnableRemoteSpeechRecoFields(string.Equals("remote", MainConfig.SRProvider, StringComparison.OrdinalIgnoreCase));
        }
    }
}
