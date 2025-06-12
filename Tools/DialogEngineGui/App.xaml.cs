

namespace Durandal
{
    using Durandal.Extensions.BondProtocol;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.LU;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP.Alignment;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.NLP.Language.English;
    using Durandal.Common.Speech;
    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.NLP;
    using Durandal.Common.File;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Time;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Audio.Codecs.Opus;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ThreadedDialogWebService _core;
        private ILogger _coreLogger;

        public ThreadedDialogWebService GetDialogEngine()
        {
            return _core;
        }

        public ILogger GetLogger()
        {
            return _coreLogger;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            ILogger bootstrapLogger = new DebugLogger("Bootstrap");
            IFileSystem fileSystem = new RealFileSystem(bootstrapLogger);
            _coreLogger = new FileLogger(
                fileSystem,
                "DialogService",
                backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL,
                bootstrapLogger: bootstrapLogger);
            IConfiguration mainConfig = await IniFileConfiguration.Create(_coreLogger.Clone("DialogConfig"), new VirtualPath("DialogEngine_config.ini"), fileSystem, DefaultRealTimeProvider.Singleton, true, true);
        
            string luEndpoint = mainConfig.GetString("luServerHost", "localhost");
            int luPort = mainConfig.GetInt32("luServerPort", 62291);
            ILogger luHttpLogger = _coreLogger.Clone("LUHttpClient");
            IHttpClient luClient = new SocketHttpClient(
                new WeakPointer<ISocketFactory>(new TcpClientSocketFactory(luHttpLogger)),
                new TcpConnectionConfiguration(luEndpoint, luPort, false),
                luHttpLogger,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                Http2SessionManager.Default,
                new Http2SessionPreferences());
            luClient.SetReadTimeout(TimeSpan.FromMilliseconds(mainConfig.GetInt32("luTimeout", 2000)));

            ILUTransportProtocol luProtocol = new LUBondTransportProtocol();
            LUHttpClient luInterface = new LUHttpClient(luClient, luHttpLogger, luProtocol);
            _coreLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "LU connection is configured for {0}", luClient.ServerAddress);

            // TODO: Move this into a common builder/helper class
            IPronouncer pronouncer = await EnglishPronouncer.Create(
                new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-US\\cmu-pronounce-ipa.dict"),
                new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\english_pronounce.dat"),
                _coreLogger.Clone("Pronouncer"),
                fileSystem);

            IWordBreaker wordBreaker = new EnglishWordBreaker();
            IWordBreaker wholeWordBreaker = new EnglishWholeWordBreaker();
            NLPToolsCollection nlpTools = new NLPToolsCollection();
            EditDistancePronunciation pronouncerEditDistance = new EditDistancePronunciation(pronouncer, wholeWordBreaker, LanguageCode.EN_US);
            ILGFeatureExtractor lgFeaturizer = new EnglishLGFeatureExtractor();
            nlpTools.Add(LanguageCode.EN_US, new NLPTools()
                {
                    Pronouncer = pronouncer,
                    WordBreaker = wholeWordBreaker,
                    FeaturizationWordBreaker = wordBreaker,
                    EditDistance = pronouncerEditDistance.Calculate,
                    LGFeatureExtractor = lgFeaturizer,
                    CultureInfoFactory = new WindowsCultureInfoFactory()
                });

            DialogWebParameters webServiceParameters = new DialogWebParameters(new DialogWebConfiguration(new WeakPointer<IConfiguration>(mainConfig)), new WeakPointer<DialogProcessingEngine>(null))
            {
                Logger = _coreLogger,
                FileSystem = fileSystem,
                LuConnection = luInterface,
                CodecFactory = new OpusRawCodecFactory(_coreLogger.Clone("OpusCodec"))
            };

            IDurandalPluginLoader pluginLoader = new ResidentDllPluginLoader(
                new BasicDialogExecutor(false),
                _coreLogger.Clone("PluginAnswerProvider"),
                fileSystem,
                new VirtualPath(RuntimeDirectoryName.PLUGIN_DIR),
                fileSystem,
                PluginFrameworkLevel.NetFull);

            IDurandalPluginProvider pluginProvider = new MachineLocalPluginProvider(
                _coreLogger,
                pluginLoader,
                fileSystem,
                nlpTools,
                null,
                null,
                null,
                null,
                new PortableHttpClientFactory(),
                null);

            DialogEngineParameters dialogParameters = new DialogEngineParameters(new DialogConfiguration(new WeakPointer<IConfiguration>(mainConfig)), new WeakPointer<IDurandalPluginProvider>(pluginProvider))
            {
                Logger = _coreLogger,
            };

            _core = new ThreadedDialogWebService(_coreLogger.Clone("ThreadedDialogWebService"), dialogParameters, webServiceParameters);

            _core.EngineStopped += EngineStoppedEvent;
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _core.Dispose();
        }

        private void EngineStoppedEvent(object source, EventArgs args)
        {
            Environment.Exit(0);
        }
    }
}
