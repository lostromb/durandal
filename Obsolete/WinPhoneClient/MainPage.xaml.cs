using System;
using System.Diagnostics;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The WebView Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641

namespace TestWebViewApp
{
    public sealed partial class MainPage : Page
    {
        private static readonly string Server = "http://www.bungie.com";

        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            WebViewControl.Navigate(new Uri(Server));
        }

        private void Browser_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                Debug.WriteLine("Navigation to this page failed, check your internet connection.");
            }
        }
    }
}
