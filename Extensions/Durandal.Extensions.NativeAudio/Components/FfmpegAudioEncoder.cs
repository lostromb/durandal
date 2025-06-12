using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Extensions.NativeAudio.Components
{
    /// <summary>
    /// Abstract audio encoder backed by ffmpeg command line, used to convert raw samples to encoded container data.
    /// </summary>
    public abstract class FfmpegAudioEncoder : AbstractAudioSampleTarget
    {
        // Max amount of bytes to read from stdout at a time.
        // Pipes typically have very small buffers (<4K bytes) so we have to take fairly small nibbles
        private const int PIPE_READ_INCREMENT = 2048;

        private static readonly Regex METADATA_KEY_VALIDATOR = new Regex("^[a-zA-Z0-9_]+$");

        // Maps ffmpeg channel layout names to internal enums
        // based on https://trac.ffmpeg.org/wiki/AudioChannelManipulation#Listchannelnamesandstandardchannellayouts
        private static readonly IReadOnlyDictionary<MultiChannelMapping, string> FfmpegMappingNameDictionary = new Dictionary<MultiChannelMapping, string>()
        {
            { MultiChannelMapping.Monaural, "mono" },
            { MultiChannelMapping.Stereo_L_R, "stereo" },
            { MultiChannelMapping.Quadraphonic, "quad" },
            { MultiChannelMapping.Quadraphonic_side, "quad(side)" },
            { MultiChannelMapping.Surround_5ch, "5.0" },
            { MultiChannelMapping.Surround_5_1ch, "5.1" },
            { MultiChannelMapping.Surround_5_1ch_side, "5.1(side)" },
            { MultiChannelMapping.Surround_6_1ch, "6.1" },
            { MultiChannelMapping.Surround_7_1ch, "7.1" },
        };

        private readonly ILogger _logger;
        private readonly string _ffmpegPath;
        private Process _ffmpegProcess = null; // Handle to background process
        private readonly StringBuilder _stdErrMessageBuffer = new StringBuilder();
        private Stream _ffmpegStdIn = null; // Raw stdin stream to process
        private StreamReader _ffmpegStdErr = null; // Text reader for stderr
        private int _disposed = 0;

        protected FfmpegAudioEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputSampleFormat,
            ILogger logger,
            string ffmpegPath,
            string implementingTypeName,
            string nodeCustomName)
            : base(graph, implementingTypeName, nodeCustomName)
        {
            _ffmpegPath = ffmpegPath;
            
            if (string.IsNullOrEmpty(_ffmpegPath))
            {
                OSAndArchitecture platform = NativePlatformUtils.GetCurrentPlatform(logger);
                switch (platform.OS)
                {
                    case PlatformOperatingSystem.Windows:
                        // TODO check that ffmpeg exe exists
                        _ffmpegPath = "ffmpeg.exe";
                        break;
                    case PlatformOperatingSystem.Linux:
                        _ffmpegPath = "ffmpeg";
                        break;
                    default:
                        throw new PlatformNotSupportedException($"Don't know how to run ffmpeg on OS {platform.OS}");
                }
            }

            _logger = logger.AssertNonNull(nameof(logger));
            if (!FfmpegMappingNameDictionary.ContainsKey(inputSampleFormat.ChannelMapping))
            {
                throw new ArgumentException($"Channel mapping {inputSampleFormat.ChannelMapping} is not supported in ffmpeg", nameof(inputSampleFormat));
            }

            InputFormat = inputSampleFormat;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (numSamplesPerChannel > 0)
            {
                int writeSizeBytes = numSamplesPerChannel * InputFormat.NumChannels * sizeof(float);
                using (PooledBuffer<byte> scratchBuf = BufferPool<byte>.Rent(writeSizeBytes))
                {
                    AudioMath.ConvertSamples_FloatTo4BytesFloatLittleEndian(buffer, bufferOffset, scratchBuf.Buffer, 0, numSamplesPerChannel * InputFormat.NumChannels);
                    await _ffmpegStdIn.WriteAsync(
                        scratchBuf.Buffer,
                        0,
                        writeSizeBytes,
                        cancelToken).ConfigureAwait(false);
                    PurgeStdErrMessages();
                }
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
                    _ffmpegStdIn.Close();
                    _ffmpegProcess.WaitForExit(); // BUGBUG infinite loop
                    _ffmpegProcess?.Close();
                    _ffmpegProcess?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// The ffmpeg command line parameters which control the output encoding.
        /// Must include at least a "-c:a ____" declaration, potentially with other options.
        /// For example, "-c:a libopus -b:a 112K -ac 1"
        /// </summary>
        protected abstract string OutputEncoderParameters { get; }

        /// <summary>
        /// Dictionary of metadata key-value pairs used when writing out the file.
        /// </summary>
        public abstract IReadOnlyDictionary<string, string> AudioMetadata { get; }

        protected async Task StartEncoderProcess(FileInfo outputFile, bool overwriteExistingFile)
        {
            // Build the metadata collection if present
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                string channelMapString = FfmpegMappingNameDictionary[InputFormat.ChannelMapping];
                pooledSb.Builder.Append("-hide_banner -xerror -nostats -nostdin -f f32le");
                pooledSb.Builder.AppendFormat(" -ar {0}", InputFormat.SampleRateHz);
                pooledSb.Builder.AppendFormat(" -ac {0}", InputFormat.NumChannels);
                pooledSb.Builder.Append(" -i pipe:");
                pooledSb.Builder.AppendFormat(" -af channelmap=channel_layout={0} ", channelMapString);
                pooledSb.Builder.Append(OutputEncoderParameters);

                foreach (var kvp in AudioMetadata)
                {
                    if (!METADATA_KEY_VALIDATOR.IsMatch(kvp.Key))
                    {
                        throw new FormatException("Invalid format for metadata key \"" + kvp.Key + "\". Only letters, numbers, and underscore is allowed.");
                    }

                    pooledSb.Builder.AppendFormat(" -metadata {0}=\"{1}\"", kvp.Key, EscapeDoubleQuotes(kvp.Value));
                }

                if (overwriteExistingFile)
                {
                    pooledSb.Builder.Append(" -y");
                }

                pooledSb.Builder.AppendFormat(" \"{0}\"", outputFile.FullName);

                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = pooledSb.Builder.ToString(),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                };

                _ffmpegProcess = Process.Start(processInfo);
                _ffmpegStdIn = _ffmpegProcess.StandardInput.BaseStream;
                _ffmpegStdErr = _ffmpegProcess.StandardError;

                // We're supposed to check for status messages to be written to stderr, but for some reason the process hangs
                // forever when I try that.
                await DurandalTaskExtensions.NoOpTask;
            }
        }

        private static string EscapeDoubleQuotes(string input)
        {
            if (string.IsNullOrEmpty(input) ||
                !input.Contains("\""))
            {
                return input;
            }

            return input.Replace("\"", "\\\"");
        }
    }
}
