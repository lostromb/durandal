using Durandal.Common.Client;
using Durandal.Common.Logger;
using DurandalWinRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;
using Windows.UI.ApplicationSettings;
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
    /// The settings page for configuring the application
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private int _buttonPressed = 0;

        public SettingsPage()
        {
            this.InitializeComponent();

            enableTrigger.Checked += TriggerChecked;
            enableTrigger.Unchecked += TriggerUnchecked;
            speechRecoServiceRemoteButton.Checked += RemoteSpeechRecoChecked;
            speechRecoServiceAzureButton.Checked += AzureSpeechRecoChecked;
        }

        private DurandalApp MainApp => ((App)App.Current).Durandal;
        private ClientConfiguration MainConfig => MainApp.ClientConfig;

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref _buttonPressed, 1, 0) == 0)
            {
                this.Frame.Navigate(typeof(MainPage));
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

                if (!string.IsNullOrEmpty(arbitrationEndpointBox.Text) && Uri.IsWellFormedUriString(arbitrationEndpointBox.Text, UriKind.Absolute))
                    MainConfig.TriggerArbitratorUrl = new Uri(arbitrationEndpointBox.Text);
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
                this.Frame.Navigate(typeof(MainPage));
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
            arbitrationEndpointBox.IsEnabled = keywordSpotterEnabled;
            arbitrationGroupBox.IsEnabled = keywordSpotterEnabled;
        }

        private void EnableRemoteSpeechRecoFields(bool remoteSpeechRecoEnabled)
        {
            remoteSpeechRecoEndpointBox.IsEnabled = remoteSpeechRecoEnabled;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            endpointUrlBox.Text = MainConfig.RemoteDialogServerAddress == null ? "https://durandal-ai.net:62292" : MainConfig.RemoteDialogServerAddress.AbsoluteUri;
            authServiceEndpointBox.Text = MainConfig.AuthenticationEndpoint == null ? "https://durandal-ai.net:443" : MainConfig.AuthenticationEndpoint.AbsoluteUri;
            enableTrigger.IsChecked = MainConfig.TriggerEnabled;
            triggerPhraseBox.Text = MainConfig.TriggerPhrase;
            triggerSensitivityBox.Text = MainConfig.PrimaryAudioTriggerSensitivity.ToString();
            secondaryTriggerSensitivityBox.Text = MainConfig.SecondaryAudioTriggerSensitivity.ToString();
            arbitrationEndpointBox.Text = MainConfig.TriggerArbitratorUrl == null ? string.Empty : MainConfig.TriggerArbitratorUrl.AbsoluteUri;
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

        private void Page_Unloaded(object source, RoutedEventArgs e)
        {
        }
    }
}
