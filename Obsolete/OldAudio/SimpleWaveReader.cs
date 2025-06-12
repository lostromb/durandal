using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Audio;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// A very simple class which extracts 16-bit audio samples from a RIFF (standard .wav) format source stream.
    /// </summary>
    public class SimpleWaveReader : IDisposable
    {
        private const int FIELD_SAMPLE_RATE = 24;
        private const int FIELD_NUM_CHANNELS = 22;
        private const int FIELD_BITS_PER_SAMPLE = 34;
        private const int FIELD_DATA_LENGTH = 40;
        
        private int _sampleRate;
        private ThreadedStreamReader _streamReader;
        private int _disposed = 0;

        public int SampleRate
        {
            get
            {
                return _sampleRate;
            }
        }

        public bool EndOfStream
        {
            get
            {
                return _streamReader.EndOfStream;
            }
        }
        
        public SimpleWaveReader(Stream inputStream, IRealTimeProvider realTime)
        {
            _streamReader = new ThreadedStreamReader(inputStream, realTime, 4096);
            ParseHeader();
        }

        ~SimpleWaveReader()
        {
            Dispose(false);
        }

        public short[] ReadNextSampleFrame()
        {
            if (_streamReader.EndOfStream)
                return new short[0]; //throw new EndOfStreamException("The wav stream has closed; there is no more to read");

            int bufferSize = (_streamReader.Available() / 2) * 2; // Force ourselves to read an even # of bytes, otherwise everything breaks
            byte[] buffer = new byte[bufferSize];
            int bytesRead = _streamReader.Read(buffer, 0, bufferSize);
            if (bytesRead <= 0)
                return new short[0];

            byte[] actualData = new byte[bytesRead];
            Array.Copy(buffer, actualData, bytesRead);
            short[] returnVal = AudioMath.BytesToShorts(actualData);
            // FIXME: This method keeps stereo channels interleaved, which means they'll seem to play at 1/2 speed.
            // Not a big deal since we specified mono-only support at the start of this project

            return returnVal;
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
                _streamReader?.Dispose();
            }
        }

        /// <summary>
        /// Parse the wave format from the riff header.
        /// This method is very dumb, and assumes that all files have similarly formatted headers that end up being 44 bytes long.
        /// </summary>
        private void ParseHeader()
        {
            // Read 44 bytes from the header
            byte[] header = new byte[44];
            int cursor = 0;
            while (cursor < 44)
            {
                cursor += _streamReader.Read(header, cursor, 44 - cursor);
            }

            // And parse it
            _sampleRate = ReadSampleRate(header);

            int numChannels = ReadNumChannels(header);
            int bitsPerSample = ReadBitsPerSample(header);
            if (numChannels != 1)
            {
                throw new NotSupportedException("SimpleWaveReader only supports single-channel wave streams");
            }

            if (bitsPerSample != 16)
            {
                throw new NotSupportedException("SimpleWaveReader only supports 16-bit wave streams");
            }
        }

        private static int ReadBitsPerSample(byte[] header)
        {
            if (header.Length < 44)
            {
                throw new ArgumentException("RIFF header is too small; must be at least 44 bytes");
            }
            return BitConverter.ToInt16(header, FIELD_BITS_PER_SAMPLE);
        }

        // todo: fix this ghetto code
        private static readonly byte[] STANDARD_RIFF_HEADER = { 
            0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x00,
            0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x80, 0x3E, 0x00, 0x00, 0x00, 0x7D, 0x00, 0x00,
            0x02, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61, 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// Returns a 16K two-byte mono riff header with filesize = 0
        /// </summary>
        /// <returns></returns>
        public static byte[] BuildRiffHeader()
        {
            return STANDARD_RIFF_HEADER;
        }

        /// <summary>
        /// Returns a 16K two-byte mono riff header with the proper data lengths set
        /// </summary>
        /// <param name="audioDataSize"></param>
        /// <returns></returns>
        public static byte[] BuildRiffHeader(int audioDataSize)
        {
            return BuildRiffHeader(audioDataSize, 16000);
        }

        /// <summary>
        /// Returns a 16K two-byte mono riff header with the proper data lengths set
        /// </summary>
        /// <param name="audioDataSize"></param>
        /// <returns></returns>
        public static byte[] BuildRiffHeader(int audioDataSize, int sampleRate)
        {
            byte[] header = STANDARD_RIFF_HEADER;

            int headerSize = 44;
            int headerStart = 0;
            int fileSizeOffset = 4;

            // for debug
            //int lastFileSize = BitConverter.ToInt32(waveData, fileSizeOffset);
            //int lastDataSize = BitConverter.ToInt32(waveData, dataSizeOffset);

            byte[] fileSize = BitConverter.GetBytes(audioDataSize + headerSize - 8);
            byte[] dataSize = BitConverter.GetBytes(audioDataSize);
            byte[] sampleRateField = BitConverter.GetBytes(sampleRate);

            Array.Copy(fileSize, 0, header, headerStart + fileSizeOffset, 4);
            Array.Copy(dataSize, 0, header, headerStart + FIELD_DATA_LENGTH, 4);
            Array.Copy(sampleRateField, 0, header, FIELD_SAMPLE_RATE, 4);

            return header;
        }

        private static int ReadSampleRate(byte[] header)
        {
            if (header.Length < 44)
            {
                throw new ArgumentException("RIFF header is too small; must be at least 44 bytes");
            }
            return BitConverter.ToInt32(header, FIELD_SAMPLE_RATE);
        }

        private static int ReadNumChannels(byte[] header)
        {
            if (header.Length < 44)
            {
                throw new ArgumentException("RIFF header is too small; must be at least 44 bytes");
            }
            return BitConverter.ToInt16(header, FIELD_NUM_CHANNELS);
        }

        /// <summary>
        /// Given a byte array representing a RIFF-format WAVE file, this method will augment the filesize and data size fields in the header.
        /// </summary>
        /// <param name="waveData"></param>
        /// <returns>True if finishing completed normally</returns>
        public static bool FinishRiffWaveHeader(byte[] waveData)
        {
            int headerSize = 44;
            int headerStart = 0;
            int fileSizeOffset = 4;
            int dataSizeOffset = 40;

            if (waveData.Length < headerSize)
                return false;

            // for debug
            //int lastFileSize = BitConverter.ToInt32(waveData, fileSizeOffset);
            //int lastDataSize = BitConverter.ToInt32(waveData, dataSizeOffset);

            byte[] fileSize = BitConverter.GetBytes(waveData.Length - 8); // Remember to subtract 8 bytes from the file size header, to account for the RIFF tag overhead
            byte[] dataSize = BitConverter.GetBytes(waveData.Length - headerSize);

            Array.Copy(fileSize, 0, waveData, headerStart + fileSizeOffset, 4);
            Array.Copy(dataSize, 0, waveData, headerStart + dataSizeOffset, 4);

            return true;
        }
    }
}
