using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.Speech.Triggers.Sphinx;
using Durandal.Common.File;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio.Codecs.Opus.Structs;
using Durandal.Common.Audio.Codecs.Opus.Ogg;
using Durandal.Common.Time;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.IO;
using System.Threading;
using System.Runtime.CompilerServices;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Speech.Triggers
{
    [TestClass]
    [DeploymentItem("TestData/Holland.opus")]
    public class SphinxTests
    {
        [Ignore]
        [TestMethod]
        public async Task SphinxParityTest()
        {
            ILogger logger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            KeywordSpottingConfiguration defaultKeywordConfig = new KeywordSpottingConfiguration()
            {
                PrimaryKeyword = "SOUL",
                PrimaryKeywordSensitivity = 8,
                SecondaryKeywords = new List<string>() { "CHURCH" },
                SecondaryKeywordSensitivity = 10
            };

            IDictionary<int, string> triggers = new Dictionary<int, string>();
            IFileSystem sphinxDataManager = new RealFileSystem(logger.Clone("SphinxFilesystem"), @"C:\Code\Durandal\Data\sphinx");
            PortablePocketSphinx sphinx = new PortablePocketSphinx(sphinxDataManager, logger.Clone("Sphinx"));

            int packetSizeSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(20));

            AudioTriggerResult triggerResult = null;

            using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (FileStream stream = new FileStream("Holland.opus", FileMode.Open, FileAccess.Read))
            using (OggOpusDecoder sampleSource = new OggOpusDecoder(new WeakPointer<IAudioGraph>(audioGraph), null, logger, null))
            using (PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(packetSizeSamplesPerChannel))
            using (SphinxAudioTrigger trigger = new SphinxAudioTrigger(new WeakPointer<IAudioGraph>(audioGraph), AudioSampleFormat.Mono(16000), null, sphinx, logger, "en-US-semi", "cmudict_SPHINX_40.txt", defaultKeywordConfig, false))
            {
                Assert.AreEqual(AudioInitializationResult.Success, await sampleSource.Initialize(new NonRealTimeStreamWrapper(stream, false), false, CancellationToken.None, realTime));
                long sampleNumber = 0;
                trigger.Initialize();
                trigger.ConnectInput(sampleSource);
                trigger.TriggeredEvent.Subscribe((sender, args, time) =>
                {
                    triggerResult = args.AudioTriggerResult;
                    return DurandalTaskExtensions.NoOpTask;
                });

                while (!sampleSource.PlaybackFinished)
                {
                    int amountRead = await sampleSource.ReadAsync(pooledBuf.Buffer, 0, packetSizeSamplesPerChannel, CancellationToken.None, realTime).ConfigureAwait(false);
                    if (amountRead > 0)
                    {
                        await trigger.WriteAsync(pooledBuf.Buffer, 0, amountRead, CancellationToken.None, realTime).ConfigureAwait(false);
                        sampleNumber += amountRead;
                    }

                    int packetNumber = (int)(sampleNumber / (long)packetSizeSamplesPerChannel);
                    if (triggerResult != null &&
                        triggerResult.Triggered)
                    {
                        triggers.Add(packetNumber, triggerResult.TriggeredKeyword);
                    }

                    triggerResult = null;

                    if (packetNumber % 10000 == 0)
                    {
                        trigger.Reset();
                    }

                    if (packetNumber == 20000)
                    {
                        trigger.Configure(new KeywordSpottingConfiguration()
                        {
                            PrimaryKeyword = "LOVE",
                            PrimaryKeywordSensitivity = 5,
                            SecondaryKeywords = new List<string>() { "FATHER", "PROMISE", "FACE" },
                            SecondaryKeywordSensitivity = 10
                        });
                    }
                }
            }

            Assert.IsTrue(triggers.ContainsKey(124));
            Assert.AreEqual("CHURCH", triggers[124]);
            Assert.IsTrue(triggers.ContainsKey(13728));
            Assert.AreEqual("SOUL", triggers[13728]);
            Assert.IsTrue(triggers.ContainsKey(31701));
            Assert.AreEqual("LOVE", triggers[31701]);
            Assert.IsTrue(triggers.ContainsKey(37735));
            Assert.AreEqual("FATHER", triggers[37735]);
            Assert.IsTrue(triggers.ContainsKey(38650));
            Assert.AreEqual("FACE", triggers[38650]);
            Assert.IsTrue(triggers.ContainsKey(43116));
            Assert.AreEqual("LOVE", triggers[43116]);
            Assert.IsTrue(triggers.ContainsKey(44971));
            Assert.AreEqual("FACE", triggers[44971]);
        }
    }
}
