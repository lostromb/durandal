using Durandal.Common.Utils;
using System;
using System.IO;

namespace Durandal.Common.File
{
    /// <summary>
    /// Prototype class that implements a miniature file system inside of a random access stream
    /// </summary>
    internal class MemoryMappedFileSystem
    {
        private const int SECTOR_SIZE = 4096;

        private readonly bool _writeableFilesystem;
        private readonly Stream _base;

        public MemoryMappedFileSystem(Stream baseStream)
        {
            _base = baseStream;
            if (!_base.CanRead)
            {
                throw new ArgumentException("Memory mapped stream must be readable");
            }
            if (!_base.CanSeek)
            {
                throw new ArgumentException("Memory mapped stream must be seekable");
            }

            _writeableFilesystem = _base.CanWrite;
        }

        private class FileReference
        {
            public bool IsDirectory;
            public int StartSector;
            public long Length;
            public string Path;

            public static FileReference Read(Stream stream)
            {
                using (BinaryReader reader = new BinaryReader(stream, StringUtils.UTF8_WITHOUT_BOM, true))
                {
                    FileReference returnVal = new FileReference();
                    returnVal.IsDirectory = reader.ReadBoolean();
                    returnVal.StartSector = reader.ReadInt32();
                    returnVal.Length = reader.ReadInt64();
                    returnVal.Path = reader.ReadString();
                    return returnVal;
                }
            }

            public void Write(Stream stream)
            {
                using (BinaryWriter writer = new BinaryWriter(stream, StringUtils.UTF8_WITHOUT_BOM, true))
                {
                    writer.Write(IsDirectory);
                    writer.Write(StartSector);
                    writer.Write(Length);
                    writer.Write(Path);
                }
            }
        }
    }
}
