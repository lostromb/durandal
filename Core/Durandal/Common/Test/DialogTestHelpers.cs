using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Test
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.NLP;
    using Durandal.Common.Config;
    using Durandal.Common.Logger;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.LU;
    using Durandal;
    using Durandal.Common.File;
    using System.Threading;
    using Durandal.Common.NLP.Language.English;
    using Durandal.Common.Tasks;
    using Durandal.Common.NLP.Annotation;
    using Durandal.Common.NLP.Language;
    using System.Threading.Tasks;
    using Instrumentation;
    using Durandal.Common.Time;
    using Durandal.Common.Test.Builders;
    using System.IO;
    using Audio.Components;
    using Durandal.Common.IO;
    using Durandal.Common.Utils;
    using Durandal.Common.Remoting;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Collections;

    public static class DialogTestHelpers
    {
        public static readonly string TEST_CLIENT_ID = "UnitTestClient";
        public static readonly string TEST_USER_ID = "UnitTestUser";

        private static EnglishWordBreaker _wordbreaker = new EnglishWordBreaker();

        public static DialogConfiguration GetTestDialogConfiguration(WeakPointer<IConfiguration> baseConfig)
        {
            DialogConfiguration fakeConfig = new DialogConfiguration(baseConfig);
            fakeConfig.IgnoreSideSpeech = true;
            fakeConfig.MinPluginConfidence = 0.7f;
            fakeConfig.MaxSideSpeechConfidence = 0.75f;
            fakeConfig.AllowedGlobalProfileEditors = new List<string>(new string[] { "reflection" });
            return fakeConfig;
        }

        public static DialogWebConfiguration GetTestDialogWebConfiguration(WeakPointer<IConfiguration> baseConfig)
        {
            DialogWebConfiguration fakeConfig = new DialogWebConfiguration(baseConfig);
            fakeConfig.SandboxPlugins = false;
            fakeConfig.FailFastPlugins = true;
            fakeConfig.MaxPluginExecutionTime = 10000;
            fakeConfig.TTSProvider = "sapi";
            fakeConfig.SRProvider = "sapi";
            fakeConfig.SpeechPoolSize = 2;
            return fakeConfig;
        }

        public static RemotingConfiguration GetTestRemotingConfiguration(WeakPointer<IConfiguration> baseConfig)
        {
            RemotingConfiguration fakeConfig = new RemotingConfiguration(baseConfig);
            fakeConfig.IpcProtocol = "json";
            fakeConfig.RemotingPipeImplementation = "mmio";
            fakeConfig.PluginLoader = "containerized";
            fakeConfig.KeepAlivePingTimeout = TimeSpan.FromSeconds(1);
            fakeConfig.KeepAliveFailureThreshold = 0;
            fakeConfig.KeepAlivePingInterval = TimeSpan.Zero;
            return fakeConfig;
        }

        public static ClientContext GetTestClientContextTextQuery()
        {
            return GetTestClientContext(ClientCapabilities.DisplayUnlimitedText |
                                        ClientCapabilities.DisplayHtml5 |
                                        ClientCapabilities.ServeHtml);
        }

        public static ClientContext GetTestClientContextAudioQuery()
        {
            return GetTestClientContext(ClientCapabilities.DisplayUnlimitedText |
                                        ClientCapabilities.DisplayHtml5 |
                                        ClientCapabilities.ServeHtml |
                                        ClientCapabilities.CanSynthesizeSpeech |
                                        ClientCapabilities.HasMicrophone |
                                        ClientCapabilities.HasSpeakers |
                                        ClientCapabilities.KeywordSpotter);
        }

        public static ClientContext GetTestClientContext(ClientCapabilities capabilities)
        {
            ClientContext returnVal = new ClientContext();
            returnVal.SetCapabilities(capabilities);
            returnVal.ClientId = TEST_CLIENT_ID;
            returnVal.UserId = TEST_USER_ID;
            returnVal.Locale = LanguageCode.EN_US;
            return returnVal;
        }

        public static List<RecoResult> GetSimpleRecoResultList(string domain, string intent, float confidence, string utterance = "This is a unit test case")
        {
            List<RecoResult> results = new List<RecoResult>();
            results.Add(GetSimpleRecoResult(domain, intent, confidence, utterance));
            return results;
        }

        public static RecoResult GetSimpleRecoResult(string domain, string intent, float confidence, string utterance = "This is a unit test case")
        {
            RecoResult result = new RecoResult()
            {
                Confidence = confidence,
                Domain = domain,
                Intent = intent,
                Utterance = _wordbreaker.Break(utterance)
            };
            result.TagHyps.Add(new TaggedData()
            {
                Confidence = 1.0f,
                Utterance = utterance
            });
            return result;
        }

        public static DialogRequest GetSimpleClientRequest(ClientContext context, string query, InputMethod source)
        {
            DialogRequest returnVal = new DialogRequest();
            returnVal.ClientContext = context;
            returnVal.InteractionType = source;
            if (source == InputMethod.Spoken)
            {
                returnVal.SpeechInput = new SpeechRecognitionResult()
                {
                    RecognitionStatus = SpeechRecognitionStatus.Success,
                    RecognizedPhrases = new List<SpeechRecognizedPhrase>()
                    {
                        new SpeechRecognizedPhrase()
                        {
                            DisplayText = query,
                            IPASyllables = string.Empty,
                            Locale = context.Locale.ToBcp47Alpha2String(),
                            SREngineConfidence = 0.95f,
                            InverseTextNormalizationResults = new List<string>()
                            {
                                query
                            },
                            MaskedInverseTextNormalizationResults = new List<string>()
                            {
                                query
                            }
                        }
                    }
                };
            }
            else
            {
                returnVal.TextInput = query;
            }

            return returnVal;
        }

        public static AudioData GenerateAudioData(AudioSampleFormat format, int lengthMs)
        {
            AudioSample utterance = GenerateUtterance(format, lengthMs);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (MemoryStream outputStream = new MemoryStream())
            using (FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), utterance, nodeCustomName: null))
            using (RawPcmEncoder encoder = new RawPcmEncoder(new WeakPointer<IAudioGraph>(graph), format, nodeCustomName: null))
            {
                encoder.Initialize(new NonRealTimeStreamWrapper(outputStream, false), false, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                sampleSource.ConnectOutput(encoder);
                sampleSource.WriteFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                AudioData returnVal = new AudioData()
                {
                    Codec = encoder.Codec,
                    CodecParams = encoder.CodecParams,
                    Data = new ArraySegment<byte>(outputStream.ToArray())
                };

                return returnVal;
            }
        }

        /// <summary>
        /// Generates a constant audio tone that fits the desired sample rate and length requirements.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="lengthMs"></param>
        /// <returns></returns>
        public static AudioSample GenerateUtterance(AudioSampleFormat format, int lengthMs)
        {
            int length = (int)((long)format.SampleRateHz * lengthMs / 1000);
            float[] samples = new float[length * format.NumChannels];
            double toneGen = 0;
            double envelopeGen = 0;
            double VOLUME = 0.4;
            double TONE_RATE = 3741/*hz*/ * (2 * Math.PI) / (double)format.SampleRateHz;
            double ENVELOPE_RATE = 3/*hz*/ * (2 * Math.PI) / (double)format.SampleRateHz;
            for (int c = 0; c < length * format.NumChannels;)
            {
                for (int chan = 0; chan < format.NumChannels; chan++)
                {
                    samples[c++] = (float)(Math.Sin(toneGen) * Math.Sin(envelopeGen) * VOLUME);
                }

                toneGen += TONE_RATE;
                envelopeGen += ENVELOPE_RATE;
            }

            return new AudioSample(samples, format);
        }

        public static async Task<LanguageUnderstandingEngine> BuildLUEngine(ILogger logger, IEnumerable<FakeLUModel> models, IRealTimeProvider realTime, IAnnotatorProvider annotators = null)
        {
            List<LanguageCode> locales = new List<LanguageCode>();
            locales.Add(LanguageCode.EN_US);
            LUConfiguration luConfig = new LUConfiguration(new WeakPointer<IConfiguration>(new InMemoryConfiguration(logger)), logger.Clone("LUConfig"));
            if (annotators == null)
            {
                annotators = new BasicAnnotatorProvider();
                luConfig.AnnotatorsToLoad = new HashSet<string>();
            }
            else
            {
                luConfig.AnnotatorsToLoad = new HashSet<string>(annotators.GetAllAnnotators());
            }

            IFileSystem fileSystem = new InMemoryFileSystem();
            List<string> domains = new List<string>();

            foreach (FakeLUModel model in models)
            {
                VirtualPath trainingTemplateFile = new VirtualPath("\\training\\en-US\\" + model.Domain + ".template");
                List<string> training = new List<string>();
                training.Add("#STATIC#");
                training.FastAddRangeList(model.Training);
                training.Add("#REGEX#");
                training.FastAddRangeList(model.Regexes);
                fileSystem.WriteLines(trainingTemplateFile, training);
                domains.Add(model.Domain);

                if (model.DomainConfig != null)
                {
                    VirtualPath domainConfigPath = new VirtualPath("\\modelconfig\\en-US\\" + model.Domain + ".modelconfig.ini");
                    using (IniFileConfiguration configIni = await IniFileConfiguration.Create(
                        logger,
                        domainConfigPath,
                        fileSystem,
                        realTime).ConfigureAwait(false))
                    {
                        logger.Log("Copying domain configuration to " + domainConfigPath.FullName);
                        foreach (var configValue in model.DomainConfig.GetAllValues().Values)
                        {
                            logger.Log("Copying " + configValue.ToString());
                            configIni.Set(configValue);
                        }
                    }
                }
            }

            ICultureInfoFactory cultureInfoFactory = new BasicCultureInfoFactory(logger);
            LanguageUnderstandingEngine engine = new LanguageUnderstandingEngine(
                luConfig,
                logger.Clone("LU"),
                fileSystem,
                annotators,
                new TaskThreadPool(NullMetricCollector.WeakSingleton, DimensionSet.Empty, "LUWorkers"));

            engine.Initialize(locales, cultureInfoFactory, realTime);
            engine.LoadModels(LanguageCode.EN_US, domains);
            long totalWaitTime = 0;
            while (!engine.AnyModelLoaded && totalWaitTime < 60000)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None).ConfigureAwait(false);
                totalWaitTime += 10;
            }

            if (totalWaitTime >= 60000)
            {
                logger.Log("Took over " + totalWaitTime + "ms to load mock LU model. This will likely cause test problems. Please investigate", LogLevel.Wrn);
            }

            return engine;
        }
    }
}
