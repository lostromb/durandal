using Durandal.Common.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SystemTrayClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Storyboard storyboard;

        public MainWindow()
        {
            InitializeComponent();

            this.ShowActivated = false;

            System.Drawing.Rectangle workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            int padding = 5;
            this.Width = 300;
            this.Height = 0;
            // Position the window in the notification area of the screen
            this.Left = workingArea.Width - this.Width - padding;
            this.Top = workingArea.Height - 500 - padding;

            DoubleAnimationUsingKeyFrames timeline = new DoubleAnimationUsingKeyFrames();
            IEasingFunction ease = new System.Windows.Media.Animation.PowerEase();
            timeline.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero), ease));
            timeline.KeyFrames.Add(new EasingDoubleKeyFrame(500, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600)), ease));
            
            storyboard = new Storyboard();
            Storyboard.SetTargetName(timeline, this.Name);
            Storyboard.SetTargetProperty(timeline, new PropertyPath(Window.HeightProperty));
            storyboard.Children.Add(timeline);

            this.Loaded += new RoutedEventHandler(Window_Loaded);
        }

        public void Window_Loaded(object sender, RoutedEventArgs args)
        {
            storyboard.Begin(this);
        }
    }
}
