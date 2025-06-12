using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Compression.Zip
{
    partial class ZipFile
    {
        private static System.Text.Encoding _defaultEncoding = null;
        private static bool _defaultEncodingInitialized = false;

        /// <summary>
        /// 
        /// 
        /// Static constructor for ZipFile
        /// </summary>
        /// <remarks>
        /// Code Pages 437 and 1252 for English are same
        /// Code Page 1252 Windows Latin 1 (ANSI) - <see href="https://msdn.microsoft.com/en-US/library/cc195054.aspx"/>
        /// Code Page 437 MS-DOS Latin US - <see href="https://msdn.microsoft.com/en-US/library/cc195060.aspx"/>
        /// </remarks>
        static ZipFile()
        {
            _defaultEncoding = SharedUtilities.GetIBM437Encoding();
        }

        /// <summary>
        /// The default text encoding used in zip archives.  It is numeric 437, also
        /// known as IBM437.
        /// </summary>
        /// <seealso cref="Durandal.Common.Compression.Zip.ZipFile.AlternateEncoding"/>
        public static System.Text.Encoding DefaultEncoding
        {
            get
            {
                return _defaultEncoding;
            }
            set
            {
                if (_defaultEncodingInitialized)
                {
                    return;
                }
                _defaultEncoding = value;
                _defaultEncodingInitialized = true;
            }
        }
    }
}
