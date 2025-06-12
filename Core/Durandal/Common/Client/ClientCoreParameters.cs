using Durandal.Common.Audio;
using Durandal.Common.Dialog;
using Durandal.Common.Net;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Time;
using Durandal.Common.Security.Client;
using Durandal.Common.Security;
using Durandal.Common.Security.Login;
using Durandal.Common.Net.Http;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Instrumentation;
using Durandal.Common.Speech;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Client
{
    public class ClientCoreParameters
    {
        /// <summary>
        /// Configuration for various parts of the client
        /// </summary>
        public ClientConfiguration ClientConfig { get; private set; }

        /// <summary>
        /// A delegate method that generates a client context for outgoing requests
        /// </summary>
        public ClientContextFactory ContextGenerator { get; private set; }

        /// <summary>
        /// A global logger to use for client initialization and all requests
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// The client that actually communicates with a dialog service
        /// </summary>
        public IDialogClient DialogConnection { get; set; }

        /// <summary>
        /// An adapter for presenting local HTML pages on the client side.
        /// In thi typical implementation, this is either a micro HTTP server or some kind of interceptor
        /// behind a locally framed web browser (like a XAML WebView).
        /// </summary>
        public IClientPresentationLayer LocalPresentationLayer { get; set; }

        /// <summary>
        /// The microphone input used for the audio client. If not present, audio requests will not be supported.
        /// </summary>
        public WeakPointer<IAudioSampleSource> Microphone { get; set; }

        /// <summary>
        /// The audio output used for the audio client
        /// </summary>
        public WeakPointer<IAudioSampleTarget> Speakers { get; set; }

        /// <summary>
        /// The audio graph which is connected to the microphone circuit of the client.
        /// </summary>
        public WeakPointer<IAudioGraph> InputAudioGraph { get; set; }

        /// <summary>
        /// The audio graph which is connected to the speaker circuit of the client
        /// </summary>
        public WeakPointer<IAudioGraph> OutputAudioGraph { get; set; }

        /// <summary>
        /// A keyword spotter. If not present, keyword spotting will be disabled.
        /// </summary>
        public WeakPointer<IAudioTrigger> AudioTrigger { get; set; }

        /// <summary>
        /// A speech recognition service. If not present, the client will just send the raw audio to dialog.
        /// </summary>
        public WeakPointer<ISpeechRecognizerFactory> SpeechReco { get; set; }

        /// <summary>
        /// A speech synthesizer for converting SSML to audio.
        /// </summary>
        public WeakPointer<ISpeechSynth> SpeechSynth { get; set; }

        /// <summary>
        /// An audio graph component which can detect the beginning/ending of utterances
        /// </summary>
        public WeakPointer<IUtteranceRecorder> UtteranceRecorder { get; set; }

        /// <summary>
        /// An audio codec to use for compressing audio sent to the service
        /// </summary>
        public IAudioCodecFactory CodecFactory { get; set; }

        /// <summary>
        /// A definition of real time, used for handling things like audio timestamps and delayed actions
        /// </summary>
        public IRealTimeProvider RealTimeProvider { get; set; }

        /// <summary>
        /// If true, attempt to load private keys and authenticate outgoing requests to the dialog service
        /// </summary>
        public bool EnableRSA { get; set; }

        /// <summary>
        /// A delegate object which handles client actions that are sent back by the dialog service.
        /// </summary>
        public IClientActionDispatcher ClientActionDispatcher { get; set; }

        /// <summary>
        /// A helper object for rendering HTML pages locally in common scenarios (like error cases)
        /// </summary>
        public IClientHtmlRenderer LocalHtmlRenderer { get; set; }

        /// <summary>
        /// A local store for loading private keys/certificates that are on the client only.
        /// </summary>
        public IClientSideKeyStore PrivateKeyStore { get; set; }

        /// <summary>
        /// A list of zero or more providers that can handle login callbacks for different services. For example,
        /// you could allow the client to log in users with Microsoft or Facebook account, as long as the provider
        /// is enabled for that account's login scheme.
        /// </summary>
        public IList<ILoginProvider> LoginProviders { get; set; }

        /// <summary>
        /// When a keyword is spotted and there are potentially multiple keyword-spotting devices within audio range, a
        /// verification query will be sent to this arbitrator which will enforce that only a single client will activate from that keyword.
        /// </summary>
        public ITriggerArbitrator AudioTriggerArbitrator { get; set; }

        /// <summary>
        /// A factory for HTTP clients, used for fetching streaming audio.
        /// </summary>
        public IHttpClientFactory HttpClientFactory { get; set; }

        /// <summary>
        /// A metric collector for reporting health/status of various parts of the code
        /// </summary>
        public WeakPointer<IMetricCollector> Metrics { get; set; }

        /// <summary>
        /// The default set of dimensions to use as a base when reporting client metrics.
        /// </summary>
        public DimensionSet MetricDimensions { get; set; }

        public ClientCoreParameters(ClientConfiguration config, ClientContextFactory contextGenerator)
        {
            ClientConfig = config;
            ContextGenerator = contextGenerator;
            Logger = NullLogger.Singleton;
            DialogConnection = null;
            LocalPresentationLayer = null;
            Microphone = WeakPointer<IAudioSampleSource>.Null;
            InputAudioGraph = WeakPointer<IAudioGraph>.Null;
            Speakers = WeakPointer<IAudioSampleTarget>.Null;
            OutputAudioGraph = WeakPointer<IAudioGraph>.Null;
            AudioTrigger = WeakPointer<IAudioTrigger>.Null;
            SpeechReco = WeakPointer<ISpeechRecognizerFactory>.Null;
            SpeechSynth = WeakPointer<ISpeechSynth>.Null;
            CodecFactory = new RawPcmCodecFactory();
            EnableRSA = false;
            ClientActionDispatcher = null;
            PrivateKeyStore = new NullClientKeyStore();
            LoginProviders = null;
            AudioTriggerArbitrator = null;
            Metrics = NullMetricCollector.WeakSingleton;
            MetricDimensions = DimensionSet.Empty;
            HttpClientFactory = new PortableHttpClientFactory(Metrics, MetricDimensions);
        }
    }
}
