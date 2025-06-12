using System;
using System.Collections.Generic;
using System.IO;

namespace Durandal.Common.Audio
{
    // A stream class that simply accepts whatever audio is passed to it
    // and stores it into a "bucket" audio chunk that can be retrieved later.
    public class BucketAudioStream : Stream
    {
        private List<short[]> history = new List<short[]>();
        private int audioDataLength = 0;
        
        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > 1 && count % 2 == 0)
            {
                byte[] newData = new byte[count];
                Array.Copy(buffer, offset, newData, 0, count);
                short[] audioData = AudioMath.BytesToShorts(newData);
                Write(audioData);
            }
        }

        public void Write(short[] audioData)
        {
            history.Add(audioData);
            audioDataLength += audioData.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0;
        }

        public override bool CanRead
        {
            get { return false; }
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
            get { return audioDataLength; }
        }

        public override long Position { get; set; }

        public short[] GetAllData()
        {
            short[] data = new short[audioDataLength];
            int cursor = 0;
            foreach (short[] chunk in history)
            {
                Array.Copy(chunk, 0, data, cursor, chunk.Length);
                cursor += chunk.Length;
            }
            return data;
        }

        public void Clear()
        {
            history.Clear();
            audioDataLength = 0;
        }
    }
}
