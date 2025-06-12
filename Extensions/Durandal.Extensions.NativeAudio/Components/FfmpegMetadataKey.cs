using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.NativeAudio.Components
{
    /// <summary>
    /// Contains static strings for common FFMPEG metadata keys, to be used
    /// with the -metadata KEY="VALUE" command line syntax.
    /// </summary>
    public static class FfmpegMetadataKey
    {
        public static readonly string TITLE = "title";
        public static readonly string ARTIST = "artist";
        public static readonly string ALBUM_ARTIST = "album_artist";
        public static readonly string ALBUM = "album";
        public static readonly string TRACK = "track";
        public static readonly string GENRE = "genre";
        public static readonly string DATE = "date";
        public static readonly string COMPOSER = "composer";
        public static readonly string LANGUAGE = "language";

        public static readonly string TRACKTOTAL = "TRACKTOTAL";
        public static readonly string DISCTOTAL = "DISCTOTAL";
        public static readonly string RELEASECOUNTRY = "RELEASECOUNTRY";
        public static readonly string LABELNO = "LABELNO";
    }
}
