

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
    /// A base class that creates an encoding stream based around a separate
    /// process (such as ffmpeg) which pipes data through its stdin/stdout.
    /// Encoders such as lame.exe, flac.exe, opusenc.exe can all use this.
    /// </summary>
    public abstract class CommandLineEncoderStreamBase : IAudioCompressionStream
    {
        private string encodeParams;
        private BufferedStream _encoderStdin;
        private ThreadedStreamReader _encoderStdout;
        private Process _encoderProcess;
        private int _disposed = 0;

        public CommandLineEncoderStreamBase(string encoderExePath, string encoderParams)
        {
            this.encodeParams = encoderParams;
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = encoderExePath,
                Arguments = encodeParams,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            _encoderProcess = Process.Start(processInfo);
            _encoderStdout = new ThreadedStreamReader(_encoderProcess.StandardOutput.BaseStream, DefaultRealTimeProvider.Singleton, 128, 131072);
            _encoderStdin = new BufferedStream(_encoderProcess.StandardInput.BaseStream, 131072);
        }
        
        ~CommandLineEncoderStreamBase()
        {
            Dispose(false);
        }

        public byte[] Compress(AudioChunk input)
        {
            byte[] allData = input.GetDataAsBytes();
            int bytesWritten = 0;
            int CHUNK_SIZE = 64;
            using (RecyclableMemoryStream returnVal = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                while (bytesWritten < allData.Length)
                {
                    int inputChunkSize = Math.Min(CHUNK_SIZE, allData.Length - bytesWritten);

                    // Write uncompressed waveform
                    _encoderStdin.Write(allData, bytesWritten, inputChunkSize);
                    _encoderStdin.Flush();

                    // Read compressed data back from the process
                    int blockSize = _encoderStdout.Available();
                    byte[] outputChunk = new byte[blockSize];
                    int outputChunkSize = _encoderStdout.Read(outputChunk, 0, blockSize);
                    if (outputChunkSize > 0)
                    {
                        returnVal.Write(outputChunk, 0, outputChunkSize);
                    }

                    bytesWritten += inputChunkSize;
                }

                return returnVal.ToArray();
            }
        }

        public byte[] Close()
        {
            using (MemoryStream finalOutput = new MemoryStream())
            {
                _encoderStdout.FlushToStream(finalOutput);

                _encoderStdin.Flush();
                _encoderStdin.Close();

                while (!_encoderStdout.EndOfStream)
                {
                    _encoderStdout.FlushToStream(finalOutput);
                }

                return finalOutput.ToArray();
            }
        }

        public string GetEncodeParams()
        {
            return encodeParams;
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
                if (_encoderProcess != null)
                {
                    _encoderProcess.Dispose();
                }

                if (_encoderStdin != null)
                {
                    _encoderStdin.Dispose();
                }

                if (_encoderStdout != null)
                {
                    _encoderStdout.Dispose();
                }
            }
        }
    }
}
