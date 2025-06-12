
namespace Durandal.Extensions.NativeAudio.Components
{
    using Durandal.Common.Audio;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using Durandal.Common.IO;
    using Durandal.Common.Time;
    using Durandal.Common.Tasks;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Durandal.Common.Logger;
    using System.Linq;
    using System.Runtime.InteropServices.ComTypes;
    using System.Text.RegularExpressions;
    using Durandal.Common.Audio.WebRtc;
    using System.Globalization;
    using Durandal.API;
    using System.Text;
    using System.Runtime.InteropServices;
    using Durandal.Common.Utils.NativePlatform;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Audio sample source which connects to ffmpeg.exe, installed separately on the current machine,
    /// to decode audio from almost any codec or container format.
    /// </summary>
    public class FfmpegAudioSampleSource : AbstractAudioSampleSource
    {
        // Max amount of bytes to read from stdout at a time.
        // Pipes typically have very small buffers (<4K bytes) so we have to take fairly small nibbles
        private const int PIPE_READ_INCREMENT = 2048;

        // Parser for output stream specifiers
        private static Regex FormatParser = new Regex("Audio: pcm_f32le, (\\d+) Hz, (.+?), flt");

        // Maps ffmpeg channel layout names to internal enums
        // based on https://trac.ffmpeg.org/wiki/AudioChannelManipulation#Listchannelnamesandstandardchannellayouts
        private static readonly IReadOnlyDictionary<string, MultiChannelMapping> FfmpegMappingNameDictionary = new Dictionary<string, MultiChannelMapping>(StringComparer.OrdinalIgnoreCase)
        {
            { "mono", MultiChannelMapping.Monaural },
            { "stereo", MultiChannelMapping.Stereo_L_R },
            { "quad", MultiChannelMapping.Quadraphonic },
            { "quad(side)", MultiChannelMapping.Quadraphonic_side },
            { "5.0", MultiChannelMapping.Surround_5ch },
            { "5.1", MultiChannelMapping.Surround_5_1ch },
            { "5.1(side)", MultiChannelMapping.Surround_5_1ch_side },
            { "6.1", MultiChannelMapping.Surround_6_1ch },
            { "7.1", MultiChannelMapping.Surround_7_1ch },
        };

        private readonly ILogger _logger;
        private readonly StringBuilder _stdErrMessageBuffer = new StringBuilder();
        private Stream _ffmpegStdOut = null; // Raw stdout stream from process
        private StreamReader _ffmpegStdErr = null; // Text reader for stderr
        private Process _ffmpegProcess = null; // Handle to background process
        private bool _endOfStream = false;
        private int _disposed = 0;

        /// <summary>
        /// Private constructor for async constructor pattern
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="logger"></param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        private FfmpegAudioSampleSource(WeakPointer<IAudioGraph> graph, ILogger logger, string nodeCustomName) : base(graph, nameof(FfmpegAudioSampleSource), nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
        }

        /// <summary>
        /// Constructs a new instance of <see cref="FfmpegAudioSampleSource"/>.
        /// </summary>
        /// <param name="graph">The audio graph that this component will be part of</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger for diagnostics</param>
        /// <param name="inputFileName">The name of the input file to pass to ffmpeg.exe</param>
        /// <param name="ffmpegPath">The fully qualified path name to ffmpeg.exe, if it is not registered on a locally accessible %path%</param>
        /// <returns>A newly created sample source</returns>
        public static async Task<FfmpegAudioSampleSource> Create(WeakPointer<IAudioGraph> graph, string nodeCustomName, ILogger logger, FileInfo inputFileName, string ffmpegPath = null)
        {
            FfmpegAudioSampleSource returnVal = new FfmpegAudioSampleSource(graph, logger, nodeCustomName);

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                OSAndArchitecture platform = NativePlatformUtils.GetCurrentPlatform(logger);
                switch (platform.OS)
                {
                    case PlatformOperatingSystem.Windows:
                        // TODO check that ffmpeg exe exists
                        ffmpegPath = "ffmpeg.exe";
                        break;
                    case PlatformOperatingSystem.Linux:
                        ffmpegPath = "ffmpeg";
                        break;
                    default:
                        throw new PlatformNotSupportedException($"Don't know how to run ffmpeg on OS {platform.OS}");
                }
            }

            await returnVal.StartDecoderProcess(ffmpegPath, inputFileName, logger).ConfigureAwait(false);
            return returnVal;
        }

        public override bool PlaybackFinished => _endOfStream;

        /// <summary>
        /// Advances this source by the specified maximum number of samples per channel, writing them to the output
        /// </summary>
        /// <param name="samplesPerChannelToWrite"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns>The number of samples per channel actually written to output</returns>
        public async Task<int> WriteSamplesToOutput(int samplesPerChannelToWrite, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await OutputGraph.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
            OutputGraph.BeginInstrumentedScope(realTime, NodeFullName);
            try
            {
                if (Output == null)
                {
                    return 0;
                }

                if (samplesPerChannelToWrite > 0)
                {
                    using (PooledBuffer<float> scratchBuf = BufferPool<float>.Rent(samplesPerChannelToWrite * OutputFormat.NumChannels))
                    {
                        int samplesPerChannelReadFromSource = await ReadAsyncInternal(scratchBuf.Buffer, 0, samplesPerChannelToWrite, cancelToken, realTime).ConfigureAwait(false);
                        if (samplesPerChannelReadFromSource > 0)
                        {
                            await Output.WriteAsync(
                                scratchBuf.Buffer,
                                0,
                                samplesPerChannelReadFromSource,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                        }

                        if (PlaybackFinished)
                        {
                            await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                        }

                        return samplesPerChannelReadFromSource;
                    }
                }
                else
                {
                    return 0;
                }
            }
            finally
            {
                OutputGraph.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, samplesPerChannelToWrite));
                OutputGraph.UnlockGraph();
            }
        }

        private void PurgeStdErrMessages()
        {
            // Purge any messages from stderr (such as stats or weird DTS warning messages) and log them to verbose console if applicable
            while (_ffmpegStdErr.Peek() >= 0)
            {
                char c = (char)_ffmpegStdErr.Read();
                if (c == '\r' || c == '\n')
                {
                    if (_stdErrMessageBuffer.Length > 0 && (_logger.ValidLogLevels & LogLevel.Vrb) != 0)
                    {
                        _logger.Log("[FFMPEG] " + _stdErrMessageBuffer.ToString(), LogLevel.Vrb);
                    }

                    _stdErrMessageBuffer.Clear();
                }
                else
                {
                    _stdErrMessageBuffer.Append(c);
                }
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(FfmpegAudioSampleSource));
            }

            if (_endOfStream)
            {
                return -1;
            }

            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset));
            if (numSamplesPerChannel <= 0) throw new ArgumentOutOfRangeException(nameof(numSamplesPerChannel));
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            using (PooledBuffer<byte> intermediateBuffer = BufferPool<byte>.Rent(numSamplesPerChannel * OutputFormat.NumChannels * sizeof(float)))
            {
                int bytesToRead = numSamplesPerChannel * OutputFormat.NumChannels * sizeof(float);
                int byteAlignment = OutputFormat.NumChannels * sizeof(float);
                int bytesActuallyRead = 0;
                while (!_endOfStream && bytesActuallyRead < bytesToRead)
                {
                    PurgeStdErrMessages();
                    int thisReadSize = await _ffmpegStdOut.ReadAsync(intermediateBuffer.Buffer, bytesActuallyRead, Math.Min(PIPE_READ_INCREMENT, bytesToRead - bytesActuallyRead), cancelToken).ConfigureAwait(false);

                    if (thisReadSize == 0)
                    {
                        // Reached end of stream.
                        _endOfStream = true;

                        // Discard any trucated bytes in the current sample
                        bytesActuallyRead -= bytesActuallyRead % byteAlignment;
                    }
                    else
                    {
                        bytesActuallyRead += thisReadSize;
                    }
                }

                int samplesActuallyRead = bytesActuallyRead / sizeof(float);
                int samplesPerChannelActuallyRead = samplesActuallyRead / OutputFormat.NumChannels;

                // Convert from raw bytes (32-bit little-endian float) to platform native float samples
                AudioMath.ConvertSamples_4BytesFloatLittleEndianToFloat(intermediateBuffer.Buffer, 0, buffer, bufferOffset, samplesActuallyRead);

                return samplesPerChannelActuallyRead;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    _ffmpegProcess?.Close();
                    _ffmpegProcess?.Dispose();
                    _ffmpegStdOut?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private async Task StartDecoderProcess(string ffmpegPath, FileInfo inputFileName, ILogger logger)
        {
            string ffmpegArgs = $"-hide_banner -xerror -nostdin -nostats -i \"{inputFileName.FullName}\" -map 0:a:0 -c:a pcm_f32le -map_metadata -1 -f f32le pipe:";

            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = ffmpegArgs,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _ffmpegProcess = Process.Start(processInfo);
            _ffmpegStdOut = _ffmpegProcess.StandardOutput.BaseStream;
            _ffmpegStdErr = _ffmpegProcess.StandardError;

            List<string> errorMessages = new List<string>();
            bool outputStreamCreated = false;
            bool outputStreamParsed = false;

            // Read input file properties which get written to stderr.
            // Normally this is the properties of the created output stream, but it could be error messages too
            while (!outputStreamParsed && !_ffmpegStdErr.EndOfStream)
            {
                // Valid output looks like this:
                // Output #0, f32le, to 'pipe:':
                //  Metadata:
                //    encoder         : Lavf59.37.100
                //  Stream #0:0(eng): Audio: pcm_f32le, 44100 Hz, stereo, f32, 1411 kb/s
                //    Metadata:
                //      encoder         : Lavc59.61.100 pcm_f32le
                string line = await _ffmpegStdErr.ReadLineAsync().ConfigureAwait(false);
                if ((logger.ValidLogLevels & LogLevel.Vrb) != 0)
                {
                    logger.Log("[FFMPEG] " + line, LogLevel.Vrb);
                }

                errorMessages.Add(line);

                if (!outputStreamCreated && line.Contains("Output #0, f32le"))
                {
                    outputStreamCreated = true;
                }

                if (outputStreamCreated && !outputStreamParsed && line.Contains("Stream #0:0") && line.Contains("Audio: pcm_f32le"))
                {
                    // Parse the output format so we can actually initialize this component in the graph.
                    OutputFormat = ParseFfmpegOutputAudioFormat(line);
                    PurgeStdErrMessages();
                    logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Parsed audio format for file \"{0}\": {1}", inputFileName.Name, OutputFormat);
                    outputStreamParsed = true;
                }
            }

            if (!outputStreamParsed)
            {
                // Analyze common ffmpeg error messages
                if (errorMessages.Any((s) => s.Contains("No such file or directory")))
                {
                    throw new FileNotFoundException("Could not load input audio file for decoding", inputFileName.FullName);
                }

                if (errorMessages.Any((s) => s.Contains("Stream map '0:a:0' matches no streams.")))
                {
                    throw new InvalidDataException($"Input file {inputFileName.FullName} contains no valid audio streams");
                }
                
                // If it's not a known error case, dump console output for diagnosis
                foreach (string message in errorMessages)
                {
                    logger.Log(message, LogLevel.Err);
                }

                throw new Exception("Could not initialize ffmpeg audio input, see log messages for full error details");
            }
        }

        /// <summary>
        /// Parses an audio stream specifier from ffmpeg console output into a structured audio format.
        /// </summary>
        /// <param name="formatString">And input string in the form of "Stream #0:0: Audio: pcm_f32le, 44100 Hz, stereo, flt, 1411 kb/s"</param>
        /// <returns>A parsed audio format</returns>
        private static AudioSampleFormat ParseFfmpegOutputAudioFormat(string formatString)
        {
            Match formatMatch = FormatParser.Match(formatString);
            if (!formatMatch.Success)
            {
                throw new FormatException($"Could not parse ffmpeg output stream specifier \"{formatString}\"");
            }

            int sampleRate = int.Parse(formatMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            string channelMappingString = formatMatch.Groups[2].Value;
            MultiChannelMapping mapping;
            if (!FfmpegMappingNameDictionary.TryGetValue(channelMappingString, out mapping))
            {
                throw new FormatException($"Could not parse Ffmpeg channel layout \"{channelMappingString}\"");
            }

            return new AudioSampleFormat(sampleRate, mapping);
        }
    }
}
