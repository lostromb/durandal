using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Durandal.Common.File
{
    /// <summary>
    /// experimental garbage for now
    /// </summary>
    internal class MemoryMappedStream : Stream
    {
        private const int SECTOR_SIZE = 4096;
        private const int SECTOR_HEADER_LENGTH = 9; // 1 byte occupied, 4 bytes previous sector, 4 bytes next sector

        private Stream _baseStream;
        private byte[] _sectorHeader = new byte[SECTOR_HEADER_LENGTH];
        private bool _writeable;
        private int _firstSector = -1;
        private int _currentSector = -1;
        private int _prevSector = -1;
        private int _nextSector = -1;
        private int _sectorCursor = 0;
        private long _virtualCursor = 0;
        private int _virtualFileLength = 0;
        private bool _currentSectorValid = false;
        private long _fileLengthOffset = -1;
        private int _disposed = 0;

        internal MemoryMappedStream(Stream baseStream, int startSector, long fileLengthOffset, bool writeable = false)
        {
            _baseStream = baseStream;
            _writeable = writeable;
            _firstSector = startSector;
            _fileLengthOffset = fileLengthOffset;
            ChangeSector(_firstSector);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return _writeable;
            }
        }

        public override long Length
        {
            get
            {
                return _virtualFileLength;
            }
        }

        public override long Position
        {
            get
            {
                return _virtualCursor;
            }

            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

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
                    _baseStream?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private long ReadFileSize()
        {
            long oldPos = _baseStream.Position;
            _baseStream.Seek(_fileLengthOffset - oldPos, SeekOrigin.Current);
            byte[] temp = new byte[8];
            _baseStream.Read(temp, 0, 8);
            _baseStream.Seek(oldPos - _baseStream.Position, SeekOrigin.Current);
            return BitConverter.ToInt64(temp, 0);
        }

        private void WriteFileSize()
        {
            long oldPos = _baseStream.Position;
            _baseStream.Seek(_fileLengthOffset - oldPos, SeekOrigin.Current);
            byte[] temp = BitConverter.GetBytes(_virtualFileLength);
            _baseStream.Write(temp, 0, 8);
            _baseStream.Seek(oldPos - _baseStream.Position, SeekOrigin.Current);
        }

        private void ChangeSector(int targetSector)
        {
            long destination = (long)targetSector * SECTOR_SIZE;
            long curPos = _baseStream.Position;

            // If we are about to fall off the edge of the base stream, increase its size
            if (destination > _baseStream.Length)
            {
                if (_writeable)
                {
                    _baseStream.SetLength(destination + SECTOR_SIZE);
                }
                else
                {
                    throw new InvalidOperationException("Attempted to expand the size of a read-only filesystem");
                }
            }

            _baseStream.Seek(destination - curPos, SeekOrigin.Current);
            _currentSector = targetSector;

            // Read the new sector header. Keep in mind it may be invalid
            _baseStream.Read(_sectorHeader, 0, SECTOR_HEADER_LENGTH);
            _currentSectorValid = (_sectorHeader[0] & 0x1) != 0;
            if (_currentSectorValid)
            {
                _prevSector = BitConverter.ToInt32(_sectorHeader, 1);
                _nextSector = BitConverter.ToInt32(_sectorHeader, 5);
            }
            else
            {
                _prevSector = -1;
                _nextSector = -1;
            }

            _sectorCursor = SECTOR_HEADER_LENGTH;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                if (offset < 0 || offset > _virtualFileLength)
                {
                    throw new IndexOutOfRangeException("Attempted to seek beyond the boundaries of a file");
                }

                ChangeSector(_firstSector);
            }
            else if (origin == SeekOrigin.End)
            {
                if (offset < 0 || offset > _virtualFileLength)
                {
                    throw new IndexOutOfRangeException("Attempted to seek beyond the boundaries of a file");
                }

                // We don't know the tail sector so there's not an optimal way to seek from end of file unfortunately.
                // Convert it into a relative offset
                offset = (_virtualFileLength - offset) - (_virtualCursor);
            }
            else if (origin == SeekOrigin.Current)
            {
                if (_virtualCursor + offset < 0 || _virtualCursor + offset > _virtualFileLength)
                {
                    throw new IndexOutOfRangeException("Attempted to seek beyond the boundaries of a file");
                }
            }

            while (offset != 0)
            {
                // Are we inside the target sector?
                if (_sectorCursor + offset < SECTOR_SIZE &&
                    _sectorCursor + offset >= SECTOR_HEADER_LENGTH)
                {
                    _baseStream.Seek(offset, SeekOrigin.Current);
                    _sectorCursor += (int)offset;
                    _virtualCursor += (int)offset;
                }
                else if (offset > 0)
                {
                    // Go to next sector
                    if (_nextSector < 0)
                    {
                        throw new IndexOutOfRangeException("Attempted to seek outside of valid data");
                    }

                    _virtualCursor += (SECTOR_SIZE - SECTOR_HEADER_LENGTH);
                    ChangeSector(_nextSector);
                }
                else if (offset < 0)
                {
                    // Go to previous sector
                    if (_prevSector < 0)
                    {
                        throw new IndexOutOfRangeException("Attempted to seek outside of valid data");
                    }

                    _virtualCursor -= (SECTOR_SIZE - SECTOR_HEADER_LENGTH);
                    ChangeSector(_prevSector);
                }
            }

            return _virtualCursor;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // See how many bytes we are able to read from the current sector
            int bytesRead = 0;
            long bytesLeftToRead = count;
            long bytesLeftInFile = _virtualFileLength - _virtualCursor;
            long bytesLeftInSector = SECTOR_SIZE - _sectorCursor;

            while (bytesLeftToRead > 0 && bytesLeftInFile > 0)
            {
                // Red all we can from the current sector
                int readLength = (int)Math.Min(bytesLeftInSector, bytesLeftInFile);
                readLength = _baseStream.Read(buffer, offset + bytesRead, readLength);
                _sectorCursor += readLength;
                _virtualCursor += readLength;
                bytesRead += readLength;
                if (_sectorCursor == SECTOR_SIZE)
                {
                    // Move to next sector if possible
                    if (_nextSector >= 0)
                    {
                        ChangeSector(_nextSector);
                        if (!_currentSectorValid)
                        {
                            throw new InvalidDataException("Attempted to read from an undefined virtual sector");
                        }
                    }
                }

                bytesLeftInSector = SECTOR_SIZE - _sectorCursor;
                bytesLeftInFile = _virtualFileLength - _virtualCursor;
            }

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // See how many bytes we are able to write to the current sector
            int bytesWritten = 0;
            long bytesLeftToWrite = count;
            long spaceLeftInSector = SECTOR_SIZE - _sectorCursor;

            while (bytesLeftToWrite > 0)
            {
                // Red all we can from the current sector
                int writeLength = (int)Math.Min(bytesLeftToWrite, spaceLeftInSector);
                _baseStream.Write(buffer, offset + bytesWritten, writeLength);
                _sectorCursor += writeLength;
                _virtualCursor += writeLength;
                bytesWritten += writeLength;
                bytesLeftToWrite -= writeLength;
                if (_sectorCursor == SECTOR_SIZE)
                {
                    // Move to next sector
                    if (_nextSector < 0)
                    {
                        // Sector needs initialization first
                        _nextSector = FindAvailableSector();
                        int oldPrevSector = _currentSector;
                        UpdateCurrentSector(true, _prevSector, _nextSector);
                        ChangeSector(_nextSector);
                        UpdateCurrentSector(true, oldPrevSector, -1);
                    }

                    if (_nextSector >= 0)
                    {
                        ChangeSector(_nextSector);
                        if (!_currentSectorValid)
                        {
                            // Should never happen
                            throw new InvalidDataException("Attempted to write to an uninitialized sector");
                        }
                    }
                }

                spaceLeftInSector = SECTOR_SIZE - _sectorCursor;
            }

            // Write back the new file length
            _virtualFileLength += bytesWritten;
            WriteFileSize();
        }

        private void UpdateCurrentSector(bool valid, int previous, int next)
        {
            _baseStream.Seek(0 - _sectorCursor, SeekOrigin.Current);
            _baseStream.WriteByte((byte)(valid ? 0x1 : 0x0));
            byte[] f = BitConverter.GetBytes(previous);
            _baseStream.Write(f, 0, 4);
            f = BitConverter.GetBytes(next);
            _baseStream.Write(f, 0, 4);
            _baseStream.Seek(_sectorCursor - SECTOR_HEADER_LENGTH, SeekOrigin.Current);
        }

        private int FindAvailableSector()
        {
            long oldPos = _baseStream.Position;
            int returnVal = _currentSector + 1;
            while (true)
            {
                _baseStream.Seek((long)SECTOR_SIZE * returnVal, SeekOrigin.Begin);
                if ((_baseStream.ReadByte() & 0x1) == 0)
                {
                    _baseStream.Seek(oldPos, SeekOrigin.Begin);
                    return returnVal;
                }
                else
                {
                    returnVal++;
                }
            }
        }
    }
}
