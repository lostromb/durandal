using Durandal.Common.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http
{
    public abstract class HttpContentStream : NonRealTimeStream
    {
        /// <summary>
        /// Gets the total number of content bytes that have been transferred by this stream.
        /// </summary>
        public abstract long ContentBytesTransferred { get; }

        /// <summary>
        /// After this stream has finished reading its content, the socket may have trailers
        /// appended to the end. This field is used to store those trailers.
        /// </summary>
        public abstract HttpHeaders Trailers { get; }
    }
}
