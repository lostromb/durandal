

using Durandal.Common.Tasks;

namespace Durandal.Common.Audio
{
    using Durandal.Common.AudioV2;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using Durandal.Common.IO;
    using Durandal.Common.Time;

    /// <summary>
    /// A base class that creates an decoding stream based around a separate
    /// process (such as ffmpeg) which pipes data through its stdin/stdout.
    /// Encoders such as lame.exe, flac.exe, opusdec.exe can all use this.
    /// </summary>
    public abstract class CommandLineDecoderStreamBase : IAudioDecompressionStream
    {
        private BufferedStream _decoderStdin;
        private ThreadedStreamReader _decoderStdout;
        private IVariableBuffer _riffHeaderBuffer;
        private bool _riffHeaderProcessed;
        private int _outputSampleRate;
        private Process _encoderProcess;
        private int _disposed = 0;

        public CommandLineDecoderStreamBase(string decoderExePath, string decoderParams)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = decoderExePath,
                Arguments = decoderParams,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            _encoderProcess = Process.Start(processInfo);
            _decoderStdout = new ThreadedStreamReader(_encoderProcess.StandardOutput.BaseStream, DefaultRealTimeProvider.Singleton, 128, 131072);
            _decoderStdin = new BufferedStream(_encoderProcess.StandardInput.BaseStream, 131072);
            _riffHeaderBuffer = new SingleUseBuffer();
            _riffHeaderProcessed = false;
            _outputSampleRate = 0;
        }

        ~CommandLineDecoderStreamBase()
        {
            Dispose(false);
        }

        public AudioChunk Decompress(ArraySegment<byte> input)
        {
            int bytesWritten = 0;
            // Windows STDIN has a buffer of 1024 bytes, so keep our buffers small here
            int CHUNK_SIZE = 64;
            using (RecyclableMemoryStream returnVal = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                while (bytesWritten < input.Count)
                {
                    int inputChunkSize = Math.Min(CHUNK_SIZE, input.Count - bytesWritten);

                    // Write compressed data
                    _decoderStdin.Write(input.Array, input.Offset + bytesWritten, inputChunkSize);
                    _decoderStdin.Flush();

                    // Read uncompressed wav data (usually with a riff header, we don't know) back from the process
                    int blockSize = _decoderStdout.Available();
                    byte[] outputChunk = new byte[blockSize];
                    int outputChunkSize = _decoderStdout.Read(outputChunk, 0, blockSize);
                    if (outputChunkSize > 0)
                    {
                        // Intercept the riff header, if applicable
                        if (!_riffHeaderProcessed)
                        {
                            byte[] trimmedBuffer = new byte[outputChunkSize];
                            Array.Copy(outputChunk, trimmedBuffer, outputChunkSize);
                            _riffHeaderBuffer.Write(trimmedBuffer, 0, trimmedBuffer.Length);
                            if (_riffHeaderBuffer.Available() >= 44)
                            {
                                // Parse the output sample rate and discard the rest of the riff data
                                byte[] riffHeader = new byte[44];
                                _riffHeaderBuffer.Read(riffHeader, 0, 44);
                                _outputSampleRate = BitConverter.ToInt32(riffHeader, 24);

                                // Flush the buffer out
                                int tailChunkSize = _riffHeaderBuffer.Available();
                                byte[] theRest = new byte[tailChunkSize];
                                tailChunkSize = _riffHeaderBuffer.Read(theRest, 0, tailChunkSize);
                                returnVal.Write(theRest, 0, tailChunkSize);
                                _riffHeaderProcessed = true;
                                _riffHeaderBuffer.CloseWrite();
                            }
                        }
                        else
                        {
                            // Just write the data across
                            returnVal.Write(outputChunk, 0, outputChunkSize);
                        }
                    }

                    bytesWritten += inputChunkSize;
                }

                byte[] rawWavData = returnVal.ToArray();
                returnVal.Close();

                return new AudioChunk(rawWavData, _outputSampleRate);
            }
        }

        public AudioChunk Close()
        {
            using (RecyclableMemoryStream finalOutput = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                _decoderStdout.FlushToStream(finalOutput);

                _decoderStdin.Flush();
                _decoderStdin.Close();

                while (!_decoderStdout.EndOfStream)
                {
                    _decoderStdout.FlushToStream(finalOutput);
                }

                byte[] rawWavData = finalOutput.ToArray();
                return new AudioChunk(rawWavData, _outputSampleRate);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (!disposing) Durandal.Common.Utils.DebugMemoryLeaktracer.TraceDisposableItemFinalized(this.GetType());

            if (disposing)
            {
                _encoderProcess?.Dispose();
                _decoderStdin?.Dispose();
                _decoderStdout?.Dispose();
            }
        }
    }
}
