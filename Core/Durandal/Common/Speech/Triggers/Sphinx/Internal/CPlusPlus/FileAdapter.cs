using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus
{
    internal class FileAdapter
    {
        private IFileSystem _fileSystem;

        public FileAdapter(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public FILE fopen(Pointer<byte> fileName, string accessMode)
        {
            string path = cstring.FromCString(fileName);
            VirtualPath virtualPath = new VirtualPath(path);
            return new FILE(_fileSystem.OpenStream(virtualPath, FileOpenMode.Open, FileAccessMode.Read));
        }

        public BinaryReader fopen_new(Pointer<byte> fileName, string accessMode)
        {
            string path = cstring.FromCString(fileName);
            VirtualPath virtualPath = new VirtualPath(path);
            return new BinaryReader(_fileSystem.OpenStream(virtualPath, FileOpenMode.Open, FileAccessMode.Read));
        }

        public bool file_exists(Pointer<byte> fileName)
        {
            string path = cstring.FromCString(fileName);
            VirtualPath virtualPath = new VirtualPath(path);
            return _fileSystem.Exists(virtualPath);
        }

        /// <summary>
        /// Get stats on a file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="returnVal"></param>
        public int stat(Pointer<byte> fileName, BoxedValue<stat_t> returnVal)
        {
            string path = cstring.FromCString(fileName);
            VirtualPath virtualPath = new VirtualPath(path);
            if (_fileSystem.Exists(virtualPath))
            {
                FileStat stat = _fileSystem.Stat(virtualPath);
                if (stat == null)
                {
                    return -1; // Some other error occurred; we don't know
                }

                returnVal.Val = new stat_t();
                returnVal.Val.st_mtime = stat.LastWriteTime.ToFileTime();
                returnVal.Val.st_size = stat.Size;
                return 0;
            }
            else
            {
                return -1; // File not found, return EBADF for ferr
            }
        }
    }
}
