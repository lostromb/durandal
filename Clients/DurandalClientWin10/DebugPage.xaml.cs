using Durandal.Common.Client;
using Durandal.Common.Logger;
using DurandalWinRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    /// Page used for debug output, client logs, etc
    /// </summary>
    public sealed partial class DebugPage : Page
    {
        private const int MAX_LOG_LENGTH_CHARS = 20000;
        private int _buttonPressed = 0;

        public DebugPage()
        {
            this.InitializeComponent();
        }

        private DurandalApp MainApp => ((App)App.Current).Durandal;

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref _buttonPressed, 1, 0) == 0)
            {
                this.Frame.Navigate(typeof(MainPage));
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ILoggingHistory logHistory = MainApp.Logger.History;
            if (logHistory == null)
            {
                logsBox.Text = "No client logs are available";
            }
            else
            {
                StringBuilder logTextBuilder = new StringBuilder(MAX_LOG_LENGTH_CHARS + 1000);
                foreach (LogEvent log in logHistory.FilterByCriteria(LogLevel.All, true))
                {
                    logTextBuilder.AppendLine(log.ToShortStringHighPrecisionTime());
                    if (logTextBuilder.Length > MAX_LOG_LENGTH_CHARS)
                    {
                        break;
                    }
                }

                logsBox.Text = logTextBuilder.ToString();
            }
        }
    }
}
