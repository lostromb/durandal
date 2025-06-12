using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace MediaControl
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;

        private ILogger logger;
        private IFileSystem localDataManager;
        private WinampController winamp;
        private NLPTools nlTools;
        private MediaControlServer server;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ILogger debugLogger = new DebugLogger();
            ILogger fileLogger = new FileLogger("MediaControl");
            logger = new AggregateLogger("Main", new TaskThreadPool(), new NullStringEncrypter(), debugLogger, fileLogger);

            logger.Log("Loading configuration...");
            IFileSystem configResourceManager = new WindowsFileSystem(logger);
            IConfiguration config = IniFileConfiguration.Create(logger.Clone("Config"), new VirtualPath("MediaControl"), configResourceManager, DefaultRealTimeProvider.Singleton, warnIfNotFound: true, reloadOnExternalChanges: false).Await();

            logger.Log("Loading common data...");
            localDataManager = new WindowsFileSystem(logger.Clone("LocalFiles"), null);
            nlTools = new NLPTools();
            nlTools.Pronouncer = EnglishPronouncer.Create(new VirtualPath("cmu-pronounce-ipa.dict"), new VirtualPath("pron.cache"), logger.Clone("Pronouncer"), localDataManager).Await();
            nlTools.WordBreaker = new EnglishWholeWordBreaker();
            nlTools.FeaturizationWordBreaker = new EnglishWordBreaker();

            logger.Log("Initializing Winamp...");
            DirectoryInfo musicLibrary = new DirectoryInfo(config.GetString("mediaLibraryRoot"));
            DirectoryInfo cacheDir = new DirectoryInfo(Environment.CurrentDirectory);
            winamp = new WinampController(logger.Clone("WinampController"), musicLibrary, cacheDir, nlTools);

            int servicePort = config.GetInt32("servicePort");
            logger.Log("Starting media server on port " + servicePort);
            server = new MediaControlServer(servicePort, logger.Clone("MediaControlServer"), winamp);

            logger.Log("Creating tray icon...");

            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Update Library", UpdateLibrary);
            trayMenu.MenuItems.Add("Exit", TrayMenuExit);
            
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Media Remote";
            trayIcon.Icon = global::MediaControl.Resources.trayicon;
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
            trayIcon.MouseClick += TrayMouseClick;

            // Only shutdown the app when we close it via the tray
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            logger.Log("Startup completed OK");
        }

        private void TrayMouseClick(object sender, MouseEventArgs e)
        {
            //if (e.Button.HasFlag(MouseButtons.Left))
            //{
            //    Debug.WriteLine("Click");
            //}
        }

        private void TrayMenuExit(object sender, EventArgs e)
        {
            this.Shutdown();
        }

        private void UpdateLibrary(object sender, EventArgs e)
        {
            winamp.UpdateLibrary();
        }
    }
}
