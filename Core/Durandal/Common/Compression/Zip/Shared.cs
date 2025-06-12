// Shared.cs
// ------------------------------------------------------------------
//
// Copyright (c) 2006-2011 Dino Chiesa.
// All rights reserved.
//
// This code module is part of DotNetZip, a zipfile class library.
//
// ------------------------------------------------------------------
//
// This code is licensed under the Microsoft Public License.
// See the file License.txt for the license details.
// More info on: http://dotnetzip.codeplex.com
//
// ------------------------------------------------------------------
//
// Last Saved: <2011-August-02 19:41:01>
//
// ------------------------------------------------------------------
//
// This module defines some shared utility classes and methods.
//
// Created: Tue, 27 Mar 2007  15:30
//

using Durandal.Common.File;
using System;
using System.IO;

namespace Durandal.Common.Compression.Zip
{
    /// <summary>
    /// Collects general purpose utility methods.
    /// </summary>
    internal static class SharedUtilities
    {
        /// private null constructor
        //private SharedUtilities() { }

        // workitem 8423
        public static Int64 GetFileLength(VirtualPath fileName, IFileSystem fileSystem)
        {
            if (!fileSystem.Exists(fileName))
                throw new FileNotFoundException(String.Format("Could not find file '{0}'.", fileName), fileName.FullName);

            FileStat stat = fileSystem.Stat(fileName);
            return stat.Size;
        }


        [System.Diagnostics.Conditional("NETCF")]
        public static void Workaround_Ladybug318918(Stream s)
        {
            // This is a workaround for this issue:
            // https://connect.microsoft.com/VisualStudio/feedback/details/318918
            // It's required only on NETCF.
            s.Flush();
        }
        
        private static System.Text.RegularExpressions.Regex doubleDotRegex1 =
            new System.Text.RegularExpressions.Regex(@"^(.*/)?([^/\\.]+/\\.\\./)(.+)$");

        private static string SimplifyFwdSlashPath(string path)
        {
            if (path.StartsWith("./")) path = path.Substring(2);
            path = path.Replace("/./", "/");

            // Replace foo/anything/../bar with foo/bar
            path = doubleDotRegex1.Replace(path, "$1$3");
            return path;
        }


        /// <summary>
        /// Utility routine for transforming path names from filesystem format (on Windows that means backslashes) to
        /// a format suitable for use within zipfiles. This means trimming the volume letter and colon (if any) And
        /// swapping backslashes for forward slashes.
        /// </summary>
        /// <param name="pathName">source path.</param>
        /// <returns>transformed path</returns>
        public static string NormalizePathForUseInZipFile(string pathName)
        {
            // boundary case
            if (String.IsNullOrEmpty(pathName)) return pathName;

            // trim volume if necessary
            if ((pathName.Length >= 2)  && ((pathName[1] == ':') && (pathName[2] == '\\')))
                pathName =  pathName.Substring(3);

            // swap slashes
            pathName = pathName.Replace('\\', '/');

            // trim all leading slashes
            while (pathName.StartsWith("/")) pathName = pathName.Substring(1);

            return SimplifyFwdSlashPath(pathName);
        }

        /// <summary>
        /// Sanitize paths in zip files. This means making sure that relative paths in a zip file don't go outside
        /// the top directory. Entries like something/../../../../Temp/evil.txt get sanitized to Temp/evil.txt
        /// when extracting
        /// </summary>
        /// <param name="path">A path with forward slashes as directory separator</param>
        /// <returns>sanitized path</returns>
        public static string SanitizePath(string path)
        {
            System.Collections.Generic.List<string> dirs = new System.Collections.Generic.List<string>();
            int level = 0;
            foreach (string dir in path.Split('/'))
            {
                if (dir == "..")
                {
                    if (level == 0)
                        continue;
                    level--;
                }
                else
                {
                    if (dirs.Count - 1 < level)
                        dirs.Add(dir);
                    else
                        dirs[level] = dir;
                    level++;
                }
            }

            path = "";
            for (int i = 0; i < level; i++)
            {
                if (i > 0)
                    path += "/";
                path += dirs[i];
            }

            return path;
        }


        //static System.Text.Encoding ibm437 = System.Text.Encoding.GetEncoding("IBM437");
        static System.Text.Encoding utf8 = System.Text.Encoding.GetEncoding("UTF-8");

        internal static byte[] StringToByteArray(string value, System.Text.Encoding encoding)
        {
            byte[] a = encoding.GetBytes(value);
            return a;
        }
        internal static byte[] StringToByteArray(string value)
        {
            System.Text.Encoding ibm437 = GetIBM437Encoding();
            return StringToByteArray(value, ibm437);
        }

        private static System.Text.Encoding ibm437 = null;

        internal static System.Text.Encoding GetIBM437Encoding()
        {
            if (ibm437 == null)
            {
                try
                {
                    ibm437 = System.Text.Encoding.GetEncoding("IBM437");
                }
                catch (Exception)
                {
                    try
                    {
                        ibm437 = System.Text.Encoding.GetEncoding("ASCII");
                    }
                    catch (Exception)
                    {
                        ibm437 = System.Text.Encoding.UTF8;
                    }
                }
            }

            return ibm437;
        }

        //internal static byte[] Utf8StringToByteArray(string value)
        //{
        //    return StringToByteArray(value, utf8);
        //}

        //internal static string StringFromBuffer(byte[] buf, int maxlength)
        //{
        //    return StringFromBuffer(buf, maxlength, ibm437);
        //}

        internal static string Utf8StringFromBuffer(byte[] buf)
        {
            return StringFromBuffer(buf, utf8);
        }

        internal static string StringFromBuffer(byte[] buf, System.Text.Encoding encoding)
        {
            // this form of the GetString() method is required for .NET CF compatibility
            string s = encoding.GetString(buf, 0, buf.Length);
            return s;
        }


        internal static int ReadSignature(System.IO.Stream s)
        {
            int x = 0;
            try { x = _ReadFourBytes(s, "n/a"); }
            catch (BadReadException) { }
            return x;
        }


        internal static int ReadEntrySignature(System.IO.Stream s)
        {
            // handle the case of ill-formatted zip archives - includes a data descriptor
            // when none is expected.
            int x = 0;
            try
            {
                x = _ReadFourBytes(s, "n/a");
                if (x == ZipConstants.ZipEntryDataDescriptorSignature)
                {
                    // advance past data descriptor - 12 bytes if not zip64
                    s.Seek(12, SeekOrigin.Current);
                    // workitem 10178
                    Workaround_Ladybug318918(s);
                    x = _ReadFourBytes(s, "n/a");
                    if (x != ZipConstants.ZipEntrySignature)
                    {
                        // Maybe zip64 was in use for the prior entry.
                        // Therefore, skip another 8 bytes.
                        s.Seek(8, SeekOrigin.Current);
                        // workitem 10178
                        Workaround_Ladybug318918(s);
                        x = _ReadFourBytes(s, "n/a");
                        if (x != ZipConstants.ZipEntrySignature)
                        {
                            // seek back to the first spot
                            s.Seek(-24, SeekOrigin.Current);
                            // workitem 10178
                            Workaround_Ladybug318918(s);
                            x = _ReadFourBytes(s, "n/a");
                        }
                    }
                }
            }
            catch (BadReadException) { }
            return x;
        }


        internal static int ReadInt(System.IO.Stream s)
        {
            return _ReadFourBytes(s, "Could not read block - no data!  (position 0x{0:X8})");
        }

        private static int _ReadFourBytes(System.IO.Stream s, string message)
        {
            int n = 0;
            byte[] block = new byte[4];
            n = s.Read(block, 0, block.Length);
            if (n != block.Length) throw new BadReadException(String.Format(message, s.Position));
            int data = unchecked((((block[3] * 256 + block[2]) * 256) + block[1]) * 256 + block[0]);
            return data;
        }



        /// <summary>
        ///   Finds a signature in the zip stream. This is useful for finding
        ///   the end of a zip entry, for example, or the beginning of the next ZipEntry.
        /// </summary>
        ///
        /// <remarks>
        ///   <para>
        ///     Scans through 64k at a time.
        ///   </para>
        ///
        ///   <para>
        ///     If the method fails to find the requested signature, the stream Position
        ///     after completion of this method is unchanged. If the method succeeds in
        ///     finding the requested signature, the stream position after completion is
        ///     direct AFTER the signature found in the stream.
        ///   </para>
        /// </remarks>
        ///
        /// <param name="stream">The stream to search</param>
        /// <param name="SignatureToFind">The 4-byte signature to find</param>
        /// <returns>The number of bytes read</returns>
        internal static long FindSignature(System.IO.Stream stream, int SignatureToFind)
        {
            long startingPosition = stream.Position;

            int BATCH_SIZE = 65536; //  8192;
            byte[] targetBytes = new byte[4];
            targetBytes[0] = (byte)(SignatureToFind >> 24);
            targetBytes[1] = (byte)((SignatureToFind & 0x00FF0000) >> 16);
            targetBytes[2] = (byte)((SignatureToFind & 0x0000FF00) >> 8);
            targetBytes[3] = (byte)(SignatureToFind & 0x000000FF);
            byte[] batch = new byte[BATCH_SIZE];
            int n = 0;
            bool success = false;
            do
            {
                n = stream.Read(batch, 0, batch.Length);
                if (n != 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (batch[i] == targetBytes[3])
                        {
                            long curPosition = stream.Position;
                            stream.Seek(i - n, System.IO.SeekOrigin.Current);
                            // workitem 10178
                            Workaround_Ladybug318918(stream);

                            // workitem 7711
                            int sig = ReadSignature(stream);

                            success = (sig == SignatureToFind);
                            if (!success)
                            {
                                stream.Seek(curPosition, System.IO.SeekOrigin.Begin);
                                // workitem 10178
                                Workaround_Ladybug318918(stream);
                            }
                            else
                                break; // out of for loop
                        }
                    }
                }
                else break;
                if (success) break;

            } while (true);

            if (!success)
            {
                stream.Seek(startingPosition, System.IO.SeekOrigin.Begin);
                // workitem 10178
                Workaround_Ladybug318918(stream);
                return -1;  // or throw?
            }

            // subtract 4 for the signature.
            long bytesRead = (stream.Position - startingPosition) - 4;

            return bytesRead;
        }


        // If I have a time in the .NET environment, and I want to use it for
        // SetWastWriteTime() etc, then I need to adjust it for Win32.
        internal static DateTime AdjustTime_Reverse(DateTime time)
        {
            if (time.Kind == DateTimeKind.Utc) return time;
            DateTime adjusted = time;
            if (DateTime.Now.IsDaylightSavingTime() && !time.IsDaylightSavingTime())
                adjusted = time - new System.TimeSpan(1, 0, 0);

            else if (!DateTime.Now.IsDaylightSavingTime() && time.IsDaylightSavingTime())
                adjusted = time + new System.TimeSpan(1, 0, 0);

            return adjusted;
        }

#if NECESSARY
        // If I read a time from a file with GetLastWriteTime() (etc), I need
        // to adjust it for display in the .NET environment.
        internal static DateTime AdjustTime_Forward(DateTime time)
        {
            if (time.Kind == DateTimeKind.Utc) return time;
            DateTime adjusted = time;
            if (DateTime.Now.IsDaylightSavingTime() && !time.IsDaylightSavingTime())
                adjusted = time + new System.TimeSpan(1, 0, 0);

            else if (!DateTime.Now.IsDaylightSavingTime() && time.IsDaylightSavingTime())
                adjusted = time - new System.TimeSpan(1, 0, 0);

            return adjusted;
        }
#endif


        internal static DateTime PackedToDateTime(Int32 packedDateTime)
        {
            // workitem 7074 & workitem 7170
            if (packedDateTime == 0xFFFF || packedDateTime == 0)
                return new System.DateTime(1995, 1, 1, 0, 0, 0, 0);  // return a fixed date when none is supplied.

            Int16 packedTime = unchecked((Int16)(packedDateTime & 0x0000ffff));
            Int16 packedDate = unchecked((Int16)((packedDateTime & 0xffff0000) >> 16));

            int year = 1980 + ((packedDate & 0xFE00) >> 9);
            int month = (packedDate & 0x01E0) >> 5;
            int day = packedDate & 0x001F;

            int hour = (packedTime & 0xF800) >> 11;
            int minute = (packedTime & 0x07E0) >> 5;
            //int second = packedTime & 0x001F;
            int second = (packedTime & 0x001F) * 2;

            // validation and error checking.
            // this is not foolproof but will catch most errors.
            if (second >= 60) { minute++; second = 0; }
            if (minute >= 60) { hour++; minute = 0; }
            if (hour >= 24) { day++; hour = 0; }

            DateTime d = System.DateTime.Now;
            bool success= false;
            try
            {
                d = new System.DateTime(year, month, day, hour, minute, second, 0);
                success= true;
            }
            catch (System.ArgumentOutOfRangeException)
            {
                if (year == 1980 && (month == 0 || day == 0))
                {
                    try
                    {
                        d = new System.DateTime(1980, 1, 1, hour, minute, second, 0);
                success= true;
                    }
                    catch (System.ArgumentOutOfRangeException)
                    {
                        try
                        {
                            d = new System.DateTime(1980, 1, 1, 0, 0, 0, 0);
                success= true;
                        }
                        catch (System.ArgumentOutOfRangeException) { }

                    }
                }
                // workitem 8814
                // my god, I can't believe how many different ways applications
                // can mess up a simple date format.
                else
                {
                    try
                    {
                        while (year < 1980) year++;
                        while (year > 2030) year--;
                        while (month < 1) month++;
                        while (month > 12) month--;
                        while (day < 1) day++;
                        while (day > 28) day--;
                        while (minute < 0) minute++;
                        while (minute > 59) minute--;
                        while (second < 0) second++;
                        while (second > 59) second--;
                        d = new System.DateTime(year, month, day, hour, minute, second, 0);
                        success= true;
                    }
                    catch (System.ArgumentOutOfRangeException) { }
                }
            }
            if (!success)
            {
                string msg = String.Format("y({0}) m({1}) d({2}) h({3}) m({4}) s({5})", year, month, day, hour, minute, second);
                throw new ZipException(String.Format("Bad date/time format in the zip file. ({0})", msg));

            }
            // workitem 6191
            //d = AdjustTime_Reverse(d);
            d = DateTime.SpecifyKind(d, DateTimeKind.Local);
            return d;
        }


        internal
         static Int32 DateTimeToPacked(DateTime time)
        {
            // The time is passed in here only for purposes of writing LastModified to the
            // zip archive. It should always be LocalTime, but we convert anyway.  And,
            // since the time is being written out, it needs to be adjusted.

            time = time.ToLocalTime();
            // workitem 7966
            //time = AdjustTime_Forward(time);

            // see http://www.vsft.com/hal/dostime.htm for the format
            UInt16 packedDate = (UInt16)((time.Day & 0x0000001F) | ((time.Month << 5) & 0x000001E0) | (((time.Year - 1980) << 9) & 0x0000FE00));
            UInt16 packedTime = (UInt16)((time.Second / 2 & 0x0000001F) | ((time.Minute << 5) & 0x000007E0) | ((time.Hour << 11) & 0x0000F800));

            Int32 result = (Int32)(((UInt32)(packedDate << 16)) | packedTime);
            return result;
        }


        /// <summary>
        ///   Create a pseudo-random filename, suitable for use as a temporary
        ///   file, and open it.
        /// </summary>
        /// <remarks>
        /// <para>
        ///   The System.IO.Path.GetRandomFileName() method is not available on
        ///   the Compact Framework, so this library provides its own substitute
        ///   on NETCF.
        /// </para>
        /// <para>
        ///   This method produces a filename of the form
        ///   DotNetZip-xxxxxxxx.tmp, where xxxxxxxx is replaced by randomly
        ///   chosen characters, and creates that file.
        /// </para>
        /// </remarks>
        public static void CreateAndOpenUniqueTempFile(
            VirtualPath dir,
            IFileSystem fileSystem,
            out Stream fs,
            out VirtualPath filename)
        {
            // workitem 9763
            // http://dotnet.org.za/markn/archive/2006/04/15/51594.aspx
            // try 3 times:
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    filename = dir.Combine(InternalGetTempFileName());
                    fs = fileSystem.OpenStream(filename, FileOpenMode.OpenOrCreate, FileAccessMode.ReadWrite);
                    return;
                }
                catch (IOException)
                {
                    if (i == 2) throw;
                }
            }
            throw new IOException();
        }
        
        public static string InternalGetTempFileName()
        {
            return "DotNetZip-" + Path.GetRandomFileName().Substring(0, 8) + ".tmp";
        }


        /// <summary>
        /// Workitem 7889: handle ERROR_LOCK_VIOLATION during read
        /// </summary>
        /// <remarks>
        /// This could be gracefully handled with an extension attribute, but
        /// This assembly is built for .NET 2.0, so I cannot use them.
        /// </remarks>
        internal static int ReadWithRetry(System.IO.Stream s, byte[] buffer, int offset, int count, string FileName)
        {
            int n = 0;
            bool done = false;
            do
            {
                try
                {
                    n = s.Read(buffer, offset, count);
                    done = true;
                }
                catch (System.IO.IOException)
                {
                    throw;
                }
            }
            while (!done);

            return n;
        }
        
        private static uint _HRForException(System.Exception ex1)
        {
            return 0;
        }
    }



    /// <summary>
    ///   A decorator stream. It wraps another stream, and performs bookkeeping
    ///   to keep track of the stream Position.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     In some cases, it is not possible to get the Position of a stream, let's
    ///     say, on a write-only output stream like ASP.NET's
    ///     <c>Response.OutputStream</c>, or on a different write-only stream
    ///     provided as the destination for the zip by the application.  In this
    ///     case, programmers can use this counting stream to count the bytes read
    ///     or written.
    ///   </para>
    ///   <para>
    ///     Consider the scenario of an application that saves a self-extracting
    ///     archive (SFX), that uses a custom SFX stub.
    ///   </para>
    ///   <para>
    ///     Saving to a filesystem file, the application would open the
    ///     filesystem file (getting a <c>FileStream</c>), save the custom sfx stub
    ///     into it, and then call <c>ZipFile.Save()</c>, specifying the same
    ///     FileStream. <c>ZipFile.Save()</c> does the right thing for the zipentry
    ///     offsets, by inquiring the Position of the <c>FileStream</c> before writing
    ///     any data, and then adding that initial offset into any ZipEntry
    ///     offsets in the zip directory. Everything works fine.
    ///   </para>
    ///   <para>
    ///     Now suppose the application is an ASPNET application and it saves
    ///     directly to <c>Response.OutputStream</c>. It's not possible for DotNetZip to
    ///     inquire the <c>Position</c>, so the offsets for the SFX will be wrong.
    ///   </para>
    ///   <para>
    ///     The workaround is for the application to use this class to wrap
    ///     <c>HttpResponse.OutputStream</c>, then write the SFX stub and the ZipFile
    ///     into that wrapper stream. Because <c>ZipFile.Save()</c> can inquire the
    ///     <c>Position</c>, it will then do the right thing with the offsets.
    ///   </para>
    /// </remarks>
    public class CountingStream : System.IO.Stream
    {
        // workitem 12374: this class is now public
        private System.IO.Stream _s;
        private Int64 _bytesWritten;
        private Int64 _bytesRead;
        private Int64 _initialOffset;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="stream">The underlying stream</param>
        public CountingStream(System.IO.Stream stream)
            : base()
        {
            _s = stream;
            try
            {
                _initialOffset = _s.Position;
            }
            catch
            {
                _initialOffset = 0L;
            }
        }

        /// <summary>
        ///   Gets the wrapped stream.
        /// </summary>
        public Stream WrappedStream
        {
            get
            {
                return _s;
            }
        }

        /// <summary>
        ///   The count of bytes written out to the stream.
        /// </summary>
        public Int64 BytesWritten
        {
            get { return _bytesWritten; }
        }

        /// <summary>
        ///   the count of bytes that have been read from the stream.
        /// </summary>
        public Int64 BytesRead
        {
            get { return _bytesRead; }
        }

        /// <summary>
        ///    Adjust the byte count on the stream.
        /// </summary>
        ///
        /// <param name='delta'>
        ///   the number of bytes to subtract from the count.
        /// </param>
        ///
        /// <remarks>
        ///   <para>
        ///     Subtract delta from the count of bytes written to the stream.
        ///     This is necessary when seeking back, and writing additional data,
        ///     as happens in some cases when saving Zip files.
        ///   </para>
        /// </remarks>
        public void Adjust(Int64 delta)
        {
            _bytesWritten -= delta;
            if (_bytesWritten < 0)
                throw new InvalidOperationException();
            if (_s as CountingStream != null)
                ((CountingStream)_s).Adjust(delta);
        }

        /// <summary>
        ///   The read method.
        /// </summary>
        /// <param name="buffer">The buffer to hold the data read from the stream.</param>
        /// <param name="offset">the offset within the buffer to copy the first byte read.</param>
        /// <param name="count">the number of bytes to read.</param>
        /// <returns>the number of bytes read, after decryption and decompression.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _s.Read(buffer, offset, count);
            _bytesRead += n;
            return n;
        }

        /// <summary>
        ///   Write data into the stream.
        /// </summary>
        /// <param name="buffer">The buffer holding data to write to the stream.</param>
        /// <param name="offset">the offset within that data array to find the first byte to write.</param>
        /// <param name="count">the number of bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0) return;
            _s.Write(buffer, offset, count);
            _bytesWritten += count;
        }

        /// <summary>
        ///   Whether the stream can be read.
        /// </summary>
        public override bool CanRead
        {
            get { return _s.CanRead; }
        }

        /// <summary>
        ///   Whether it is possible to call Seek() on the stream.
        /// </summary>
        public override bool CanSeek
        {
            get { return _s.CanSeek; }
        }

        /// <summary>
        ///   Whether it is possible to call Write() on the stream.
        /// </summary>
        public override bool CanWrite
        {
            get { return _s.CanWrite; }
        }

        /// <summary>
        ///   Flushes the underlying stream.
        /// </summary>
        public override void Flush()
        {
            _s.Flush();
        }

        /// <summary>
        ///   The length of the underlying stream.
        /// </summary>
        public override long Length
        {
            get { return _s.Length; }   // bytesWritten??
        }

        /// <summary>
        ///   Returns the sum of number of bytes written, plus the initial
        ///   offset before writing.
        /// </summary>
        public long ComputedPosition
        {
            get { return _initialOffset + _bytesWritten; }
        }


        /// <summary>
        ///   The Position of the stream.
        /// </summary>
        public override long Position
        {
            get { return _s.Position; }
            set
            {
                _s.Seek(value, System.IO.SeekOrigin.Begin);
                // workitem 10178
                SharedUtilities.Workaround_Ladybug318918(_s);
            }
        }

        /// <summary>
        ///   Seek in the stream.
        /// </summary>
        /// <param name="offset">the offset point to seek to</param>
        /// <param name="origin">the reference point from which to seek</param>
        /// <returns>The new position</returns>
        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            return _s.Seek(offset, origin);
        }

        /// <summary>
        ///   Set the length of the underlying stream.  Be careful with this!
        /// </summary>
        ///
        /// <param name='value'>the length to set on the underlying stream.</param>
        public override void SetLength(long value)
        {
            _s.SetLength(value);
        }
    }


}
