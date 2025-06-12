using Durandal.Common.File;

namespace Durandal.Common.Speech
{
    using Durandal.Common.Audio;
    using Durandal.Common.Utils;
    using System;
    using System.IO;
    using System.Threading;
    using Durandal.Common.IO;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A dummy buffer class that can never reach end of stream.
    /// This class is designed to serve as the speech input for SAPI interfaces, since
    /// sapi behaves strangely with limited-length streams. At the same time,
    /// SAPI's StopListening function will block forever until it reaches the end of input(?)
    /// so we have to accommodate that as well.
    /// </summary>
    public class SpeechStreamer : Stream
    {
        private const int READ_TIMEOUT = 500;
        private ConcurrentBuffer<short> audioBuffer;
        private EventWaitHandle writeHandle;
        private volatile bool _closed = false;
        private int _disposed = 0;

        public SpeechStreamer(int bufferSize)
        {
            audioBuffer = new ConcurrentBuffer<short>(bufferSize);
            writeHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { return -1L; }
        }

        public override long Position
        {
            get { return 0L; }
            set { }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0L;
        }

        public override void SetLength(long value) { }

#pragma warning disable 0114
        public void Close()
        {
            _closed = true;
        }
#pragma warning restore 0114

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    writeHandle.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override int Read(byte[] target, int offset, int count)
        {
            if (_closed)
            {
                return 0;
            }
            
            int shortCount = count / 2;
            //while (!_closed && audioBuffer.Available() < shortCount)
            //{
            //    // Wait for some new data to be written
            //    // If we timeout, return a bunch of zeroes so as to unblock the listener
            //    if (!writeHandle.WaitOne(READ_TIMEOUT))
            //    {
            //        byte[] zeroes = new byte[shortCount * 2];
            //        ArrayExtensions.MemCopy(zeroes, 0, target, offset, shortCount * 2);
            //        return shortCount * 2;
            //    }
            //}
            //byte[] payload = AudioMath.ShortsToBytes(audioBuffer.Read(shortCount));
            //ArrayExtensions.MemCopy(payload, 0, target, offset, shortCount * 2);
            return shortCount * 2;
        }

        public override void Write(byte[] data, int offset, int count)
        {
        }

        public void WriteAudio(float[] buffer, int bufferOffset, int numSamplesPerChannel)
        {
            //audioBuffer.Write(chunk.Data);
            //writeHandle.Set();
        }

        public int Capacity
        {
            get
            {
                return audioBuffer.Capacity();
            }
        }

        public override void Flush()
        {
        }

        public int Available
        {
            get
            {
                return audioBuffer.Available();
            }
        }
    }
}
