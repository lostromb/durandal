using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.Collections.Interning;
using Durandal.Common.Collections.Interning.Impl;
using Durandal.Common.Compression.LZ4;
using Durandal.Common.Compression.ZLib;
using Durandal.Common.Events;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.IO.Crc;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using Durandal.Common.Remoting;
using Durandal.Common.Remoting.Handlers;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Remoting.Proxies;
using Durandal.Common.Security;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Speech.SR;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Extensions.BondProtocol;
using Durandal.Extensions.Compression.Crc;
using Durandal.Extensions.Compression.ZStandard.File;
using IronCompress;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Prototype.NetCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            //AssemblyReflector.ApplyAccelerators(typeof(CRC32CAccelerator).Assembly, new ConsoleLogger("Main", LogLevel.All));
            //EchoMicrophone().Await();
            //SplitAudio().Await();
            //RunInternalizerLoop();

            //string cert = "3082071C30820604A00302010202137C0351EAAAABFF50D051BAB18F00000351EAAA300D06092A864886F70D01010B0500304431133011060A0992268993F22C640119160347424C31133011060A0992268993F22C6401191603414D45311830160603550403130F414D4520496E667261204341203035301E170D3233303533303137353135315A170D3234303532343137353135315A302D312B3029060355040313226F6666696365333635736561726368736572766963652E6F75746C6F6F6B2E636F6D30820122300D06092A864886F70D01010105000382010F003082010A0282010100CCCB0524285D180DAA64356AD3F567C492D2CF592E57C214E376B1503498EDAD8B1BA7DF2DFB0E61A2B92869FE5EBEC986704AC6E0392E021020A07BEDC2F996122D4DD7E6FFEDF6B375C3775CB0F8A952C8E0F3EB5A2A87B9CEF53543422E58646BA8BF1E97977DF25332CA02416F655C379F6990039E8FD3789B99F7D974FEC6F8678541B5167676E529DFF6C68D1C75F1B549A557DCE63BD52E54DB70C03ADF8289643DD33278221E5120BBD1014EBAFB4EA96647B80A24990A0BA83614D1547ED4B9D156EBFE8D615999B19A64F727712F4CA20A642BF3638BE6F568B3D091D091E28E25AB57025416C2C2C4F9D24D543062212FBC3CFA8E34D1B7296AF10203010001A382041C30820418302706092B060104018237150A041A3018300A06082B06010505070301300A06082B06010505070302303D06092B06010401823715070430302E06262B06010401823715088690E30D84D5B47884F18B3E859BDD16CE9D12816082F5F62B83F2D12002016402010A308201CB06082B06010505070101048201BD308201B9306306082B060105050730028657687474703A2F2F63726C2E6D6963726F736F66742E636F6D2F706B69696E6672612F43657274732F434F31504B49494E54434130312E414D452E47424C5F414D45253230496E667261253230434125323030352E637274305306082B060105050730028647687474703A2F2F63726C312E616D652E67626C2F6169612F434F31504B49494E54434130312E414D452E47424C5F414D45253230496E667261253230434125323030352E637274305306082B060105050730028647687474703A2F2F63726C322E616D652E67626C2F6169612F434F31504B49494E54434130312E414D452E47424C5F414D45253230496E667261253230434125323030352E637274305306082B060105050730028647687474703A2F2F63726C332E616D652E67626C2F6169612F434F31504B49494E54434130312E414D452E47424C5F414D45253230496E667261253230434125323030352E637274305306082B060105050730028647687474703A2F2F63726C342E616D652E67626C2F6169612F434F31504B49494E54434130312E414D452E47424C5F414D45253230496E667261253230434125323030352E637274301D0603551D0E041604144A6CD0BDDA1EE911E8E91E20548A1A72F5C27CA2300E0603551D0F0101FF0404030205A0302D0603551D110426302482226F6666696365333635736561726368736572766963652E6F75746C6F6F6B2E636F6D308201260603551D1F0482011D3082011930820115A0820111A082010D863F687474703A2F2F63726C2E6D6963726F736F66742E636F6D2F706B69696E6672612F43524C2F414D45253230496E667261253230434125323030352E63726C8631687474703A2F2F63726C312E616D652E67626C2F63726C2F414D45253230496E667261253230434125323030352E63726C8631687474703A2F2F63726C322E616D652E67626C2F63726C2F414D45253230496E667261253230434125323030352E63726C8631687474703A2F2F63726C332E616D652E67626C2F63726C2F414D45253230496E667261253230434125323030352E63726C8631687474703A2F2F63726C342E616D652E67626C2F63726C2F414D45253230496E667261253230434125323030352E63726C30170603551D200410300E300C060A2B0601040182377B0101301F0603551D230418301680147AD6198528796C71761E60F8F34BEFA20542161D301D0603551D250416301406082B0601050507030106082B06010505070302300D06092A864886F70D01010B05000382010100908FA613209C85D80CF21744AF030ACE71D613F6507142D9B740AC24DFFF8037940BD7D471FAB3F428C7B487CB62852AB23731F14E39105DF04E0C147A98055E9F72883D7CC235648FBBDA3418678FBD4D583152434FB4DC08D077B770311115F9AD8194BB705731FE1D07BA0E35646F046CD82DEE5273A212B951D1507735EBC68CA981736DBF4FD0D7C34D6C48277D2ADE2B6321AF137DC319917044C606D2D940F5A26DF449610703F1621329097100B45E309872B63DBB5E149611F1E7F94E65279D022F5902412AC26DE220E770EEAB7862B8B71F1ABC7DE218686996396BC3045A2D9A7C87EC94523FCEC231EA18259E6A692CE446A9FE7B4CA4E4049B";
            //File.WriteAllBytes(@"C:\Users\lostromb\Desktop\prod-office365searchservice-credsmart.crt", global::Durandal.Common.Utils.BinaryHelpers.FromHexString(cert));

            //TinyHttpServer().Await();

            //StringBuilder s = new StringBuilder(16);
            //Console.WriteLine(GetInternalArrayStats(s));
            //s.Capacity = 80000;
            //Console.WriteLine(GetInternalArrayStats(s));

            //ParallelismTest().Await();
            //BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Benchmarks));

            //string a = "Test";
            //string b = "teST";
            //string c = "Text";
            //Console.WriteLine(WhateverBenchmarks.StringEqualsLatinCaseInsensitive(a.AsSpan(), b.AsSpan()));
            //Console.WriteLine(WhateverBenchmarks.StringEqualsLatinCaseInsensitive(a.AsSpan(), c.AsSpan()));
            //Console.WriteLine(WhateverBenchmarks.StringEqualsLatinCaseInsensitive(a.AsSpan(), c.AsSpan()));
            //Console.WriteLine(WhateverBenchmarks.StringEqualsLatinCaseInsensitive(b.AsSpan(), c.AsSpan()));

            //string[] lines = File.ReadAllLines(@"C:\Users\lostromb\Desktop\cert.txt");
            //List<byte> bytes = new List<byte>();
            //foreach (string line in lines)
            //{
            //    byte b;
            //    if (byte.TryParse(line, out b))
            //    {
            //        bytes.Add(b);
            //    }
            //}

            //File.WriteAllBytes(@"C:\Users\lostromb\Desktop\Office365SearchService-devbox.cer", bytes.ToArray());
            //X509Certificate2 cert = CertificateJunk.GenCertificate4("not.a.valid.certificate");
            //byte[] bytes = cert.Export(X509ContentType.Cert);
            //Console.WriteLine(Convert.ToBase64String(bytes));
            //return;

            //PerfCounterTest().Await();
            //var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(InterningBenchmarks));
            //TestMicrophoneEcho().Await();
            //TestHttp2Client().Await();
            //TestKestrelServer().Await();

            //MetricCollector metrics = new MetricCollector(new ConsoleLogger("Metrics"), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
            //metrics.AddMetricOutput(new ConsoleMetricOutput());
            //Task server = TestHttp2Server(NullMetricCollector.Singleton);
            //Task client = TestHttp2LocalClient(NullMetricCollector.Singleton);
            //server.Await();
            //client.Await();

            //TestMetricsThreadSafe().Await();

            //TestHttp2ServerAsProxy(NullMetricCollector.Singleton).Await();
            //H2MITM().Await();
            //SocketServerLoad().Await();

            //int NumInputs = 1000;
            //int InputLength = 20;
            //IRandom random = new FastRandom(54322);
            //List<string> inputs = new List<string>();
            //StringBuilder builder = new StringBuilder();
            //for (int c = 0; c < NumInputs; c++)
            //{
            //    int length = random.NextInt(1, InputLength);
            //    while (builder.Length < length)
            //    {
            //        builder.Append('a' + random.NextInt(0, 26));
            //    }

            //    inputs.Add(builder.ToString());
            //    builder.Clear();
            //}

            //BinaryTreeIndex binaryIndex = new BinaryTreeIndex();
            //for (int c = 0; c < NumInputs; c++)
            //{
            //    binaryIndex.Upsert(MemoryMarshal.Cast<char, byte>(inputs[c].AsSpan()));
            //}

            //while (true)
            //{
            //    int ordinal;
            //    for (int idx = 0; idx < NumInputs; idx++)
            //    {
            //        binaryIndex.TryGet(MemoryMarshal.Cast<char, byte>(inputs[idx].AsSpan()), out ordinal);
            //    }
            //}
        }

        public static (long totalSize, long longestArray) GetInternalArrayStats(object o)
        {
            return GetInternalArrayStats(o, new HashSet<object>());
        }

        private static (long totalSize, long longestArray) GetInternalArrayStats(object o, ISet<object> seenObjs)
        {
            if (o is null || seenObjs.Contains(o))
            {
                return (0, 0);
            }

            seenObjs.Add(o);
            long totalArraySize = 0;
            long longestArray = 0;
            foreach (var member in o.GetType().GetRuntimeFields())
            {
                if (member.FieldType.IsValueType)
                {
                    continue;
                }

                object memberVal = member.GetValue(o);
                if (memberVal is null)
                {
                    continue;
                }

                if (memberVal is System.Array array && member.FieldType.HasElementType)
                {
                    if (array.Length == 0)
                    {
                        seenObjs.Add(memberVal);
                        continue;
                    }

                    int elementSize = 0;
                    if (memberVal is byte[] || memberVal is sbyte[])
                    {
                        elementSize = 1;
                    }
                    else if (memberVal is char[] || memberVal is short[] || memberVal is ushort[])
                    {
                        elementSize = 2;
                    }
                    else if (memberVal is int[] || memberVal is uint[])
                    {
                        elementSize = 4;
                    }
                    else if (memberVal is long[] || memberVal is ulong[])
                    {
                        elementSize = 8;
                    }

                    if (elementSize != 0)
                    {
                        long thisArraySize = array.LongLength * elementSize;
                        totalArraySize += thisArraySize;
                        longestArray = Math.Max(longestArray, thisArraySize);
                    }
                    else
                    {
                        // It's presumably an array of some nonprimitive type so iterate over its members
                        foreach (var obj in array)
                        {
                            (long subTotalSize, long subLongestArray) = GetInternalArrayStats(memberVal, seenObjs);
                            totalArraySize += subTotalSize;
                            longestArray = Math.Max(longestArray, subLongestArray);
                        }
                    }

                    seenObjs.Add(memberVal);
                }
                else
                {
                    // It's a reference type but not an array; recurse
                    (long subTotalSize, long subLongestArray) = GetInternalArrayStats(memberVal, seenObjs);
                    totalArraySize += subTotalSize;
                    longestArray = Math.Max(longestArray, subLongestArray);
                }
            }

            return (totalArraySize, longestArray);
        }

        private static void RunInternalizerLoop()
        {
            int NumValues = 400;
            int ValueMaxLength = 20;
            IRandom random = new FastRandom(7777);
            StringBuilder builder = new StringBuilder();
            HashSet<string> existingStrings = new HashSet<string>();
            List<KeyValuePair<int, string>> inputs = new List<KeyValuePair<int, string>>();
            List<KeyValuePair<int, string>> miss_inputs = new List<KeyValuePair<int, string>>();
            List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> inputs2 = new List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>();
            List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>> inputs3 = new List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>>();
            while (existingStrings.Count < NumValues)
            {
                int valueLength = random.NextInt(1, ValueMaxLength);
                while (builder.Length < valueLength)
                {
                    builder.Append((char)('a' + random.NextInt(0, 26)));
                }

                string s = builder.ToString();
                if (!existingStrings.Contains(s))
                {
                    inputs.Add(new KeyValuePair<int, string>(existingStrings.Count, s));
                    inputs2.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                        new InternedKey<ReadOnlyMemory<char>>(existingStrings.Count),
                        s.AsMemory()));
                    inputs3.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>(
                        new InternedKey<ReadOnlyMemory<char>>(existingStrings.Count),
                        s));
                    existingStrings.Add(s);
                }

                builder.Clear();
            }

            // try to randomize the input list so return value ordinals are not predictable
            inputs.Sort((a, b) => a.Value.GetHashCode() - b.Value.GetHashCode());

            SealedInternalizer_StringIgnoreCase_Linear char_linear = new SealedInternalizer_StringIgnoreCase_Linear(inputs2);
            SealedInternalizer_StringIgnoreCase_Linear char_linear_opttree = new SealedInternalizer_StringIgnoreCase_Linear(inputs3);

            while (miss_inputs.Count < NumValues)
            {
                int valueLength = random.NextInt(1, ValueMaxLength);
                while (builder.Length < valueLength)
                {
                    builder.Append((char)('a' + random.NextInt(0, 26)));
                }

                string s = builder.ToString();
                miss_inputs.Add(new KeyValuePair<int, string>(miss_inputs.Count, s));
                builder.Clear();
            }

            InternedKey<ReadOnlyMemory<char>> ordinal;
            MovingAverage counterA = new MovingAverage(1000, 0.0);
            MovingAverage counterB = new MovingAverage(1000, 0.0);
            ValueStopwatch profileTimer = ValueStopwatch.StartNew();
            //while (true)
            //{
            //    foreach (var input in inputs)
            //    {
            //        char_linear.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
            //    }
            //    counter.Increment(inputs.Count);

            //    foreach (var input in miss_inputs)
            //    {
            //        char_linear.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
            //    }
            //    counter.Increment(miss_inputs.Count);

            //    if (updateTimer.Elapsed > TimeSpan.FromMilliseconds(200))
            //    {
            //        Console.Write("\r" + counter.Rate + "   ");
            //        updateTimer.Restart();
            //    }
            //}

            Stopwatch reportingTimer = Stopwatch.StartNew();
            while (true)
            {
                for (int l = 0; l < 10; l++)
                {
                    profileTimer.Restart();
                    for (int c = 0; c < 100; c++)
                    {
                        foreach (var input in inputs)
                        {
                            char_linear.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
                        }
                        foreach (var input in miss_inputs)
                        {
                            char_linear.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
                        }
                    }
                    profileTimer.Stop();
                    counterA.Add(profileTimer.ElapsedMillisecondsPrecise());

                    profileTimer.Restart();
                    for (int c = 0; c < 100; c++)
                    {
                        foreach (var input in inputs)
                        {
                            char_linear_opttree.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
                        }
                        foreach (var input in miss_inputs)
                        {
                            char_linear_opttree.TryGetInternalizedKey(input.Value.AsSpan(), out ordinal);
                        }
                    }
                    profileTimer.Stop();
                    counterB.Add(profileTimer.ElapsedMillisecondsPrecise());
                }

                if (reportingTimer.Elapsed > TimeSpan.FromMilliseconds(100))
                {
                    Console.Write("\rOptimized time compared to baseline: {0:F3}", counterB.Average / counterA.Average);
                    reportingTimer.Restart();
                }
            }
        }

        public static async Task SplitAudio()
        {
            ILogger logger = new ConsoleLogger();
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            using (IAudioGraph inputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph outputGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FileStream fileIn = new FileStream(@"C:\Code\Durandal\Data\Point51.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder audioIn = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(inputGraph), "WaveIn"))
            //using (FfmpegAudioSampleSource audioIn = await FfmpegAudioSampleSource.Create(new WeakPointer<IAudioGraph>(inputGraph), "FFmpeg", logger.Clone("FFmpeg"), new FileInfo(@"S:\Unsorted\complete\JJ audio.mkv")))
            {
                //await wavDecoder.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                AudioSampleFormat surroundFormat = audioIn.OutputFormat;
                AudioSampleFormat splitFormat = AudioSampleFormat.Stereo(surroundFormat.SampleRateHz);
                using (PassthroughAudioPipe inputMotivator = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(inputGraph), surroundFormat, "InputMotivator"))
                using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(inputGraph), surroundFormat, "Splitter"))
                using (FileStream fileOut = new FileStream(@"C:\Code\AudiobookRecorder\Mixed.opus", FileMode.Create, FileAccess.Write))
                //using (RiffWaveEncoder wavOut = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(outputGraph), splitFormat, "WaveOut", logger))
                using (OggOpusEncoder opusOut = new OggOpusEncoder(new WeakPointer<IAudioGraph>(outputGraph), splitFormat, "OpusOut", logger.Clone("Opus"), 10, 112))
                using (ChannelMixer centerMonoToStereo = new ChannelMixer(new WeakPointer<IAudioGraph>(inputGraph), splitFormat.SampleRateHz, MultiChannelMapping.Monaural, MultiChannelMapping.Stereo_L_R, "CenterSpread"))
                using (LowShelfFilter bassReduce = new LowShelfFilter(new WeakPointer<IAudioGraph>(inputGraph), splitFormat, "Filter", 150, -10.0f))
                using (VolumeFilter centerVolume = new VolumeFilter(new WeakPointer<IAudioGraph>(inputGraph), splitFormat, "CenterVolume"))
                using (PushPullBuffer pushPull1 = new PushPullBuffer(new WeakPointer<IAudioGraph>(inputGraph), new WeakPointer<IAudioGraph>(outputGraph), splitFormat, "PP1", TimeSpan.FromSeconds(5)))
                using (PushPullBuffer pushPull2 = new PushPullBuffer(new WeakPointer<IAudioGraph>(inputGraph), new WeakPointer<IAudioGraph>(outputGraph), splitFormat, "PP2", TimeSpan.FromSeconds(5)))
                using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(outputGraph), splitFormat, "Mixer", readForever: false, logger.Clone("Mixer")))
                using (PassthroughAudioPipe outputMotivator = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(outputGraph), splitFormat, "OutputMotivator"))
                using (VolumeFilter finalVolume = new VolumeFilter(new WeakPointer<IAudioGraph>(outputGraph), splitFormat, "FinalVolume"))
                {
                    await opusOut.Initialize(fileOut, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                    audioIn.ConnectOutput(inputMotivator);
                    inputMotivator.ConnectOutput(splitter);
                    splitter.AddOutput(bassReduce, surroundFormat.GetChannelIndexForSpeaker(SpeakerLocation.FrontLeft), surroundFormat.GetChannelIndexForSpeaker(SpeakerLocation.FrontRight));
                    splitter.AddOutput(centerMonoToStereo, surroundFormat.GetChannelIndexForSpeaker(SpeakerLocation.FrontCenter));
                    bassReduce.ConnectOutput(pushPull1);
                    centerMonoToStereo.ConnectOutput(centerVolume);
                    centerVolume.ConnectOutput(pushPull2);
                    mixer.AddInput(pushPull1);
                    mixer.AddInput(pushPull2);
                    mixer.ConnectOutput(outputMotivator);
                    outputMotivator.ConnectOutput(finalVolume);
                    finalVolume.ConnectOutput(opusOut);

                    centerVolume.VolumeLinear = 0.5f;
                    finalVolume.VolumeLinear = 3.0f;

                    TimeSpan encodeTime = TimeSpan.Zero;
                    while (!audioIn.PlaybackFinished)
                    {
                        long samplesFromInput = await inputMotivator.DriveGraph(TimeSpan.FromMilliseconds(100), CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        encodeTime += AudioMath.ConvertSamplesPerChannelToTimeSpan(splitFormat.SampleRateHz, samplesFromInput);
                        await outputMotivator.DriveGraph(samplesFromInput, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Console.Write("\r" + encodeTime.PrintTimeSpan());
                    }
                }
            }
        }

        public static async Task SplitAudio2()
        {
            ILogger logger = new ConsoleLogger();
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FileStream fileIn = new FileStream(@"C:\Code\Durandal\Data\Point51.wav", FileMode.Open, FileAccess.Read))
            using (RiffWaveDecoder audioIn = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), "WaveIn"))
            {
                await audioIn.Initialize(fileIn, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                AudioSampleFormat surroundFormat = audioIn.OutputFormat;
                AudioSampleFormat vorbisSurroundFormat = new AudioSampleFormat(surroundFormat.SampleRateHz, MultiChannelMapping.Surround_5_1ch_Vorbis_Layout);
                using (ChannelMixer mixer = new ChannelMixer(new WeakPointer<IAudioGraph>(graph), surroundFormat.SampleRateHz, surroundFormat.ChannelMapping, vorbisSurroundFormat.ChannelMapping, "ChannelMixer"))
                using (FileStream fileOut = new FileStream(@"C:\Code\Durandal\Data\Point51.raw", FileMode.Create, FileAccess.Write))
                using (RawPcmEncoder wavOut = new RawPcmEncoder(new WeakPointer<IAudioGraph>(graph), vorbisSurroundFormat, "WaveOut"))
                {
                    await wavOut.Initialize(fileOut, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    audioIn.ConnectOutput(mixer);
                    mixer.ConnectOutput(wavOut);
                    await audioIn.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }
            }
        }

        private static string CreateJsonHedgemaze(IRandom rand, int tokenLimit, int depthLimit = 6)
        {
            StringBuilder builder = new StringBuilder(tokenLimit * 50);
            CreateJsonHedgemaze(builder, rand, tokenLimit, depthLimit);
            return builder.ToString();
        }

        private static void CreateJsonHedgemaze(StringBuilder builder, IRandom rand, int tokenLimit, int depthLimit = 6)
        {
            int localLimit = tokenLimit;
            CreateJsonHedgemaze(builder, rand, ref localLimit, 0, depthLimit);
        }

        private static void CreateJsonHedgemaze(StringBuilder builder, IRandom rand, ref int tokenLimit, int depth, int depthLimit)
        {
            HashSet<string> keysUsedAtThisLevel = new HashSet<string>();
            builder.Append(' ', depth);
            builder.Append("{\r\n");
            int numKeysToAttempt = rand.NextInt(3, 20);
            if (depth == 0)
            {
                numKeysToAttempt = int.MaxValue;
            }

            depth += 1;
            for (int keyIdx = 0; keyIdx < numKeysToAttempt && tokenLimit >= 0; keyIdx++)
            {
                string key = rand.NextInt().ToString();
                while (keysUsedAtThisLevel.Contains(key))
                {
                    key = rand.NextInt().ToString();
                }

                keysUsedAtThisLevel.Add(key);

                // Write the property name
                builder.Append(' ', depth);
                builder.Append("\"");
                builder.Append(key);
                builder.Append("\":");
                tokenLimit--;

                int typeOfValue = rand.NextInt(0, 11); // upper limit is arbitrary, just depends on how many string values we want to skew towards
                if (depth >= depthLimit)
                {
                    typeOfValue = -1; // force string value if we're at depth limit
                }

                // Now the value
                switch (typeOfValue)
                {
                    case 0:
                    case 1:
                    case 2:
                        // Array of something
                        int numValuesToAttempt = rand.NextInt(0, 10);
                        builder.Append("\r\n");
                        builder.Append(' ', depth);
                        builder.Append("[\r\n");
                        depth += 1;
                        for (int arrayIdx = 0; arrayIdx < numValuesToAttempt && tokenLimit >= 0; arrayIdx++)
                        {
                            if (typeOfValue == 0)
                            {
                                // Array of integers
                                builder.Append(' ', depth);
                                builder.Append(rand.NextInt(0, 1000));
                                tokenLimit--;
                            }
                            else if (typeOfValue == 1)
                            {
                                // Array of strings
                                builder.Append(' ', depth);
                                builder.Append("\"");
                                builder.Append(rand.NextInt().ToString());
                                builder.Append("\"");
                                tokenLimit--;
                            }
                            else
                            {
                                // Array of objects
                                CreateJsonHedgemaze(builder, rand, ref tokenLimit, depth, depthLimit);
                            }

                            if (arrayIdx >= numValuesToAttempt || tokenLimit < 0)
                            {
                                builder.Append("\r\n");
                            }
                            else
                            {
                                builder.Append(",\r\n");
                            }
                        }
                        depth -= 1;
                        builder.Append(' ', depth);
                        builder.Append("]");
                        break;
                    case 3:
                        // Null value
                        builder.Append(" null");
                        tokenLimit--;
                        break;
                    case 4:
                        // Int value
                        builder.Append(" ");
                        builder.Append(rand.NextInt(1, 1000));
                        tokenLimit--;
                        break;
                    case 5:
                    case 6:
                    case 7:
                        // Nested single object
                        builder.Append("\r\n");
                        CreateJsonHedgemaze(builder, rand, ref tokenLimit, depth, depthLimit);
                        break;
                    default:
                        // string
                        builder.Append(" \"");
                        builder.Append(rand.NextInt().ToString());
                        builder.Append("\"");
                        tokenLimit--;
                        break;
                }

                // Omit the comma if we won't loop again...
                if (keyIdx >= numKeysToAttempt || tokenLimit < 0)
                    builder.Append("\r\n");
                else
                    builder.Append(",\r\n");
            }

            depth -= 1;
            builder.Append(' ', depth);
            builder.Append("}");
        }

        private static void DoSomething()
        {
            try
            {
                DoSomethingElse();
                BufferPool<byte>.Shred();
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException("An argument was out of range", e);
            }
        }

        private static void DoSomethingElse()
        {
            if (FastRandom.Shared.NextInt() > 0)
            {
                throw new ArgumentException("Args cannot be negative");
            }

            BufferPool<byte>.Shred();
        }

        private static async Task<int> DelayBy(int value)
        {
            await Task.Delay(value);
            return value;
        }

        private static async Task ParallelismTest()
        {
            TaskWhenAnyCollection<int> tc = new TaskWhenAnyCollection<int>(
                DelayBy(4000),
                DelayBy(1000),
                DelayBy(2000),
                DelayBy(5000),
                DelayBy(3000));
            for (int c = 0; c < tc.Count; c++)
            {
                Task<int> nextTask = await tc.WaitForNextFinishedTask();
                int value = await nextTask;
                Console.WriteLine(value);
            }
        }

        private static async Task TinyHttpServer()
        {
            ILogger logger = new ConsoleLogger();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            WeakPointer<IMetricCollector> metrics = NullMetricCollector.WeakSingleton;
            WeakPointer<IThreadPool> threadPool = new WeakPointer<IThreadPool>(new TaskThreadPool());
            logger.Log("Building HTTP server");
            using (RawTcpSocketServer socketServer = new RawTcpSocketServer(
                new ServerBindingInfo[] { new ServerBindingInfo("*", 11220) },
                logger.Clone("SocketServer"),
                realTime,
                metrics,
            DimensionSet.Empty,
                threadPool))
            using (SocketHttpServer httpServer = new SocketHttpServer(socketServer, logger.Clone("HttpServer"), FastRandom.Shared, metrics, DimensionSet.Empty))
            {
                httpServer.RegisterSubclass(new MessageHttpServer(logger.Clone("MessageController")));
                logger.Log("Starting HTTP server");
                httpServer.StartServer("MessageHttp", CancellationToken.None, realTime).Await();
                logger.Log("Running");

                while (httpServer.Running)
                {
                    await Task.Delay(1000);
                }
            }
        }

        private class MessageHttpServer : IHttpServerDelegate
        {
            private readonly ILogger _logger;

            public MessageHttpServer(ILogger logger)
            {
                _logger = logger.AssertNonNull(nameof(logger));
            }

            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                try
                {
                    if (string.Equals(context.HttpRequest.DecodedRequestFile, "/message"))
                    {
                        await context.WritePrimaryResponse(HttpResponse.OKResponse(), _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                    else
                    {
                        await context.WritePrimaryResponse(HttpResponse.NotFoundResponse(), _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e);
                    if (!context.PrimaryResponseStarted)
                    {
                        await context.WritePrimaryResponse(HttpResponse.ServerErrorResponse(e), _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
        }

        //private static async Task TestSpeechReco()
        //{
        //    ILogger logger = new ConsoleLogger("Prototype", LogLevel.All);
        //    NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
        //    NLPToolsCollection nlTools = new NLPToolsCollection();
        //    AudioSampleFormat format = AudioSampleFormat.Mono(16000);
        //    VoskSpeechRecognizerFactory recoFactory = new VoskSpeechRecognizerFactory(logger.Clone("VoskFactory"), nlTools, format.SampleRateHz);
        //    recoFactory.LoadLanguageModel(@"C:\Code\Durandal\Data\vosk\vosk-model-en-us-0.22-lgraph", LanguageCode.ENGLISH, LanguageCode.EN_US);

        //    IAudioDriver audioDeviceDriver = new WasapiDeviceDriver(logger.Clone("WasapiDriver"));
        //    StutterReportingInstrumentationDelegate stutterTracker = new StutterReportingInstrumentationDelegate(logger.Clone("MicGraphInstrumentation"));
        //    using (IAudioGraph micIsolatedGraph = new AudioGraph(AudioGraphCapabilities.Concurrent | AudioGraphCapabilities.Instrumented, stutterTracker.HandleInstrumentation))
        //    using (IAudioGraph micGraph = new AudioGraph(AudioGraphCapabilities.Concurrent | AudioGraphCapabilities.Instrumented, stutterTracker.HandleInstrumentation))
        //    using (IAudioGraph speakerGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
        //    using (IAudioCaptureDevice microphone = audioDeviceDriver.OpenCaptureDevice(null, micIsolatedGraph, format, "Microphone"))
        //    using (IAudioRenderDevice playback = audioDeviceDriver.OpenRenderDevice(null, speakerGraph, format, "Speakers"))
        //    using (AsyncAudioWriteBuffer writeBuffer = new AsyncAudioWriteBuffer(micIsolatedGraph, micGraph, format, "MicAsyncWriteBuffer", TimeSpan.FromMilliseconds(1000), logger.Clone("Buffer"), NullMetricCollector.WeakSingleton, DimensionSet.Empty))
        //    using (AudioSplitter micSplitter = new AudioSplitter(micGraph, format, "MicSplitter"))
        //    using (LinearMixer speakerMixer = new LinearMixer(speakerGraph, playback.InputFormat, "SpeakerMixer", readForever: true))
        //    {
        //        speakerMixer.ConnectOutput(playback);
        //        microphone.ConnectOutput(writeBuffer);
        //        writeBuffer.ConnectOutput(micSplitter);
        //        await playback.StartPlayback(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
        //        await microphone.StartCapture(DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

        //        while (true)
        //        {
        //            Console.WriteLine("Press enter to recognize");
        //            Console.ReadLine();

        //            using (ISpeechRecognizer recognizer = await recoFactory.CreateRecognitionStream(micGraph, "VoskReco", LanguageCode.EN_US, logger.Clone("QueryLogger"), CancellationToken.None, DefaultRealTimeProvider.Singleton))
        //            using (BucketAudioSampleTarget recording = new BucketAudioSampleTarget(micGraph, format, "SpeechCapture"))
        //            {
        //                micSplitter.AddOutput(recording);
        //                micSplitter.AddOutput(recognizer);
        //                recognizer.IntermediateResultEvent.Subscribe(HandleSpeechRecoEvent);
        //                Console.WriteLine("Start talking");
        //                await Task.Delay(5000);
        //                Console.WriteLine("Done talking");
        //                await writeBuffer.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
        //                recording.DisconnectInput();
        //                recognizer.DisconnectInput();
        //                AudioSample capturedSample = recording.GetAllAudio();

        //                // Play back user's voice
        //                speakerMixer.AddInput(new FixedAudioSampleSource(speakerGraph, capturedSample, "PlaybackSample"), takeOwnership: true);

        //                var recoResult = await recognizer.FinishUnderstandSpeech(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
        //                Console.WriteLine(JsonConvert.SerializeObject(recoResult));
        //                recognizer.IntermediateResultEvent.Unsubscribe(HandleSpeechRecoEvent);
        //            }
        //        }
        //    }
        //}

        public static Task HandleSpeechRecoEvent(object source, TextEventArgs args, IRealTimeProvider realTime)
        {
            Console.WriteLine("Intermediate result: " + args.Text);
            return DurandalTaskExtensions.NoOpTask;
        }

        private static async Task RunEventsAsync()
        {
            AsyncEvent<TextEventArgs> myEvent = new AsyncEvent<TextEventArgs>();
            List<MockEventListener> recorders = new List<MockEventListener>();
            RateLimiter limiter = new RateLimiter(1000, 100);

            for (int c = 0; c < 10; c++)
            {
                MockEventListener recorder = new MockEventListener();
                recorders.Add(recorder);
                myEvent.Subscribe(recorder.HandleEventAsync);
            }

            TextEventArgs eventArgs = new TextEventArgs("Test");

            while (true)
            {
                myEvent.FireInBackground(null, eventArgs, NullLogger.Singleton, DefaultRealTimeProvider.Singleton);
                await limiter.LimitAsync(DefaultRealTimeProvider.Singleton, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private class MockEventListener
        {
            public Task HandleEventAsync(object source, TextEventArgs args, IRealTimeProvider realTime)
            {
                return DurandalTaskExtensions.NoOpTask;
            }
        }


        public static void ManualLoggingTest()
        {
            IRSADelegates rsa = new StandardRSADelegates();
            RsaStringEncrypterPii encrypter = new RsaStringEncrypterPii(
                rsa,
                new SystemAESDelegates(),
                new FastRandom(),
                DefaultRealTimeProvider.Singleton,
                DataPrivacyClassification.PrivateContent,
                rsa.GenerateRSAKey(2048).GetPublicKey()
                );

            ILogger console = new ConsoleLogger("Console", LogLevel.All);
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());

            MetricCollector metricCollector = new MetricCollector(console.Clone("Metrics"), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
            metricCollector.AddMetricSource(new NetCorePerfCounterReporter(DimensionSet.Empty));
            metricCollector.AddMetricOutput(new ConsoleMetricOutput());
            metricCollector.AddMetricOutput(new FileMetricOutput(console.Clone("MetricsOut"), "Prototype", ".\\metrics"));

            IFileSystem realFs = new RealFileSystem(console.Clone("FileLogger"));
            ZStandardFileSystemWrapper loggingFs = new ZStandardFileSystemWrapper(realFs, console.Clone("ZStdFS"), 10);
            loggingFs.AddBasePathToCompress(new VirtualPath("logs"));

            ILogger durandalLogger = new FileLogger(
                    fileSystem: loggingFs,
                    componentName: "Main",
                    logFilePrefix: Process.GetCurrentProcess().ProcessName,
                    maxFileSizeBytes: 1024 * 1024 * 100,
                    fileBufferSize: 32768,
                    logDirectory: new VirtualPath("logs"));

            //ILogger logger = new DetailedConsoleLogger();
            //durandalLogger = new PiiEncryptingLogger(durandalLogger, encrypter);

            RateLimiter limiter = new RateLimiter(10000);
            DirectoryInfo logDir = new DirectoryInfo(".\\logs");
            int iter = 0;
            while (true)
            {
                for (int c = 0; c < 1000000; c++)
                {
                    durandalLogger.Log("Here is a plain message");
                    durandalLogger.Log("Here is a plain error message", LogLevel.Err);
                    durandalLogger.Log("Here is a PII message", LogLevel.Std, privacyClass: DataPrivacyClassification.PrivateContent);
                    durandalLogger.Log("Here is a trace message", LogLevel.Std, traceId: Guid.NewGuid());
                    durandalLogger.Log("Here is a verbose message", LogLevel.Vrb);
                    durandalLogger.Log("Here is a\r\nmessage on two lines!");
                    durandalLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "This has been loop {0} of the iteration", iter++);

                    metricCollector.ReportInstant("Log messages / sec", DimensionSet.Empty, 7);
                    //limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                }

                // Periodically delete old files in the log directory
                foreach (var oldLogFile in logDir.EnumerateFiles().OrderByDescending((f) => f.CreationTimeUtc).Skip(1))
                {
                    oldLogFile.Delete();
                }
            }
        }

        //public static async Task DriveSpeakers()
        //{
        //    ILogger logger = new ConsoleLogger("Prototype", LogLevel.All);
        //    IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
        //    NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());

        //    NativePlatformUtils.PrepareNativeLibrary("z", logger);
        //    NativePlatformUtils.PrepareNativeLibrary("util", logger);

        //    IRandom rand = new FastRandom();
        //    AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
        //    IAudioDriver audioDeviceDriver = new SDL2DeviceDriver(logger.Clone("SDLAudio"));
        //    //IAudioDriver audioDeviceDriver = new BassDeviceDriver(logger.Clone("BassAudio"));

        //    //using (IHighPrecisionWaitProvider audioThreadTimer = new SpinwaitHighPrecisionWaitProvider(true))
        //    //using (Win32HighPrecisionWaitProvider audioThreadTimer = new Win32HighPrecisionWaitProvider())
        //    //using (IAudioGraph micGraph = new AudioGraph(AudioGraphCapabilities.Concurrent | AudioGraphCapabilities.StutterDetecting))
        //    using (IAudioGraph speakerGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
        //    //using (FileStream waveInStream = new FileStream(@"C:\Code\Durandal\Data\Point 6ch.wav", FileMode.Open, FileAccess.Read))
        //    //using (RiffWaveDecoder programAudio = new RiffWaveDecoder(speakerGraph, "RiffWaveIn"))
        //    using (FileStream opusInStream = new FileStream(@"Grow.opus", FileMode.Open, FileAccess.Read))
        //    using (OggOpusDecoder programAudio = new OggOpusDecoder(speakerGraph, "OggOpusIn", null, null))
        //    {
        //        await programAudio.Initialize(opusInStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

        //        //using (DTMFToneGenerator toneGenerator = new DTMFToneGenerator(speakerGraph, format, null))
        //        //using (FfmpegAudioSampleSource programAudio = await FfmpegAudioSampleSource.Create(speakerGraph, "ProgramAudio", logger.Clone("Ffmpeg"), new FileInfo(@"C:\Code\Durandal\Data\grow.opus")))
        //        //using (FfmpegAudioSampleSource programAudio = await FfmpegAudioSampleSource.Create(speakerGraph, "ProgramAudio", logger.Clone("Ffmpeg"), new FileInfo(@"S:\Movies & TV\Elemental.mkv")))
        //        //using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(speakerGraph, format, new FastRandom(), 0.0f, 0.5f))
        //        //using (OpenALAudioPlayer speakers = new OpenALAudioPlayer(speakerGraph, format, "Speakers", logger.Clone("Speakers"), new WeakPointer<IHighPrecisionWaitProvider>(audioThreadTimer)))
        //        using (AudioConformer conformer = new AudioConformer(speakerGraph, programAudio.OutputFormat, format, "Conformer", logger.Clone("Conformer"), AudioProcessingQuality.Balanced))
        //        using (IAudioRenderDevice speakers = audioDeviceDriver.OpenRenderDevice(null, speakerGraph, format, "Speakers"))
        //        {
        //            programAudio.ConnectOutput(conformer);
        //            conformer.ConnectOutput(speakers);

        //            Task st = speakers.StartPlayback(realTime);
        //            await st;

        //            while (true)
        //            {
        //                //if (toneGenerator.QueuedToneLength < TimeSpan.FromSeconds(1))
        //                //{
        //                //    toneGenerator.QueueTone(DTMFToneGenerator.DialTone.TONE_0 + FastRandom.Shared.NextInt(0, 16));
        //                //}

        //                await Task.Delay(100);
        //            }
        //        }
        //    }
        //}

        //public static async Task TranscodeAudio()
        //{
        //    ILogger logger = new ConsoleLogger("Prototype", LogLevel.All);
        //    NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());

        //    NativeFlacCodecFactory codecFactory = new NativeFlacCodecFactory(logger.Clone("Flac"));
        //    using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
        //    using (FileStream fileInStream = new FileStream(@"C:\Code\Durandal\Data\big arm.flac", FileMode.Open, FileAccess.Read))
        //    using (FileStream encodedOutStream = new FileStream(@"C:\Code\Durandal\Data\test.mp3", FileMode.Create, FileAccess.Write))
        //    //using (AudioDecoder decoder = codecFactory.CreateDecoder("flac", string.Empty, new WeakPointer<IAudioGraph>(graph), logger.Clone("Decoder"), "FlacDecoder"))
        //    using (AudioDecoder decoder = new MediaFoundationDecoder(logger.Clone("Decoder"), graph))
        //    {
        //        await decoder.Initialize(fileInStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

        //        //Dictionary<string, string> metadata = new Dictionary<string, string>();
        //        //metadata[FfmpegMetadataKey.TITLE] = "毒";

        //        //using (FfmpegAACEncoder encoder = await FfmpegAACEncoder.Create(
        //        //    new WeakPointer<IAudioGraph>(graph),
        //        //    decoder.OutputFormat,
        //        //    logger.Clone("Ffmpeg"),
        //        //    new FileInfo(@"C:\Code\Durandal\Data\Poison.m4a"),
        //        //    128,
        //        //    "FfmpegAac",
        //        //    metadata))
        //        //using (OggOpusEncoder encoder = new OggOpusEncoder(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, "OggOpusOut", bitrateKbps: 256))
        //        using (MediaFoundationMp3Encoder encoder = new MediaFoundationMp3Encoder(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, logger))
        //        {
        //            await encoder.Initialize(encodedOutStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
        //            decoder.ConnectOutput(encoder);
        //            await decoder.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
        //            await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
        //        }
        //    }
        //}

        //public static async Task EchoMicrophone()
        //{
        //    ILogger logger = new ConsoleLogger("Prototype", LogLevel.All);
        //    IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;

        //    IRandom rand = new FastRandom();
        //    AudioSampleFormat desiredMicFormat = AudioSampleFormat.Stereo(48000);
        //    AudioSampleFormat speakerFormat = AudioSampleFormat.Stereo(48000);

        //    IAudioDriver audioDeviceDriver = new WasapiDeviceDriver(logger.Clone("AudioDriver"));

        //    Console.WriteLine("Capture devices:");
        //    foreach (var device in audioDeviceDriver.ListCaptureDevices())
        //    {
        //        Console.WriteLine(device.Id + ": " + device.DeviceFriendlyName);
        //    }

        //    Console.WriteLine("Render devices:");
        //    foreach (var device in audioDeviceDriver.ListRenderDevices())
        //    {
        //        Console.WriteLine(device.Id + ": " + device.DeviceFriendlyName);
        //    }

        //    using (Win32HighPrecisionWaitProvider audioThreadTimer = new Win32HighPrecisionWaitProvider())
        //    //using (IHighPrecisionWaitProvider audioThreadTimer = new SpinwaitHighPrecisionWaitProvider(true))
        //    using (IAudioGraph micGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
        //    using (IAudioGraph speakerGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
        //    //using (WasapiMicrophone microphone = new WasapiMicrophone(micGraph, null, logger.Clone("Microphone")))
        //    //using (OpenALMicrophone microphone = new OpenALMicrophone(micGraph, desiredMicFormat, "Microphone", logger.Clone("Microphone"), new WeakPointer<IHighPrecisionWaitProvider>(audioThreadTimer)))
        //    using (IAudioCaptureDevice microphone = audioDeviceDriver.OpenCaptureDevice(WasapiDeviceDriver.DefaultLoopbackCaptureDevice, micGraph.AsWeakPointer(), desiredMicFormat, null))
        //    using (BucketAudioSampleTarget micBucket = new BucketAudioSampleTarget(micGraph.AsWeakPointer(), microphone.OutputFormat, null))
        //    using (LinearMixerAutoConforming outputMixer = new LinearMixerAutoConforming(speakerGraph.AsWeakPointer(), speakerFormat, null, true, logger.Clone("Mixer")))
        //    //using (WasapiPlayer speakers = new WasapiPlayer(speakerGraph, speakerFormat, null, logger.Clone("Speakers")))
        //    //using (OpenALAudioPlayer speakers = new OpenALAudioPlayer(speakerGraph, speakerFormat, "Speakers", logger.Clone("Speakers"), new WeakPointer<IHighPrecisionWaitProvider>(audioThreadTimer)))
        //    using (IAudioRenderDevice speakers = audioDeviceDriver.OpenRenderDevice(audioDeviceDriver.ResolveRenderDevice("NAudioWasapi:{0.0.0.00000000}.{7710dbae-c956-4243-a741-05f87fca6864}"), speakerGraph.AsWeakPointer(), speakerFormat, "Speakers"))
        //    {
        //        microphone.ConnectOutput(micBucket);

        //        outputMixer.ConnectOutput(speakers);

        //        while (true)
        //        {
        //            Console.WriteLine("Ready to record");
        //            Console.ReadLine();
        //            Console.WriteLine("Recording");
        //            await microphone.StartCapture(realTime);
        //            await Task.Delay(3000);
        //            await microphone.StopCapture();
        //            AudioSample sample = micBucket.GetAllAudio();
        //            micBucket.ClearBucket();
        //            //using (FileStream wavOut = new FileStream("test.wav", FileMode.Create, FileAccess.Write))
        //            //{
        //            //    await AudioHelpers.WriteWaveToStream(sample, wavOut);
        //            //}

        //            Console.WriteLine("Playing back");
        //            outputMixer.AddInput(new FixedAudioSampleSource(speakerGraph.AsWeakPointer(), sample, null), null, takeOwnership: true);
        //            await speakers.StartPlayback(realTime);
        //            await Task.Delay(3000);
        //            await speakers.StopPlayback();
        //        }
        //    }
        //}

        public static async Task SocketServerLoad()
        {
            ILogger logger = new ConsoleLogger();
            IThreadPool threadPool = new TaskThreadPool();
            ISocketServer socketServer = new RawTcpSocketServer(
                new ServerBindingInfo[]
                {
                    new ServerBindingInfo("*", 62299)
                },
                logger.Clone("SocketServer"),
                DefaultRealTimeProvider.Singleton,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                new WeakPointer<IThreadPool>(threadPool));

            IHttpServer httpServer = new SocketHttpServer(
                socketServer,
                logger.Clone("HttpServer"),
                new CryptographicRandom(),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty);

            BasicHttpDelegate httpHandler = new BasicHttpDelegate();

            httpServer.RegisterSubclass(httpHandler);

            await httpServer.StartServer("Yes", CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

            while (httpServer.Running)
            {
                await Task.Delay(1000);
            }
        }

        private class BasicHttpDelegate : IHttpServerDelegate
        {
            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse response;
                string realFilePath = "C:\\Code\\Durandal\\Data\\http" + context.HttpRequest.DecodedRequestFile.Replace('/', '\\');
                if (!File.Exists(realFilePath))
                {
                    response = HttpResponse.NotFoundResponse();
                    await context.WritePrimaryResponse(response, NullLogger.Singleton, cancelToken, realTime).ConfigureAwait(false);
                    return;
                }

                string mimeType = HttpHelpers.ResolveMimeType(context.HttpRequest.DecodedRequestFile);
                response = HttpResponse.OKResponse();
                FileStream responseFileStream = new FileStream(realFilePath, FileMode.Open, FileAccess.Read);
                response.SetContent(responseFileStream, mimeType);
                await context.WritePrimaryResponse(response, NullLogger.Singleton, cancelToken, realTime).ConfigureAwait(false);
                //response.ResponseHeaders["Content-Encoding"] = "zstd-custom";
            }
        }

        public static async Task H2MITM()
        {
            ILogger logger = new ConsoleLogger();
            logger.Log("Running in H2 packet sniffer mode");
            IThreadPool threadPool = new TaskThreadPool();
            ISocketServer socketServer = new RawTcpSocketServer(
                new ServerBindingInfo[]
                {
                    new ServerBindingInfo("*", 443, CertificateIdentifier.BySubjectName("nghttp2.org"), true)
                },
                logger.Clone("SocketServer"),
                DefaultRealTimeProvider.Singleton,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                new WeakPointer<IThreadPool>(threadPool));
            ISocketFactory socketFactory = new RawTcpSocketFactory(logger.Clone("SocketFactory"));

            H2InterceptorServer interceptor = new H2InterceptorServer(
                socketServer,
                socketFactory,
                new TcpConnectionConfiguration()
                {
                    DnsHostname = "139.162.123.134",
                    Port = 443,
                    UseTLS = true,
                    ReportHttp2Capability = true,
                    SslHostname = "nghttp2.org",
                },
                logger.Clone("H2Intercept"));

            await interceptor.StartServer("MITM", CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

            SocketHttpClient localTestClient = new SocketHttpClient(
                new WeakPointer<ISocketFactory>(socketFactory),
                new TcpConnectionConfiguration()
                {
                    DnsHostname = "127.0.0.1",
                    Port = 443,
                    UseTLS = true,
                    ReportHttp2Capability = true,
                    SslHostname = "nghttp2.org",
                },
                logger.Clone("BrowserClient"),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                Http2SessionManager.Default,
                new Http2SessionPreferences());

            await Task.Delay(1000);

            using (HttpResponse resp = await localTestClient.SendRequestAsync(HttpRequest.CreateOutgoing("/httpbin/flasgger_static/swagger-ui-bundle.js")))
            {
                await resp.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                await resp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            while (socketServer.Running)
            {
                await Task.Delay(1000);
            }
        }

        public static async Task TestMetricsThreadSafe()
        {
            ILogger logger = new ConsoleLogger();
            MetricCollector collector = new MetricCollector(logger.Clone("Collector"), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), percentileSampleSize: 10);
            collector.AddMetricOutput(new ConsoleMetricOutput());
            List<Task> tasks = new List<Task>();
            for (int c = 0; c < 16; c++)
            {
                tasks.Add(Task.Run(() =>
                {
                    IRandom rand = new FastRandom();
                    DimensionSet dimensions = new DimensionSet(new MetricDimension("Name", rand.NextInt(1, 5).ToString()));
                    while (true)
                    {
                        collector.ReportPercentile("TestCounter", dimensions, rand.NextDouble());
                    }
                }));
            }

            foreach (var task in tasks)
            {
                await task.ConfigureAwait(false);
            }
        }

        public static async Task TestHttp2Server(IMetricCollector metrics)
        {
            ILogger logger = new ConsoleLogger("Main");
            using (IThreadPool threadPool = new TaskThreadPool())
            using (ISocketServer socketServer = new RawTcpSocketServer(
                new ServerBindingInfo[]
                {
                    new ServerBindingInfo("localhost", 60000, CertificateIdentifier.BySubjectName("localhost"), supportHttp2: true)
                },
                logger.Clone("SocketServer"),
                DefaultRealTimeProvider.Singleton,
                new WeakPointer<IMetricCollector>(metrics),
                DimensionSet.Empty,
                new WeakPointer<IThreadPool>(threadPool)))
            using (SocketHttpServer h2Server = new SocketHttpServer(
                socketServer,
                logger.Clone("H2Server"),
                new CryptographicRandom(),
                new WeakPointer<IMetricCollector>(metrics),
                DimensionSet.Empty))
            {
                IHttpServerDelegate myImplementation = new H2TestServer(h2Server, logger);
                await h2Server.StartServer("TestServer", CancellationToken.None, DefaultRealTimeProvider.Singleton);

                while (h2Server.Running)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
            }
        }

        public static async Task TestHttp2ServerAsProxy(IMetricCollector metrics)
        {
            ILogger logger = new ConsoleLogger("Main");// new DetailedConsoleLogger("Main");
            logger.Log("Running in full proxy mode");
            using (ISocketFactory socketFactory = new RawTcpSocketFactory(logger.Clone("SocketFactory")))
            using (IThreadPool threadPool = new TaskThreadPool())
            using (ISocketServer socketServer = new RawTcpSocketServer(
                new ServerBindingInfo[]
                {
                    new ServerBindingInfo("*", 443, CertificateIdentifier.BySubjectName("nghttp2.org"), supportHttp2: true)
                },
                logger.Clone("SocketServer"),
                DefaultRealTimeProvider.Singleton,
                new WeakPointer<IMetricCollector>(metrics),
                DimensionSet.Empty,
                new WeakPointer<IThreadPool>(threadPool)))
            using (SocketHttpServer h2Server = new SocketHttpServer(
                socketServer,
                logger.Clone("H2Server"),
                new CryptographicRandom(),
                new WeakPointer<IMetricCollector>(metrics),
                DimensionSet.Empty))
            {
                IHttpServerDelegate myImplementation = new H2ProxyServer(
                    h2Server,
                    logger,
                    new WeakPointer<ISocketFactory>(socketFactory),
                    new WeakPointer<IMetricCollector>(metrics),
                    DimensionSet.Empty);
                await h2Server.StartServer("ProxyServer", CancellationToken.None, DefaultRealTimeProvider.Singleton);

                while (h2Server.Running)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
            }
        }

        public class H2ProxyServer : IHttpServerDelegate
        {
            private readonly ILogger _logger;
            private readonly IHttpClient _proxyClient;

            public H2ProxyServer(
                IHttpServer baseServer,
                ILogger logger,
                WeakPointer<ISocketFactory> socketFactory,
                WeakPointer<IMetricCollector> metrics,
                DimensionSet metricDimensions)
            {
                baseServer.RegisterSubclass(this);
                _logger = logger.AssertNonNull(nameof(logger));
                _proxyClient = new SocketHttpClient(
                    socketFactory,
                    new TcpConnectionConfiguration()
                    {
                        DnsHostname = "139.162.123.134",
                        Port = 443,
                        ReportHttp2Capability = false,
                        SslHostname = "nghttp2.org",
                        UseTLS = true
                    },
                    logger.Clone("ProxyClient"),
                    metrics,
                    metricDimensions,
                    Http2SessionManager.Default,
                    new Http2SessionPreferences());

                //_proxyClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;
            }

            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _logger.Log("Got an HTTP request " + context.HttpRequest.DecodedRequestFile);
                try
                {
                    context.HttpRequest.MakeProxied();
                    using (HttpResponse httpResponse = await _proxyClient.SendRequestAsync(context.HttpRequest).ConfigureAwait(false))
                    {
                        httpResponse.MakeProxied();
                        await context.WritePrimaryResponse(httpResponse, _logger.Clone("MyHttpResponse"), cancelToken, realTime).ConfigureAwait(false);
                    }
                }
                finally
                {
                    context.HttpRequest.Dispose();
                }
            }
        }

        public class H2TestServer : IHttpServerDelegate
        {
            private readonly ILogger _logger;
            private readonly byte[] _imageFile;
            private readonly byte[] _randomData;

            public H2TestServer(IHttpServer baseServer, ILogger logger)
            {
                baseServer.RegisterSubclass(this);
                _logger = logger.AssertNonNull(nameof(logger));
                _imageFile = System.IO.File.ReadAllBytes(@"C:\Code\Durandal\Data\image.jpg");
                _randomData = new byte[300000];
                new FastRandom().NextBytes(_randomData);
            }

            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                //_logger.Log("Got an HTTP request " + context.HttpRequest.DecodedRequestFile);
                //if (context.HttpRequest.GetParameters != null)
                //{
                //    foreach (var getParam in context.HttpRequest.GetParameters)
                //    {
                //        _logger.Log("&" + getParam.Key + "=" + getParam.Value);
                //    }
                //}

                //if (context.HttpRequest.RequestHeaders != null)
                //{
                //    foreach (var header in context.HttpRequest.RequestHeaders)
                //    {
                //        _logger.Log("\t" + header.Key + ": " + header.Value.FirstOrDefault());
                //    }
                //}

                if (string.Equals(context.HttpRequest.DecodedRequestFile, "/"))
                {
                    HttpResponse myResponse = HttpResponse.OKResponse();
                    myResponse.SetContent("<html><head><title>A web page!</title></head><body>Doctor Grant, the phones are working<br/><img src=\"/image.jpg\"></body></html>", "text/html");
                    if (context.SupportsServerPush)
                    {
                        context.PushPromise("GET", "/image.jpg", null, cancelToken, realTime);
                    }

                    await context.WritePrimaryResponse(myResponse, _logger.Clone("MyHttpResponse"), cancelToken, realTime).ConfigureAwait(false);
                }
                else if (string.Equals(context.HttpRequest.DecodedRequestFile, "/image.jpg"))
                {
                    HttpResponse myResponse = HttpResponse.OKResponse();
                    myResponse.SetContent(_imageFile, "image/jpeg");
                    await context.WritePrimaryResponse(myResponse, _logger.Clone("MyHttpResponse"), cancelToken, realTime).ConfigureAwait(false);
                }
                else if (string.Equals(context.HttpRequest.DecodedRequestFile, "/rand"))
                {
                    ArraySegment<byte> requestData = await context.HttpRequest.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                    HttpResponse myResponse = HttpResponse.OKResponse();
                    myResponse.SetContent(_randomData, HttpConstants.MIME_TYPE_OCTET_STREAM);
                    await context.WritePrimaryResponse(myResponse, _logger.Clone("MyHttpResponse"), cancelToken, realTime).ConfigureAwait(false);
                }
                else
                {
                    await context.WritePrimaryResponse(HttpResponse.NotFoundResponse(), _logger.Clone("MyHttpResponse"), cancelToken, realTime).ConfigureAwait(false);
                }
            }
        }

        public static async Task TestHttp2LocalClient(IMetricCollector metrics)
        {
            ILogger logger = new ConsoleLogger("Test");
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger.Clone("SocketFactory"), metrics, DimensionSet.Empty, ignoreCertErrors: true);

            TcpConnectionConfiguration connectionParams = new TcpConnectionConfiguration()
            {
                DnsHostname = "localhost",
                SslHostname = "localhost",
                Port = 60000,
                UseTLS = true,
                NoDelay = false,
                ReportHttp2Capability = true
            };

            List<Task> parallelTasks = new List<Task>();
            for (int thread = 0; thread < 8; thread++)
            {
                parallelTasks.Add(Task.Run(async () =>
                {
                    using (IHttpClient httpClient = new SocketHttpClient(
                        new WeakPointer<ISocketFactory>(socketFactory),
                        connectionParams,
                        logger.Clone("HttpClient"),
                        new WeakPointer<IMetricCollector>(metrics),
                        DimensionSet.Empty,
                        Http2SessionManager.Default,
                        new Http2SessionPreferences()))
                    {
                        RateLimiter rateLimiter = new RateLimiter(100, 100);
                        Stopwatch timer = new Stopwatch();
                        while (true)
                        {
                            timer.Restart();
                            try
                            {
                                //using (HttpResponse resp = await httpClient.SendRequestAsync(HttpRequest.CreateOutgoing("/", "GET")).ConfigureAwait(false))
                                //{
                                //    if (resp == null || resp.ResponseCode != 200)
                                //    {
                                //        metrics.ReportInstant("Request Errors", DimensionSet.Empty);
                                //    }
                                //    else
                                //    {
                                //        string content = await resp.ReadContentAsStringAsync(cancelToken, realTime);
                                //        //Console.WriteLine("Got HTTP response with length " + content.Length);
                                //        await resp.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                                //    }
                                //}
                                using (NetworkResponseInstrumented<HttpResponse> instrumentedResponse =
                                    await httpClient.SendInstrumentedRequestAsync(HttpRequest.CreateOutgoing("/", "GET")).ConfigureAwait(false))
                                {
                                    HttpResponse resp = instrumentedResponse.Response;
                                    if (resp == null || resp.ResponseCode != 200)
                                    {
                                        metrics.ReportInstant("Request Errors", DimensionSet.Empty);
                                    }
                                    else
                                    {
                                        string content = await resp.ReadContentAsStringAsync(cancelToken, realTime);
                                        //Console.WriteLine("Got HTTP response with length " + content.Length);
                                        await resp.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                metrics.ReportInstant("Request Errors", DimensionSet.Empty);
                            }

                            timer.Stop();
                            metrics.ReportPercentile("Request Time", DimensionSet.Empty, timer.ElapsedMillisecondsPrecise());
                            rateLimiter.Limit(realTime, cancelToken);
                        }
                    }
                }));

                await Task.Delay(TimeSpan.FromMilliseconds(1000));
            }

            foreach (Task t in parallelTasks)
            {
                await t.ConfigureAwait(false);
            }
        }

        public static async Task TestHttp2Client()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            RawTcpSocketFactory socketFactory = new RawTcpSocketFactory(logger.Clone("SocketFactory"));

            TcpConnectionConfiguration connectionParams = new TcpConnectionConfiguration()
            {
                DnsHostname = "139.162.123.134",
                SslHostname = "nghttp2.org",
                Port = 443,
                UseTLS = true,
                NoDelay = false,
                ReportHttp2Capability = true
            };

            using (IHttpClient httpClient = new SocketHttpClient(
                new WeakPointer<ISocketFactory>(socketFactory),
                connectionParams,
                logger.Clone("HttpClient"),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                Http2SessionManager.Default,
                new Http2SessionPreferences()))
            {
                for (int c = 0; c < 50; c++)
                {
                    using (HttpResponse resp = await httpClient.SendRequestAsync(HttpRequest.CreateOutgoing("/documentation/package_README.html", "GET")).ConfigureAwait(false))
                    {
                        string content = await resp.ReadContentAsStringAsync(cancelToken, realTime);
                        Console.WriteLine("Got HTTP response with length " + content.Length);
                        await resp.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }

                    //using (HttpResponse resp2 = await httpClient.SendRequestAsync(HttpRequest.CreateOutgoing("/stylesheets/screen.css", "GET")).ConfigureAwait(false))
                    //{
                    //    string content = await resp2.ReadContentAsStringAsync(cancelToken, realTime);
                    //    Console.WriteLine("Got CSS response with length " + content.Length);
                    //    await resp2.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    //}

                    await Task.Delay(5000);
                }
            }
        }

        //public static async Task TestMicrophoneEcho()
        //{
        //    ILogger logger = new ConsoleLogger();
        //    IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
        //    CancellationToken cancelToken = CancellationToken.None;
        //    AudioSampleFormat format = AudioSampleFormat.Mono(48000);

        //    IAudioDriver audioDeviceDriver = new WasapiDeviceDriver(logger.Clone("WasapiDriver"));
        //    using (IAudioGraph micGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
        //    using (IAudioGraph speakerGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
        //    using (IAudioRenderDevice speakers = audioDeviceDriver.OpenRenderDevice(null, speakerGraph, format, "Speakers"))
        //    using (IAudioCaptureDevice microphone = audioDeviceDriver.OpenCaptureDevice(null, micGraph, format, "Microphone"))
        //    using (LinearMixerAutoConforming speakerMixer = new LinearMixerAutoConforming(speakerGraph, format, "Mixer", true, logger.Clone("Mixer")))
        //    {
        //        speakerMixer.ConnectOutput(speakers);
        //        await speakers.StartPlayback(realTime);
        //        while (true)
        //        {
        //            Console.WriteLine("Press a key to record");
        //            Console.ReadKey();
        //            Console.WriteLine("Recording -- say something");
        //            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(micGraph, microphone.OutputFormat, "Bucket"))
        //            {
        //                microphone.ConnectOutput(bucket);
        //                await microphone.StartCapture(realTime);
        //                await Task.Delay(4000);
        //                await microphone.StopCapture();
        //                Console.WriteLine("Playing back");
        //                AudioSample sample = bucket.GetAllAudio();
        //                speakerMixer.AddInput(new FixedAudioSampleSource(speakerGraph, sample, "SamplePlayback"), channelToken: null, takeOwnership: true);
        //            }
        //        }
        //    }
        //}

        public static async Task TestKestrelServer()
        {
            ILogger logger = new ConsoleLogger("Main");
            ServerBindingInfo[] serverBindings = new ServerBindingInfo[]
                {
                    new ServerBindingInfo("*", 62291)
                };

            using (KestrelHttpServer httpServer = new KestrelHttpServer(
                serverBindings,
                logger.Clone("Server")))

            //using (IThreadPool threadPool = new TaskThreadPool())
            //using (ISocketServer socketServer = new RawTcpSocketServer(
            //    serverBindings,
            //    logger.Clone("SocketServer"),
            //    DefaultRealTimeProvider.Singleton,
            //    NullMetricCollector.WeakSingleton,
            //    DimensionSet.Empty,
            //    new WeakPointer<IThreadPool>(threadPool)))
            //using (SocketHttpServer httpServer = new SocketHttpServer(
            //    socketServer,
            //    logger.Clone("SocketHttpServer"),
            //    NullMetricCollector.WeakSingleton,
            //    DimensionSet.Empty))

            {
                IHttpServerDelegate myImplementation = new MyServerImplementation(httpServer, logger);
                await httpServer.StartServer("TestServer", CancellationToken.None, DefaultRealTimeProvider.Singleton);

                while (httpServer.Running)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
            }
        }

        public class MyServerImplementation : IHttpServerDelegate
        {
            private readonly ILogger _logger;

            public MyServerImplementation(IHttpServer baseServer, ILogger logger)
            {
                baseServer.RegisterSubclass(this);
                _logger = logger.AssertNonNull(nameof(logger));
            }

            public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _logger.Log("Got an HTTP request " + context.HttpRequest.DecodedRequestFile);
                if (string.Equals(context.HttpRequest.DecodedRequestFile, "/"))
                {
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(100), cancelToken).ConfigureAwait(false);
                    HttpResponse myResponse = HttpResponse.OKResponse();
                    myResponse.ResponseHeaders.Add("X-Shoutouts-To", "fairlight");
                    myResponse.SetContent("Doctor Grant, the phones are working");
                    await context.WritePrimaryResponse(myResponse, _logger.Clone("MyHttpResponse"), cancelToken, realTime).ConfigureAwait(false);
                }
                else if (string.Equals(context.HttpRequest.DecodedRequestFile, "/trailer"))
                {
                    HttpResponse myResponse = HttpResponse.OKResponse();
                    Stream responseStream = new MemoryStream(Encoding.UTF8.GetBytes("ContentContentContent"));
                    myResponse.SetContent(responseStream, HttpConstants.MIME_TYPE_UTF8_TEXT);
                    List<string> trailers = new List<string>();
                    trailers.Add("X-RenderTime");
                    await context.WritePrimaryResponse(
                        myResponse,
                        _logger.Clone("MyHttpResponse"),
                        cancelToken,
                        realTime,
                        trailers,
                        (string trailerName) => Task.FromResult("TrailerValue")).ConfigureAwait(false);
                }
                else
                {
                    await context.WritePrimaryResponse(HttpResponse.NotFoundResponse(), _logger.Clone("MyHttpResponse"), cancelToken, realTime).ConfigureAwait(false);
                }
            }
        }

        public static async Task PerfCounterTest()
        {
            ILogger coreLogger = new ConsoleLogger();
            DimensionSet dimensions = new DimensionSet(
                new MetricDimension("Runtime", ".Net6"));
            MetricCollector metrics = new MetricCollector(coreLogger.Clone("Metrics"), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            metrics.AddMetricOutput(new ConsoleMetricOutput());
            metrics.AddMetricSource(new NetCorePerfCounterReporter(dimensions));
            metrics.AddMetricSource(new WindowsPerfCounterReporter(coreLogger.Clone("WindowsMetrics"), dimensions, WindowsPerfCounterSet.BasicCurrentProcess));
            RateLimiter limiter = new RateLimiter(10, 10);
            await DurandalTaskExtensions.NoOpTask;
            while (true)
            {
                limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                Task.Run(() =>
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    while (timer.ElapsedMilliseconds < 200)
                    {
                        Math.Sin(timer.ElapsedTicks);
                    }

                    timer.Stop();
                }).Forget(coreLogger);

                Task.Run(() =>
                {
                    Thread.Sleep(200);
                }).Forget(coreLogger);

                Task.Run(() =>
                {
                    Thread.Sleep(200);
                    throw new Exception("Blah!");
                }).Forget(NullLogger.Singleton);
            }
        }
    }
}
