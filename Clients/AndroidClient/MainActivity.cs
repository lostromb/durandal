using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Webkit;
using System.Diagnostics;
using Durandal.Common.Client;
using Durandal.Common.Config;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Dialog;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Dialog.Web;
using Durandal.AndroidClient.Common;
using Durandal.Common.Utils.Tasks;
using System.Threading.Tasks;
using System.Collections.Generic;
using Durandal.Common.File;
using Durandal.Common.NLP.Language;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.Utils.NativePlatform;

namespace Durandal.AndroidClient
{
    /// <summary>
    /// The entry point activity which orchestrates all the others. Visually, it shows some kind
    /// of loading screen, initializes the global client and associated state, and then hands off to the ClientActivity
    /// </summary>
    [Activity(Label = "Durandal", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private static ILogger _logger;
        private static ClientCore _client;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            NativePlatformUtils.SetGlobalResolver(new AndroidNativeLibraryResolver());

            // Start initializing the client
            _logger = new DebugLogger();

            PerformanceClass cl = NativePlatformUtils.GetMachinePerformanceClass();
            _logger.Log("Perf class is " + cl);

            // Scan our whole filesystem
            //_logger.Log("SCANNING this.Application.FilesDir");
            //CrawlDirectory(new AndroidBasicFileSystem(this.Application.FilesDir), VirtualPath.Root, _logger);
            //_logger.Log("SCANNING this.Application.DataDir");
            //CrawlDirectory(new AndroidBasicFileSystem(this.Application.DataDir), VirtualPath.Root, _logger);


            _logger.Log("Starting to initialize client...");
            IFileSystem fileSystem = new AndroidBasicFileSystem(this.Application.FilesDir);

            IConfiguration baseConfig = IniFileConfiguration.Create(_logger, new VirtualPath("client_config"), fileSystem, DefaultRealTimeProvider.Singleton).Await();
            ClientConfiguration config = new ClientConfiguration(baseConfig);
            config.AudioCodec = "opus";
            config.ClientName = "Android client";
            config.Locale = LanguageCode.EN_US;
            config.LocalPresentationServerPort = 62293;

            //IDialogClient dialogClient = new DialogHttpClient(new DirectHttpClient(new NullHttpServer()), logger, new DialogJsonTransportProtocol());

            //ClientCoreParameters coreParams = new ClientCoreParameters(config, GenerateClientContext)
            //{
            //    Logger = logger,
            //    DialogConnection = dialogClient
            //};

            //_client = new ClientCore();
            //_client.Initialize(coreParams);

            StartActivity(typeof(ClientActivity));
        }

        private static ClientContext GenerateClientContext()
        {
            return null;
        }

        public static ILogger Logger => _logger;
        public static ClientCore Client => _client;
    }
}

