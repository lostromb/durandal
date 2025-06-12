namespace DialogEngineAspCore
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Security.Server;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.SR.Azure;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Speech.TTS.Bing;
    using Durandal.Common.Cache;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Extensions.BondProtocol;
    using Durandal.Extensions.MySql;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Durandal.Common.Test;
    using Durandal.Common.Utils;
    using Durandal.Common.Collections;
    using Durandal.Extensions.Redis;
    using System.Threading;

    /// <summary>
    /// Static helper class for constructing the primary interfaces that are used for dialog engine (caches, codecs, SR, TTS, etc.)
    /// </summary>
    public static class ServiceFactory
    {
        private static readonly string SERVICES_PROVIDER_CONFIG_KEY = "servicesProvider";
        private static readonly string SERVICE_PROVIDER_MEMORY = "memory";
        private static readonly string SERVICE_PROVIDER_MYSQL = "mysql";
        private static readonly string SERVICE_PROVIDER_REDIS = "redis";

        /// <summary>
        /// Attempts to initialize a speech synthesizer using the given provider name
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static ISpeechSynth TryGetSpeechSynth(
            string providerName,
            ILogger logger,
            IDictionary<string, NLPTools> nlTools,
            string apiKey,
            IMetricCollector metrics,
            DimensionSet metricDimensions,
            IThreadPool threadPool,
            int maxPoolSize = 1)
        {
            logger.Log("Initializing TTS provider \"" + providerName + "\"...");

            if (providerName.Equals("bing", StringComparison.InvariantCultureIgnoreCase))
            {
                return new BingSpeechSynth(logger, apiKey, new PortableHttpClientFactory(), VoiceGender.Female);
            }
            //else if (providerName.Equals("google", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    return new GoogleSpeechSynth(logger);
            //}
            //else if (providerName.Equals("sapi", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    return new SapiSpeechSynth(logger, threadPool, AudioSampleFormat.Mono(16000), metrics, metricDimensions, maxPoolSize);
            //}
            //else if (providerName.Equals("sapi+bing", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    // This setup will prefer SAPI if available but use the custom provider in fallback (for things like non-native synthesis for translation, etc.)
            //    ISpeechSynth sapiSpeechSynth = new SapiSpeechSynth(logger.Clone("DialogTTS"), threadPool, AudioSampleFormat.Mono(16000), metrics, metricDimensions, maxPoolSize);
            //    ISpeechSynth fallbackTts = new BingSpeechSynth(logger, apiKey, VoiceGender.Female);
            //    return new AggregateSpeechSynth(sapiSpeechSynth, fallbackTts);
            //}
            else
            {
                return new FakeSpeechSynth("en-us");
            }
        }

        /// <summary>
        /// Attempts to initialize a speech recognizer using the given provider name
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static ISpeechRecognizerFactory TryGetSpeechRecognizer(string providerName, ILogger logger, string apiKey, bool enableIntermediateResults, IRealTimeProvider realTime, int maxPoolSize = 1)
        {
            logger.Log("Initializing SR provider \"" + providerName + "\"...");
            if (providerName.Equals("azure", StringComparison.InvariantCultureIgnoreCase))
            {
                return new AzureSpeechRecognizerFactory(new PortableHttpClientFactory(), new TcpClientSocketFactory(logger.Clone("SRSocketFactory")), logger.Clone("AzureSpeechReco"), apiKey, realTime);
            }
            //else if (providerName.Equals("azure-native", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    return new AzureNativeSpeechRecognizerFactory(new PortableHttpClientFactory(), logger.Clone("AzureNativeSpeechReco"), apiKey, realTime);
            //}
            //else if (providerName.Equals("cortana", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    return new CortanaSpeechRecognizerFactory(new TcpClientSocketFactory(logger.Clone("SRSocketFactory")), logger.Clone("CortanaSpeechReco"), apiKey, realTime);
            //}
            //else if (providerName.Equals("oxford", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    return new OxfordSpeechRecognizerFactory(logger.Clone("OxfordSpeechReco"), apiKey);
            //}
            //else if (providerName.Equals("bing", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    return new BingSpeechRecognizerFactory(logger.Clone("BingSpeechReco"), enableIntermediateResults);
            //}
            //else if (providerName.Equals("google", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    return new GoogleLegacySpeechRecognizerFactory(logger.Clone("GoogleSpeechReco"), enableIntermediateResults);
            //}
            else
            {
                return NullSpeechRecoFactory.Singleton;
            }
        }

        /// <summary>
        /// Builds a collection of default dialog codecs
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static IAudioCodecFactory CreateCodecCollection(ILogger logger)
        {
            IAudioCodecFactory returnVal = new AggregateCodecFactory(
                new RawPcmCodecFactory(),
                new OpusRawCodecFactory(
                    complexity: 0,
                    bitrateKbps: 40,
                    forceMode: Durandal.Common.Audio.Codecs.Opus.Enums.OpusMode.MODE_CELT_ONLY,
                    audioTypeHint: Durandal.Common.Audio.Codecs.Opus.Enums.OpusApplication.OPUS_APPLICATION_VOIP,
                    frameSize: Durandal.Common.Audio.Codecs.Opus.Enums.OpusFramesize.OPUS_FRAMESIZE_5_MS),
                new ALawCodecFactory(),
                new ULawCodecFactory(),
                new SquareDeltaCodecFactory(),
                new OggOpusCodecFactory());

            return returnVal;
        }

        /// <summary>
        /// Builds the collection of dialog services (caches, session store, etc) with varying implementation based on the serviceProvider configuration value
        /// </summary>
        /// <param name="mainConfig"></param>
        /// <param name="coreLogger"></param>
        /// <param name="workerThreadPool"></param>
        /// <param name="metrics"></param>
        /// <param name="fileSystem"></param>
        /// <returns></returns>
        public static async Task<DialogServiceCollection> BuildServices(
            IConfiguration mainConfig,
            ILogger coreLogger,
            IThreadPool workerThreadPool,
            IMetricCollector metrics,
            DimensionSet metricDimensions,
            IFileSystem fileSystem)
        {
            DialogServiceCollection returnVal = new DialogServiceCollection();

            HashSet<string> allServiceProviders = new HashSet<string>(
                new string[] {
                    mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "conversation_cache" } }),
                    mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "dialog_action_cache" } }),
                    mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "web_data_cache" } }),
                    mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "client_context_cache" } }),
                    mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "public_key_store" } }),
                    mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "oauth_secret_store" } }),
                    mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "user_profile_store" } }),
                    mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { {  "service", "streaming_audio_cache" } }),
                });

            // Initialize MySql if any services require it
            MySqlConnectionPool sqlConnectionPool = null;
            FixedCapacityThreadPool mySqlFireAndForgetThreadPool = null;
            if (allServiceProviders.Contains(SERVICE_PROVIDER_MYSQL))
            {
                string connectionString = mainConfig.GetString("mySqlConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                {
                    coreLogger.Log("The SQL connection string (mySqlConnectionString) is invalid. You're going to have a bad time", LogLevel.Err);
                }

                // This thread pool is used to manage fire-and-forget operations made to the sql server
                mySqlFireAndForgetThreadPool = new FixedCapacityThreadPool(
                    new CustomThreadPool(coreLogger.Clone("SqlThreadPool"), metrics, metricDimensions, ThreadPriority.Normal, "SqlPool", 8, true),
                    coreLogger.Clone("SqlLimitedThreadPool"),
                    metrics,
                    metricDimensions,
                    "SqlFireAndForgetPool",
                    8,
                    ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable,
                    TimeSpan.FromMilliseconds(5000));

                coreLogger.Log("Initializing MySql database connections and service providers...");
                sqlConnectionPool = await MySqlConnectionPool.Create(connectionString, coreLogger.Clone("MySqlConnectionPool"), metrics, metricDimensions, "Default", true);
                returnVal.Disposables.Add(sqlConnectionPool);
                returnVal.Disposables.Add(mySqlFireAndForgetThreadPool);
            }

            // Initialize Redis if any services require it
            RedisConnectionPool redisConnectionPool = null;
            if (allServiceProviders.Contains(SERVICE_PROVIDER_REDIS))
            {
                string connectionString = mainConfig.GetString("redisConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                {
                    coreLogger.Log("The Redis connection string (redisConnectionString) is invalid. You're going to have a bad time", LogLevel.Err);
                }

                redisConnectionPool = await RedisConnectionPool.Create(connectionString, coreLogger.Clone("RedisConnectionPool"));
                returnVal.Disposables.Add(redisConnectionPool);
            }

            // Now go through and create each service provider implementation that is specified in the config
            string serviceProvider = mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "conversation_cache" } });
            coreLogger.Log("Initializing service provider conversation_cache with provider = " + serviceProvider);
            if (SERVICE_PROVIDER_MEMORY.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.ConversationStateCache = new InMemoryConversationStateCache();
            }
            else if (SERVICE_PROVIDER_MYSQL.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                var sqlConversationStateCache = new MySqlConversationStateCache(
                   sqlConnectionPool,
                   metrics,
                      metricDimensions,
                   new BondByteConverterConversationStateStack(),
                   coreLogger.Clone("SqlSessionStore"),
                   mySqlFireAndForgetThreadPool);
                await sqlConversationStateCache.Initialize();
                returnVal.ConversationStateCache = sqlConversationStateCache;
            }
            else
            {
                throw new NotImplementedException("Unknown service provider \"" + serviceProvider + "\"");
            }

            serviceProvider = mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "dialog_action_cache" } });
            coreLogger.Log("Initializing service provider dialog_action_cache with provider = " + serviceProvider);
            if (SERVICE_PROVIDER_MEMORY.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.DialogActionStore = new InMemoryCache<DialogAction>();
            }
            else if (SERVICE_PROVIDER_MYSQL.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                var sqlDialogActionCache = new MySqlCache<DialogAction>(
                      sqlConnectionPool,
                      metrics,
                      metricDimensions,
                      new BondByteConverterDialogAction(),
                      coreLogger.Clone("DialogActionSQLCache"),
                      mySqlFireAndForgetThreadPool);

                await sqlDialogActionCache.Initialize();
                returnVal.DialogActionStore = sqlDialogActionCache;
            }
            else if (SERVICE_PROVIDER_REDIS.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.DialogActionStore = new RedisCache<DialogAction>(
                    new WeakPointer<RedisConnectionPool>(redisConnectionPool),
                    metrics,
                    new BondByteConverterDialogAction(),
                    coreLogger.Clone("DialogActionRedisCache"));
            }
            else
            {
                throw new NotImplementedException("Unknown service provider \"" + serviceProvider + "\"");
            }

            serviceProvider = mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "web_data_cache" } });
            coreLogger.Log("Initializing service provider web_data_cache with provider = " + serviceProvider);
            if (SERVICE_PROVIDER_MEMORY.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.WebDataStore = new InMemoryCache<CachedWebData>();
            }
            else if (SERVICE_PROVIDER_MYSQL.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                var sqlWebDataStore = new MySqlCache<CachedWebData>(
                   sqlConnectionPool,
                   metrics,
                   metricDimensions,
                   new BondByteConverterCachedWebData(),
                   coreLogger.Clone("WebDataSQLCache"),
                   mySqlFireAndForgetThreadPool);
                await sqlWebDataStore.Initialize();
                returnVal.WebDataStore = sqlWebDataStore;
            }
            else if (SERVICE_PROVIDER_REDIS.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.WebDataStore = new RedisCache<CachedWebData>(
                    new WeakPointer<RedisConnectionPool>(redisConnectionPool),
                    metrics,
                    new BondByteConverterCachedWebData(),
                    coreLogger.Clone("WebDataRedisCache"));
            }
            else
            {
                throw new NotImplementedException("Unknown service provider \"" + serviceProvider + "\"");
            }

            serviceProvider = mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "client_context_cache" } });
            coreLogger.Log("Initializing service provider client_context_cache with provider = " + serviceProvider);
            if (SERVICE_PROVIDER_MEMORY.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.ClientContextStore = new InMemoryCache<ClientContext>();
            }
            else if (SERVICE_PROVIDER_MYSQL.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                var sqlClientContextStore = new MySqlCache<ClientContext>(
                    sqlConnectionPool,
                    metrics,
                    metricDimensions,
                    new BondByteConverterClientContext(),
                    coreLogger.Clone("ClientContextSQLCache"),
                    mySqlFireAndForgetThreadPool);
                await sqlClientContextStore.Initialize();
                returnVal.ClientContextStore = sqlClientContextStore;
            }
            else if (SERVICE_PROVIDER_REDIS.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.ClientContextStore = new RedisCache<ClientContext>(
                    new WeakPointer<RedisConnectionPool>(redisConnectionPool),
                    metrics,
                    new BondByteConverterClientContext(),
                    coreLogger.Clone("ClientContextRedisCache"));
            }
            else
            {
                throw new NotImplementedException("Unknown service provider \"" + serviceProvider + "\"");
            }

            serviceProvider = mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "public_key_store" } });
            coreLogger.Log("Initializing service provider public_key_store with provider = " + serviceProvider);
            if (SERVICE_PROVIDER_MEMORY.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.PublicKeyStore = new FileBasedPublicKeyStore(new VirtualPath("known_clients.tsv"), fileSystem, coreLogger.Clone("ClientInfoStorage"));
            }
            else if (SERVICE_PROVIDER_MYSQL.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                var sqlSecureClientStorage = new MySqlPublicKeyStore(sqlConnectionPool, coreLogger.Clone("SqlClientInfoStorage"));
                await sqlSecureClientStorage.Initialize();
                returnVal.PublicKeyStore = sqlSecureClientStorage;
            }
            else
            {
                throw new NotImplementedException("Unknown service provider \"" + serviceProvider + "\"");
            }

            serviceProvider = mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "oauth_secret_store" } });
            coreLogger.Log("Initializing service provider oauth_secret_store with provider = " + serviceProvider);
            if (SERVICE_PROVIDER_MEMORY.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.OAuthSecretStore = new InMemoryOAuthSecretStore();
            }
            else if (SERVICE_PROVIDER_MYSQL.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                var sqlOAuthSecretStore = new MySqlOAuthSecretStore(sqlConnectionPool, coreLogger.Clone("SqlSecretStore"));
                await sqlOAuthSecretStore.Initialize();
                returnVal.OAuthSecretStore = sqlOAuthSecretStore;
            }
            else
            {
                throw new NotImplementedException("Unknown service provider \"" + serviceProvider + "\"");
            }

            serviceProvider = mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "user_profile_store" } });
            coreLogger.Log("Initializing service provider user_profile_store with provider = " + serviceProvider);
            if (SERVICE_PROVIDER_MEMORY.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.UserProfileStore = new InMemoryProfileStorage();
            }
            else if (SERVICE_PROVIDER_MYSQL.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                var sqlUserProfileStorage = new MySqlUserProfileStorage(sqlConnectionPool, coreLogger.Clone("SqlUserProfileStore"));
                await sqlUserProfileStorage.Initialize();
                returnVal.UserProfileStore = sqlUserProfileStorage;
            }
            else
            {
                throw new NotImplementedException("Unknown service provider \"" + serviceProvider + "\"");
            }

            serviceProvider = mainConfig.GetString(SERVICES_PROVIDER_CONFIG_KEY, SERVICE_PROVIDER_MEMORY, new SmallDictionary<string, string>() { { "service", "streaming_audio_cache" } });
            coreLogger.Log("Initializing service provider streaming_audio_cache with provider = " + serviceProvider);
            if (SERVICE_PROVIDER_MEMORY.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.StreamingAudioCache = new InMemoryStreamingAudioCache();
            }
            //else if (SERVICE_PROVIDER_MYSQL.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            //{
            //    var sqlStreamingAudioCache = new MySqlStreamingAudioCache(sqlConnectionPool, coreLogger.Clone("SqlAudioCache"), workerThreadPool, metrics, metricDimensions, true);
            //    await sqlStreamingAudioCache.Initialize();
            //    returnVal.StreamingAudioCache = sqlStreamingAudioCache;
            //}
            else if (SERVICE_PROVIDER_REDIS.Equals(serviceProvider, StringComparison.OrdinalIgnoreCase))
            {
                returnVal.StreamingAudioCache = new RedisStreamingAudioCache(new WeakPointer<RedisConnectionPool>(redisConnectionPool));
            }
            else
            {
                throw new NotImplementedException("Unknown service provider \"" + serviceProvider + "\"");
            }

            return returnVal;
        }

        public static IServerSocketFactory CreateServerSocketFactory(string ipcMethod, ILogger logger)
        {
            if (string.Equals(ipcMethod, MMIOClientSocket.PROTOCOL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                return new MMIOServerSocketFactory();
            }
            else if (string.Equals(ipcMethod, AnonymousPipeClientSocket.PROTOCOL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                return new AnonymousPipeServerSocketFactory();
            }
            else if (string.Equals(ipcMethod, Win32Socket.PROTOCOL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                return new Win32ServerSocketFactory();
            }
            else
            {
                logger.Log("Unknown pipe implementation \"" + ipcMethod + "\", defaulting to anonymous pipe");
                return new AnonymousPipeServerSocketFactory();
            }
        }
    }
}
