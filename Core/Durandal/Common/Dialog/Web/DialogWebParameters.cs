using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.LU;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Security.Server;
using Durandal.Common.Speech;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Utils;
using Durandal.Common.Cache;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Dialog.Web
{
    public class DialogWebParameters
    {
        public WeakPointer<DialogProcessingEngine> CoreEngine { get; set; }
        public DialogWebConfiguration ServerConfig { get; set; }
        public ILogger Logger { get; set; }
        public IFileSystem FileSystem { get; set; }
        public ILUClient LuConnection { get; set; }
        public WeakPointer<ICache<DialogAction>> DialogActionStore { get; set; }
        public IConversationStateCache ConversationStateCache { get; set; }
        public WeakPointer<ICache<CachedWebData>> WebDataCache { get; set; }
        public WeakPointer<ICache<ClientContext>> ClientContextCache { get; set; }
        public IPublicKeyStore PublicKeyStorage { get; set; }
        public IHttpServer HttpServer { get; set; }
        public WeakPointer<IThreadPool> ProcessingThreadPool { get; set; }
        public ISpeechSynth SpeechSynth { get; set; }
        public ISpeechRecognizerFactory SpeechReco { get; set; }
        public IAudioCodecFactory CodecFactory { get; set; }
        public IStreamingAudioCache StreamingAudioCache { get; set; }
        public IRealTimeProvider RealTimeProvider { get; set; }
        public IList<IDialogTransportProtocol> TransportProtocols { get; set; }
        public WeakPointer<IMetricCollector> Metrics { get; set; }
        public DimensionSet MetricDimensions { get; set; }
        public string MachineHostName { get; set; }

        public DialogWebParameters(DialogWebConfiguration config, WeakPointer<DialogProcessingEngine> engine)
        {
            ServerConfig = config;
            CoreEngine = engine;
            RealTimeProvider = DefaultRealTimeProvider.Singleton;
            Logger = NullLogger.Singleton;
            FileSystem = NullFileSystem.Singleton;
            LuConnection = null;
            DialogActionStore = new WeakPointer<ICache<DialogAction>>(new InMemoryCache<DialogAction>());
            ConversationStateCache = new InMemoryConversationStateCache();
            WebDataCache = new WeakPointer<ICache<CachedWebData>>(new InMemoryCache<CachedWebData>());
            ClientContextCache = new WeakPointer<ICache<ClientContext>>(new InMemoryCache<ClientContext>());
            StreamingAudioCache = new InMemoryStreamingAudioCache();
            PublicKeyStorage = new InMemoryPublicKeyStore(true);
            HttpServer = null;
            Metrics = NullMetricCollector.WeakSingleton;
            MetricDimensions = DimensionSet.Empty;
            ProcessingThreadPool = new WeakPointer<IThreadPool>(new TaskThreadPool(Metrics, MetricDimensions, "DialogWeb"));
            SpeechSynth = null;
            SpeechReco = null;
            CodecFactory = new RawPcmCodecFactory();
            MachineHostName = "Unknown";
            TransportProtocols = new List<IDialogTransportProtocol>() { new DialogJsonTransportProtocol(), new DialogLZ4JsonTransportProtocol() };
        }
    }
}
