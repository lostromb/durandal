using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace SystemTrayClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private ClientInterface client;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            client = new ClientInterface();
            Task backgroundInitializeTask = client.Initialize();

            // Create a simple tray menu with only one item.
            trayMenu = new ContextMenu();
            //trayMenu.MenuItems.Add("Stop Listening")
            trayMenu.MenuItems.Add("Exit", TrayMenuExit);

            // Create a tray icon. In this example we use a
            // standard system icon for simplicity, but you
            // can of course use your own custom icon too.
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Durandal";
            trayIcon.Icon = new Icon("trayicon.ico");

            // Add menu to tray icon and show it.
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
            trayIcon.MouseClick += TrayMouseDown;

            // Only shutdown the app when we close it via the tray
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        private void TrayMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button.HasFlag(MouseButtons.Left))
            {
                client.Trigger();
            }
        }

        private void TrayMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button.HasFlag(MouseButtons.Left))
            {
                client.ForceTriggerFinish();
            }
        }

        private void TrayMenuExit(object sender, EventArgs e)
        {
            this.Shutdown();
        }
    }
}
