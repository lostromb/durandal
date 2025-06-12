using Durandal.Common.Logger;
using DurandalWinRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace DurandalClientWin10
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PrivacyPage : Page
    {
        public PrivacyPage()
        {
            this.InitializeComponent();
        }

        private void AgreeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowsLocalConfiguration localConfig = new WindowsLocalConfiguration(new DebugLogger("BootstrapConfig"));
            localConfig.Set("firstLaunch", false);
            ((DurandalClientWin10.App)App.Current).RootFrame.Navigate(typeof(MainPage));
        }

        private void DenyButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Exit();
        }
    }
}
