using Durandal.Common.Client;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.Login;
using DurandalWinRT;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace DurandalClientWin10
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AccountsPage : Page
    {
        private CancellationTokenSource _loginCancellizer = new CancellationTokenSource();

        public AccountsPage()
        {
            this.InitializeComponent();
        }

        private DurandalApp MainApp => ((App)App.Current).Durandal;
        private ClientConfiguration MainConfig => MainApp.ClientConfig;

        private async void AddMsaUserButton_Click(object sender, RoutedEventArgs args)
        {
            try
            {
                string state = Guid.NewGuid().ToString("N");
                HttpRequest loginRequestBuilder = HttpRequest.BuildFromUrlString("/common/oauth2/v2.0/authorize");
                loginRequestBuilder.GetParameters["client_id"] = "0359c040-e829-4472-843b-122ec590e75d";
                loginRequestBuilder.GetParameters["response_type"] = "code";
                loginRequestBuilder.GetParameters["redirect_uri"] = "https://durandal-ai.net/auth/login/oauth/msa-portable";
                loginRequestBuilder.GetParameters["response_mode"] = "query";
                loginRequestBuilder.GetParameters["scope"] = "User.Read";
                loginRequestBuilder.GetParameters["prompt"] = "login";
                loginRequestBuilder.GetParameters["state"] = state;
                string loginUrl = "https://login.microsoftonline.com" + loginRequestBuilder.BuildUri();
                Uri loginUri = new Uri(loginUrl);
                bool success = await Windows.System.Launcher.LaunchUriAsync(loginUri);
                ClientCore client = await MainApp.GetClient();
                UserIdentity newIdentity = await client.RegisterNewAuthenticatedUser("msa-portable", state, _loginCancellizer.Token);
                MainApp.SetActiveUserId(newIdentity);
                await UpdateInterface();
            }
            catch (Exception e)
            {
                MainApp.Logger.Log("Error while performing login", LogLevel.Err);
                MainApp.Logger.Log(e, LogLevel.Err);
            }
        }

        private async void LogoutUserButton_Click(object sender, RoutedEventArgs args)
        {
            FrameworkElement sourceButton = sender as FrameworkElement;
            if (sourceButton == null)
            {
                return;
            }

            UserIdentity logoutIdentity = sourceButton.Tag as UserIdentity;
            if (logoutIdentity == null)
            {
                throw new Exception("No identity associated with button");
            }

            ClientCore core = await MainApp.GetClient();
            await core.LogOutUser(logoutIdentity.Id);
            
            if (string.Equals(logoutIdentity.Id, MainApp.ClientConfig.UserId))
            {
                IList<UserIdentity> remainingIdentities = core.GetAvailableUserIdentities();

                // Have we logged out all users? Then we need to force the client into the default identity that is (hopefully) uniquely keyed to this device
                if (remainingIdentities.Count == 0)
                {
                    UserIdentity builtInIdentity = await DurandalApp.GetBuiltInUserIdentity(MainApp.Logger);
                    core.SetActiveUserIdentity(builtInIdentity);
                }
                else
                {
                    // Otherwise pick a first user as the new active user
                    core.SetActiveUserIdentity(remainingIdentities[0]);
                }
            }

            await UpdateInterface();
        }
        
        private async void SelectUserButton_Click(object sender, RoutedEventArgs args)
        {
            FrameworkElement sourceElement = sender as FrameworkElement;
            if (sourceElement == null)
            {
                return;
            }

            UserIdentity logoutIdentity = sourceElement.Tag as UserIdentity;
            if (logoutIdentity == null)
            {
                throw new Exception("No identity associated with panel");
            }

            ClientCore core = await MainApp.GetClient();
            core.SetActiveUserIdentity(logoutIdentity);
            await UpdateInterface();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _loginCancellizer.Cancel();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(MainPage));
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateInterface();
        }

        private async Task UpdateInterface()
        {
            UserIdentitiesPanel.Children.Clear();

            ClientCore core = await MainApp.GetClient();
            IList<UserIdentity> allUsers = core.GetAvailableUserIdentities();
            foreach (UserIdentity ident in allUsers)
            {
                UserIdentitiesPanel.Children.Add(await BuildUserProfileElement(ident));
            }
        }

        private async Task<UIElement> BuildUserProfileElement(UserIdentity profile)
        {
            string activeUserId = MainApp.ClientConfig.UserId;

            Grid outerGrid = new Grid();
            outerGrid.Margin = new Thickness(5);
            if (string.Equals(profile.Id, activeUserId))
            {
                outerGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x24, 0x6D, 0xA0));
            }

            outerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            Image profileImage = new Image();
            profileImage.Margin = new Thickness(5);
            profileImage.SetValue(Grid.ColumnProperty, 0);

            double resolutionScale = Math.Max(0.25, ((double)(int)Windows.Graphics.Display.DisplayInformation.GetForCurrentView().ResolutionScale) / 100);
            int displayWidth = (int)(this.LayoutRoot.ActualWidth / resolutionScale);
            if (displayWidth < 400)
            {
                profileImage.Height = 32;
            }
            else
            {
                profileImage.Height = 64;
            }

            if (profile.IconPng != null && profile.IconPng.Length > 0)
            {
                profileImage.Source = await ImageUtils.ConvertPngBytesToWpfImageSource(profile.IconPng);
            }
            else
            {
                profileImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/default_profile.png"));
            }

            outerGrid.Children.Add(profileImage);

            StackPanel userNameFieldsPanel = new StackPanel();
            userNameFieldsPanel.Orientation = Orientation.Vertical;
            userNameFieldsPanel.SetValue(Grid.ColumnProperty, 1);
            userNameFieldsPanel.Margin = new Thickness(5);
            TextBlock userNameBlock = new TextBlock();
            userNameBlock.Text = profile.FullName ?? "NULL";
            userNameFieldsPanel.Children.Add(userNameBlock);
            TextBlock userEmailBlock = new TextBlock();
            userEmailBlock.Text = profile.Email ?? string.Empty;
            userNameFieldsPanel.Children.Add(userEmailBlock);
            TextBlock userProviderBlock = new TextBlock();
            if (string.Equals(profile.AuthProvider, "adhoc"))
            {
                userProviderBlock.Text = "Local account";
            }
            else if (string.Equals(profile.AuthProvider, "msa-portable"))
            {
                userProviderBlock.Text = "Microsoft account";
            }
            else
            {
                userProviderBlock.Text = "Unknown account";
            }
            userNameFieldsPanel.Children.Add(userProviderBlock);
            outerGrid.Children.Add(userNameFieldsPanel);
            
            Button selectUserButton = new Button();
            selectUserButton.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            selectUserButton.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x87, 0x87, 0x87));
            selectUserButton.Content = "Select";
            selectUserButton.Click += SelectUserButton_Click;
            selectUserButton.Tag = profile;
            selectUserButton.Margin = new Thickness(5);
            selectUserButton.SetValue(Grid.ColumnProperty, 2);
            outerGrid.Children.Add(selectUserButton);

            Button logoutButton = new Button();
            logoutButton.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            logoutButton.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x87, 0x87, 0x87));
            logoutButton.Content = "Logout";
            logoutButton.Click += LogoutUserButton_Click;
            logoutButton.Tag = profile;
            logoutButton.Margin = new Thickness(5);
            logoutButton.SetValue(Grid.ColumnProperty, 3);
            outerGrid.Children.Add(logoutButton);

            return outerGrid;
        }

        private UIElement BuildClientProfileElement(ClientIdentity profile)
        {
            string activeClientId = MainApp.ClientConfig.ClientId;

            Grid outerGrid = new Grid();
            outerGrid.Margin = new Thickness(5);
            if (string.Equals(profile.Id, activeClientId))
            {
                outerGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x24, 0x6D, 0xA0));
            }
            
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel clientNameFieldsPanel = new StackPanel();
            clientNameFieldsPanel.Orientation = Orientation.Vertical;
            clientNameFieldsPanel.SetValue(Grid.ColumnProperty, 0);
            clientNameFieldsPanel.Margin = new Thickness(5);
            TextBlock clientNameBlock = new TextBlock();
            clientNameBlock.Text = profile.Name ?? "NULL";
            clientNameFieldsPanel.Children.Add(clientNameBlock);
            TextBlock clientIdBlock = new TextBlock();
            clientIdBlock.Text = profile.Id ?? string.Empty;
            clientNameFieldsPanel.Children.Add(clientIdBlock);
            TextBlock clientProviderBlock = new TextBlock();
            if (string.Equals(profile.AuthProvider, "adhoc"))
            {
                clientProviderBlock.Text = "Local account";
            }
            else
            {
                clientProviderBlock.Text = "Unknown account";
            }
            clientNameFieldsPanel.Children.Add(clientProviderBlock);
            outerGrid.Children.Add(clientNameFieldsPanel);
           
            Button removeButton = new Button();
            removeButton.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            removeButton.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x87, 0x87, 0x87));
            removeButton.Content = "Remove";
            removeButton.Click += LogoutUserButton_Click;
            removeButton.Tag = profile;
            removeButton.Margin = new Thickness(5);
            removeButton.SetValue(Grid.ColumnProperty, 1);
            outerGrid.Children.Add(removeButton);

            return outerGrid;
        }

        private async void AddAdhocUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdhocUserNameBox == null || string.IsNullOrEmpty(AdhocUserNameBox.Text))
            {
                return;
            }

            string userFullName = AdhocUserNameBox.Text.Trim();
            string userId = Guid.NewGuid().ToString("N");
            UserClientSecretInfo ident = new UserClientSecretInfo()
            {
                UserFullName = userFullName,
                UserId = userId,
                AuthProvider = "adhoc",
            };

            if (userFullName.Contains(' '))
            {
                int idx = userFullName.IndexOf(' ');
                ident.UserGivenName = userFullName.Substring(0, idx).Trim();
                ident.UserSurname = userFullName.Substring(idx + 1).Trim();
            }
            else
            {
                ident.UserGivenName = userFullName;
                ident.UserSurname = string.Empty;
            }

            string state = JsonConvert.SerializeObject(ident);
            
            ClientCore client = await MainApp.GetClient();
            UserIdentity newIdentity = await client.RegisterNewAuthenticatedUser("adhoc", state, _loginCancellizer.Token);
            MainApp.SetActiveUserId(newIdentity);
            await UpdateInterface();
        }
    }
}
