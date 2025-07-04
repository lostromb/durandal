// ZipEntry.Extract.cs
// ------------------------------------------------------------------
//
// Copyright (c) 2009-2011 Dino Chiesa
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
// last saved (in emacs):
// Time-stamp: <2011-August-06 18:08:21>
//
// ------------------------------------------------------------------
//
// This module defines logic for Extract methods on the ZipEntry class.
//
// ------------------------------------------------------------------


using Durandal.Common.File;
using System;
using System.IO;

namespace Durandal.Common.Compression.Zip
{

    public partial class ZipEntry
    {
        /// <summary>
        ///   Extract the entry to the filesystem, starting at the current
        ///   working directory.
        /// </summary>
        ///
        /// <overloads>
        ///   This method has a bunch of overloads! One of them is sure to
        ///   be the right one for you... If you don't like these, check
        ///   out the <c>ExtractWithPassword()</c> methods.
        /// </overloads>
        ///
        /// <seealso cref="Durandal.Common.Compression.Zip.ZipEntry.ExtractExistingFile"/>
        /// <seealso cref="ZipEntry.Extract(ExtractExistingFileAction)"/>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   This method extracts an entry from a zip file into the current
        ///   working directory.  The path of the entry as extracted is the full
        ///   path as specified in the zip archive, relative to the current
        ///   working directory.  After the file is extracted successfully, the
        ///   file attributes and timestamps are set.
        /// </para>
        ///
        /// <para>
        ///   The action taken when extraction an entry would overwrite an
        ///   existing file is determined by the <see cref="ExtractExistingFile"
        ///   /> property.
        /// </para>
        ///
        /// <para>
        ///   Within the call to <c>Extract()</c>, the content for the entry is
        ///   written into a filesystem file, and then the last modified time of the
        ///   file is set according to the <see cref="LastModified"/> property on
        ///   the entry. See the remarks the <see cref="LastModified"/> property for
        ///   some details about the last modified time.
        /// </para>
        ///
        /// </remarks>
        public void Extract()
        {
            InternalExtractToBaseDir(VirtualPath.Root, null, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extract the entry to a file in the filesystem, using the specified
        ///   behavior when extraction would overwrite an existing file.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   See the remarks on the <see cref="LastModified"/> property, for some
        ///   details about how the last modified time of the file is set after
        ///   extraction.
        /// </para>
        /// </remarks>
        ///
        /// <param name="extractExistingFile">
        ///   The action to take if extraction would overwrite an existing file.
        /// </param>
        public void Extract(ExtractExistingFileAction extractExistingFile)
        {
            ExtractExistingFile = extractExistingFile;
            InternalExtractToBaseDir(VirtualPath.Root, null, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extracts the entry to the specified stream.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   The caller can specify any write-able stream, for example a FileStream, a <see
        ///   cref="System.IO.MemoryStream"/>, or ASP.NET's
        ///   <c>Response.OutputStream</c>.  The content will be decrypted and
        ///   decompressed as necessary. If the entry is encrypted and no password
        ///   is provided, this method will throw.
        /// </para>
        /// <para>
        ///   The position on the stream is not reset by this method before it extracts.
        ///   You may want to call stream.Seek() before calling ZipEntry.Extract().
        /// </para>
        /// </remarks>
        ///
        /// <param name="stream">
        ///   the stream to which the entry should be extracted.
        /// </param>
        ///
        public void Extract(Stream stream)
        {
            InternalExtractToStream(stream, null, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extract the entry to the filesystem, starting at the specified base
        ///   directory.
        /// </summary>
        ///
        /// <param name="baseDirectory">the pathname of the base directory</param>
        ///
        /// <seealso cref="Durandal.Common.Compression.Zip.ZipEntry.ExtractExistingFile"/>
        /// <seealso cref="Durandal.Common.Compression.Zip.ZipEntry.Extract()"/>
        ///
        /// <example>
        /// This example extracts only the entries in a zip file that are .txt files,
        /// into a directory called "textfiles".
        /// <code lang="C#">
        /// using (ZipFile zip = ZipFile.Read("PackedDocuments.zip"))
        /// {
        ///   foreach (string s1 in zip.EntryFilenames)
        ///   {
        ///     if (s1.EndsWith(".txt"))
        ///     {
        ///       zip[s1].Extract("textfiles");
        ///     }
        ///   }
        /// }
        /// </code>
        /// <code lang="VB">
        ///   Using zip As ZipFile = ZipFile.Read("PackedDocuments.zip")
        ///       Dim s1 As String
        ///       For Each s1 In zip.EntryFilenames
        ///           If s1.EndsWith(".txt") Then
        ///               zip(s1).Extract("textfiles")
        ///           End If
        ///       Next
        ///   End Using
        /// </code>
        /// </example>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   Using this method, existing entries in the filesystem will not be
        ///   overwritten. If you would like to force the overwrite of existing
        ///   files, see the <see cref="ExtractExistingFile"/> property, or call
        ///   <see cref="Extract()"/>.
        /// </para>
        ///
        /// <para>
        ///   See the remarks on the <see cref="LastModified"/> property, for some
        ///   details about how the last modified time of the created file is set.
        /// </para>
        /// </remarks>
        public void Extract(VirtualPath baseDirectory)
        {
            InternalExtractToBaseDir(baseDirectory, null, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extract the entry to the filesystem, starting at the specified base
        ///   directory, and using the specified behavior when extraction would
        ///   overwrite an existing file.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   See the remarks on the <see cref="LastModified"/> property, for some
        ///   details about how the last modified time of the created file is set.
        /// </para>
        /// </remarks>
        ///
        /// <example>
        /// <code lang="C#">
        /// String sZipPath = "Airborne.zip";
        /// String sFilePath = "Readme.txt";
        /// String sRootFolder = "Digado";
        /// using (ZipFile zip = ZipFile.Read(sZipPath))
        /// {
        ///   if (zip.EntryFileNames.Contains(sFilePath))
        ///   {
        ///     // use the string indexer on the zip file
        ///     zip[sFileName].Extract(sRootFolder,
        ///                            ExtractExistingFileAction.OverwriteSilently);
        ///   }
        /// }
        /// </code>
        ///
        /// <code lang="VB">
        /// Dim sZipPath as String = "Airborne.zip"
        /// Dim sFilePath As String = "Readme.txt"
        /// Dim sRootFolder As String = "Digado"
        /// Using zip As ZipFile = ZipFile.Read(sZipPath)
        ///   If zip.EntryFileNames.Contains(sFilePath)
        ///     ' use the string indexer on the zip file
        ///     zip(sFilePath).Extract(sRootFolder, _
        ///                            ExtractExistingFileAction.OverwriteSilently)
        ///   End If
        /// End Using
        /// </code>
        /// </example>
        ///
        /// <param name="baseDirectory">the pathname of the base directory</param>
        /// <param name="extractExistingFile">
        /// The action to take if extraction would overwrite an existing file.
        /// </param>
        public void Extract(VirtualPath baseDirectory, ExtractExistingFileAction extractExistingFile)
        {
            ExtractExistingFile = extractExistingFile;
            InternalExtractToBaseDir(baseDirectory, null, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extract the entry to the filesystem, using the current working directory
        ///   and the specified password.
        /// </summary>
        ///
        /// <overloads>
        ///   This method has a bunch of overloads! One of them is sure to be
        ///   the right one for you...
        /// </overloads>
        ///
        /// <seealso cref="Durandal.Common.Compression.Zip.ZipEntry.ExtractExistingFile"/>
        /// <seealso cref="Durandal.Common.Compression.Zip.ZipEntry.ExtractWithPassword(ExtractExistingFileAction, string)"/>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   Existing entries in the filesystem will not be overwritten. If you
        ///   would like to force the overwrite of existing files, see the <see
        ///   cref="Durandal.Common.Compression.Zip.ZipEntry.ExtractExistingFile"/>property, or call
        ///   <see
        ///   cref="ExtractWithPassword(ExtractExistingFileAction,string)"/>.
        /// </para>
        ///
        /// <para>
        ///   See the remarks on the <see cref="LastModified"/> property for some
        ///   details about how the "last modified" time of the created file is
        ///   set.
        /// </para>
        /// </remarks>
        ///
        /// <example>
        ///   In this example, entries that use encryption are extracted using a
        ///   particular password.
        /// <code>
        /// using (var zip = ZipFile.Read(FilePath))
        /// {
        ///     foreach (ZipEntry e in zip)
        ///     {
        ///         if (e.UsesEncryption)
        ///             e.ExtractWithPassword("Secret!");
        ///         else
        ///             e.Extract();
        ///     }
        /// }
        /// </code>
        /// <code lang="VB">
        /// Using zip As ZipFile = ZipFile.Read(FilePath)
        ///     Dim e As ZipEntry
        ///     For Each e In zip
        ///         If (e.UsesEncryption)
        ///           e.ExtractWithPassword("Secret!")
        ///         Else
        ///           e.Extract
        ///         End If
        ///     Next
        /// End Using
        /// </code>
        /// </example>
        /// <param name="password">The Password to use for decrypting the entry.</param>
        public void ExtractWithPassword(string password)
        {
            InternalExtractToBaseDir(VirtualPath.Root, password, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extract the entry to the filesystem, starting at the specified base
        ///   directory, and using the specified password.
        /// </summary>
        ///
        /// <seealso cref="Durandal.Common.Compression.Zip.ZipEntry.ExtractExistingFile"/>
        ///
        /// <remarks>
        /// <para>
        ///   Existing entries in the filesystem will not be overwritten. If you
        ///   would like to force the overwrite of existing files, see the <see
        ///   cref="Durandal.Common.Compression.Zip.ZipEntry.ExtractExistingFile"/>property, or call
        ///   <see
        ///   cref="ExtractWithPassword(ExtractExistingFileAction,string)"/>.
        /// </para>
        ///
        /// <para>
        ///   See the remarks on the <see cref="LastModified"/> property, for some
        ///   details about how the last modified time of the created file is set.
        /// </para>
        /// </remarks>
        ///
        /// <param name="baseDirectory">The pathname of the base directory.</param>
        /// <param name="password">The Password to use for decrypting the entry.</param>
        public void ExtractWithPassword(VirtualPath baseDirectory, string password)
        {
            InternalExtractToBaseDir(baseDirectory, password, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extract the entry to a file in the filesystem, relative to the
        ///   current directory, using the specified behavior when extraction
        ///   would overwrite an existing file.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   See the remarks on the <see cref="LastModified"/> property, for some
        ///   details about how the last modified time of the created file is set.
        /// </para>
        /// </remarks>
        ///
        /// <param name="password">The Password to use for decrypting the entry.</param>
        ///
        /// <param name="extractExistingFile">
        /// The action to take if extraction would overwrite an existing file.
        /// </param>
        public void ExtractWithPassword(ExtractExistingFileAction extractExistingFile, string password)
        {
            ExtractExistingFile = extractExistingFile;
            InternalExtractToBaseDir(VirtualPath.Root, password, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extract the entry to the filesystem, starting at the specified base
        ///   directory, and using the specified behavior when extraction would
        ///   overwrite an existing file.
        /// </summary>
        ///
        /// <remarks>
        ///   See the remarks on the <see cref="LastModified"/> property, for some
        ///   details about how the last modified time of the created file is set.
        /// </remarks>
        ///
        /// <param name="baseDirectory">the pathname of the base directory</param>
        ///
        /// <param name="extractExistingFile">The action to take if extraction would
        /// overwrite an existing file.</param>
        ///
        /// <param name="password">The Password to use for decrypting the entry.</param>
        public void ExtractWithPassword(VirtualPath baseDirectory, ExtractExistingFileAction extractExistingFile, string password)
        {
            ExtractExistingFile = extractExistingFile;
            InternalExtractToBaseDir(baseDirectory, password, _container, _Source, FileName);
        }

        /// <summary>
        ///   Extracts the entry to the specified stream, using the specified
        ///   Password.  For example, the caller could extract to Console.Out, or
        ///   to a MemoryStream.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   The caller can specify any write-able stream, for example a FileStream, a <see
        ///   cref="System.IO.MemoryStream"/>, or ASP.NET's
        ///   <c>Response.OutputStream</c>.  The content will be decrypted and
        ///   decompressed as necessary. If the entry is encrypted and no password
        ///   is provided, this method will throw.
        /// </para>
        /// <para>
        ///   The position on the stream is not reset by this method before it extracts.
        ///   You may want to call stream.Seek() before calling ZipEntry.Extract().
        /// </para>
        /// </remarks>
        ///
        ///
        /// <param name="stream">
        ///   the stream to which the entry should be extracted.
        /// </param>
        /// <param name="password">
        ///   The password to use for decrypting the entry.
        /// </param>
        public void ExtractWithPassword(Stream stream, string password)
        {
            InternalExtractToStream(stream, password, _container, _Source, FileName);
        }

        /// <summary>
        ///   Opens a readable stream corresponding to the zip entry in the
        ///   archive.  The stream decompresses and decrypts as necessary, as it
        ///   is read.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   DotNetZip offers a variety of ways to extract entries from a zip
        ///   file.  This method allows an application to extract an entry by
        ///   reading a <see cref="System.IO.Stream"/>.
        /// </para>
        ///
        /// <para>
        ///   The return value is of type <see
        ///   cref="Durandal.Common.Compression.CrcCalculatorStream"/>.  Use it as you would any
        ///   stream for reading.  When an application calls <see
        ///   cref="Stream.Read(byte[], int, int)"/> on that stream, it will
        ///   receive data from the zip entry that is decrypted and decompressed
        ///   as necessary.
        /// </para>
        ///
        /// <para>
        ///   <c>CrcCalculatorStream</c> adds one additional feature: it keeps a
        ///   CRC32 checksum on the bytes of the stream as it is read.  The CRC
        ///   value is available in the <see
        ///   cref="Durandal.Common.Compression.CrcCalculatorStream.Crc"/> property on the
        ///   <c>CrcCalculatorStream</c>.  When the read is complete, your
        ///   application
        ///   <em>should</em> check this CRC against the <see cref="ZipEntry.Crc"/>
        ///   property on the <c>ZipEntry</c> to validate the content of the
        ///   ZipEntry. You don't have to validate the entry using the CRC, but
        ///   you should, to verify integrity. Check the example for how to do
        ///   this.
        /// </para>
        ///
        /// <para>
        ///   If the entry is protected with a password, then you need to provide
        ///   a password prior to calling <see cref="OpenReader()"/>, either by
        ///   setting the <see cref="Password"/> property on the entry, or the
        ///   <see cref="ZipFile.Password"/> property on the <c>ZipFile</c>
        ///   itself. Or, you can use <see cref="OpenReader(String)" />, the
        ///   overload of OpenReader that accepts a password parameter.
        /// </para>
        ///
        /// <para>
        ///   If you want to extract entry data into a write-able stream that is
        ///   already opened, like a FileStream, do not
        ///   use this method. Instead, use <see cref="Extract(Stream)"/>.
        /// </para>
        ///
        /// <para>
        ///   Your application may use only one stream created by OpenReader() at
        ///   a time, and you should not call other Extract methods before
        ///   completing your reads on a stream obtained from OpenReader().  This
        ///   is because there is really only one source stream for the compressed
        ///   content.  A call to OpenReader() seeks in the source stream, to the
        ///   beginning of the compressed content.  A subsequent call to
        ///   OpenReader() on a different entry will seek to a different position
        ///   in the source stream, as will a call to Extract() or one of its
        ///   overloads.  This will corrupt the state for the decompressing stream
        ///   from the original call to OpenReader().
        /// </para>
        ///
        /// <para>
        ///    The <c>OpenReader()</c> method works only when the ZipEntry is
        ///    obtained from an instance of <c>ZipFile</c>. This method will throw
        ///    an exception if the ZipEntry is obtained from a <see
        ///    cref="ZipInputStream"/>.
        /// </para>
        /// </remarks>
        ///
        /// <example>
        ///   This example shows how to open a zip archive, then read in a named
        ///   entry via a stream. After the read loop is complete, the code
        ///   compares the calculated during the read loop with the expected CRC
        ///   on the <c>ZipEntry</c>, to verify the extraction.
        /// <code>
        /// using (ZipFile zip = new ZipFile(ZipFileToRead))
        /// {
        ///   ZipEntry e1= zip["Elevation.mp3"];
        ///   using (Ionic.Zlib.CrcCalculatorStream s = e1.OpenReader())
        ///   {
        ///     byte[] buffer = new byte[4096];
        ///     int n, totalBytesRead= 0;
        ///     do {
        ///       n = s.Read(buffer,0, buffer.Length);
        ///       totalBytesRead+=n;
        ///     } while (n&gt;0);
        ///      if (s.Crc32 != e1.Crc32)
        ///       throw new Exception(string.Format("The Zip Entry failed the CRC Check. (0x{0:X8}!=0x{1:X8})", s.Crc32, e1.Crc32));
        ///      if (totalBytesRead != e1.UncompressedSize)
        ///       throw new Exception(string.Format("We read an unexpected number of bytes. ({0}!={1})", totalBytesRead, e1.UncompressedSize));
        ///   }
        /// }
        /// </code>
        /// <code lang="VB">
        ///   Using zip As New ZipFile(ZipFileToRead)
        ///       Dim e1 As ZipEntry = zip.Item("Elevation.mp3")
        ///       Using s As Ionic.Zlib.CrcCalculatorStream = e1.OpenReader
        ///           Dim n As Integer
        ///           Dim buffer As Byte() = New Byte(4096) {}
        ///           Dim totalBytesRead As Integer = 0
        ///           Do
        ///               n = s.Read(buffer, 0, buffer.Length)
        ///               totalBytesRead = (totalBytesRead + n)
        ///           Loop While (n &gt; 0)
        ///           If (s.Crc32 &lt;&gt; e1.Crc32) Then
        ///               Throw New Exception(String.Format("The Zip Entry failed the CRC Check. (0x{0:X8}!=0x{1:X8})", s.Crc32, e1.Crc32))
        ///           End If
        ///           If (totalBytesRead &lt;&gt; e1.UncompressedSize) Then
        ///               Throw New Exception(String.Format("We read an unexpected number of bytes. ({0}!={1})", totalBytesRead, e1.UncompressedSize))
        ///           End If
        ///       End Using
        ///   End Using
        /// </code>
        /// </example>
        /// <seealso cref="Durandal.Common.Compression.Zip.ZipEntry.Extract(System.IO.Stream)"/>
        /// <returns>The Stream for reading.</returns>
        public Durandal.Common.Compression.CrcCalculatorStream OpenReader()
        {
            // workitem 10923
            if (_container.ZipFile == null)
                throw new InvalidOperationException("Use OpenReader() only with ZipFile.");

            // use the entry password if it is non-null,
            // else use the zipfile password, which is possibly null
            return InternalOpenReader(_Password ?? _container.Password);
        }

        /// <summary>
        ///   Opens a readable stream for an encrypted zip entry in the archive.
        ///   The stream decompresses and decrypts as necessary, as it is read.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   See the documentation on the <see cref="OpenReader()"/> method for
        ///   full details. This overload allows the application to specify a
        ///   password for the <c>ZipEntry</c> to be read.
        /// </para>
        /// </remarks>
        ///
        /// <param name="password">The password to use for decrypting the entry.</param>
        /// <returns>The Stream for reading.</returns>
        public Durandal.Common.Compression.CrcCalculatorStream OpenReader(string password)
        {
            // workitem 10923
            if (_container.ZipFile == null)
                throw new InvalidOperationException("Use OpenReader() only with ZipFile.");

            return InternalOpenReader(password);
        }

        internal Durandal.Common.Compression.CrcCalculatorStream InternalOpenReader(string password)
        {
            ValidateCompression(_CompressionMethod_FromZipFile, FileName, GetUnsupportedCompressionMethod(_CompressionMethod));
            ValidateEncryption(Encryption, FileName, _UnsupportedAlgorithmId);
            SetupCryptoForExtract(password);

            // workitem 7958
            if (this._Source != ZipEntrySource.ZipFile)
                throw new BadStateException("You must call ZipFile.Save before calling OpenReader");

            // LeftToRead is a count of bytes remaining to be read (out)
            // from the stream AFTER decompression and decryption.
            // It is the uncompressed size, unless ... there is no compression in which
            // case ...?  :< I'm not sure why it's not always UncompressedSize
            var leftToRead = (_CompressionMethod_FromZipFile == (short)CompressionMethod.None)
                ? _CompressedFileDataSize
                : UncompressedSize;

            this.ArchiveStream.Seek(this.FileDataPosition, SeekOrigin.Begin);
            // workitem 10178
            Durandal.Common.Compression.Zip.SharedUtilities.Workaround_Ladybug318918(this.ArchiveStream);

            _inputDecryptorStream = GetExtractDecryptor(ArchiveStream);
            var input3 = GetExtractDecompressor(_inputDecryptorStream);

            return new Durandal.Common.Compression.CrcCalculatorStream(input3, leftToRead);
        }

        void OnExtractProgress(Int64 bytesWritten, Int64 totalBytesToWrite)
        {
            if (_container.ZipFile != null)
            _ioOperationCanceled = _container.ZipFile.OnExtractBlock(this, bytesWritten, totalBytesToWrite);
        }

        static void OnBeforeExtract(ZipEntry zipEntryInstance, VirtualPath path, ZipFile zipFile)
        {
            // When in the context of a ZipFile.ExtractAll, the events are generated from
            // the ZipFile method, not from within the ZipEntry instance. (why?)
            // Therefore we suppress the events originating from the ZipEntry method.
            if (zipFile == null) return;
            if (zipFile._inExtractAll) return;
            // returned boolean is always ignored for all callers of OnBeforeExtract
            zipFile.OnSingleEntryExtract(zipEntryInstance, path, true);
        }

        private void OnAfterExtract(VirtualPath path)
        {
            // When in the context of a ZipFile.ExtractAll, the events are generated from
            // the ZipFile method, not from within the ZipEntry instance. (why?)
            // Therefore we suppress the events originating from the ZipEntry method.
            if (_container.ZipFile == null) return;
            if (_container.ZipFile._inExtractAll) return;
            _container.ZipFile.OnSingleEntryExtract(this, path, false);
        }

        private void OnExtractExisting(VirtualPath path)
        {
            if (_container.ZipFile != null)
                _ioOperationCanceled = _container.ZipFile.OnExtractExisting(this, path);
        }

        private static void ReallyDelete(VirtualPath fileName, IFileSystem fileSystem)
        {
            // workitem 7881
            // reset ReadOnly bit if necessary
            fileSystem.Delete(fileName);
        }


        void WriteStatus(string format, params Object[] args)
        {
            if (_container.ZipFile != null && _container.ZipFile.Verbose)
                _container.ZipFile.StatusMessageTextWriter.WriteLine(format, args);
        }

        /// <summary>
        /// Pass in either basedir or s, but not both.
        /// In other words, you can extract to a stream or to a directory (filesystem), but not both!
        /// The Password param is required for encrypted entries.
        /// </summary>
        void InternalExtractToBaseDir(VirtualPath baseDir, string password, ZipContainer zipContainer, ZipEntrySource zipEntrySource, string fileName)
        {
            if (baseDir == null)
                throw new ArgumentNullException("baseDir");

            // workitem 7958
            if (zipContainer == null)
                throw new BadStateException("This entry is an orphan");

            // workitem 10355
            if (zipContainer.ZipFile == null)
                throw new InvalidOperationException("Use Extract() only with ZipFile.");

            zipContainer.ZipFile.Reset(false);

            if (zipEntrySource != ZipEntrySource.ZipFile)
                throw new BadStateException("You must call ZipFile.Save before calling any Extract method");

            OnBeforeExtract(this, baseDir, zipContainer.ZipFile);

            _ioOperationCanceled = false;

            var fileExistsBeforeExtraction = false;
            var checkLaterForResetDirTimes = false;
            VirtualPath targetFileName = null;
            try
            {
                ValidateCompression(_CompressionMethod_FromZipFile, fileName, GetUnsupportedCompressionMethod(_CompressionMethod));
                ValidateEncryption(Encryption, fileName, _UnsupportedAlgorithmId);

                if (IsDoneWithOutputToBaseDir(baseDir, out targetFileName))
                {
                    WriteStatus("extract dir {0}...", targetFileName);
                    // if true, then the entry was a directory and has been created.
                    // We need to fire the Extract Event.
                    OnAfterExtract(baseDir);
                    return;
                }

                // workitem 10639
                // do we want to extract to a regular filesystem file?
                
                // Check for extracting to a previously existing file. The user
                // can specify bejavior for that case: overwrite, don't
                // overwrite, and throw.  Also, if the file exists prior to
                // extraction, it affects exception handling: whether to delete
                // the target of extraction or not. This check needs to be done
                // before the password check is done, because password check may
                // throw a BadPasswordException, which triggers the catch,
                // wherein the existing file may be deleted if not flagged as
                // pre-existing.
                if (_fileSystem.Exists(targetFileName))
                {
                    fileExistsBeforeExtraction = true;
                    int rc = CheckExtractExistingFile(baseDir, targetFileName);
                    if (rc == 2) goto ExitTry; // cancel
                    if (rc == 1) return; // do not overwrite
                }

                // If no password explicitly specified, use the password on the entry itself,
                // or on the zipfile itself.
                if (_Encryption_FromZipFile != EncryptionAlgorithm.None)
                    EnsurePassword(password);

                // set up the output stream
                string tmpName = SharedUtilities.InternalGetTempFileName();
                VirtualPath tmpPath = targetFileName.Container.Combine(tmpName);
                WriteStatus("extract file {0}...", targetFileName);

                using (var output = OpenFileStream(tmpPath, ref checkLaterForResetDirTimes))
                {
                    if (ExtractToStream(ArchiveStream, output, Encryption, _Crc32))
                        goto ExitTry;

                    output.Dispose();
                }

                MoveFileInPlace(fileExistsBeforeExtraction, targetFileName, tmpPath, checkLaterForResetDirTimes);

                OnAfterExtract(baseDir);

                ExitTry: ;
            }
            catch (Exception)
            {
                _ioOperationCanceled = true;
                throw;
            }
            finally
            {
                if (_ioOperationCanceled && targetFileName != null)
                {
                    // An exception has occurred. If the file exists, check
                    // to see if it existed before we tried extracting.  If
                    // it did not, attempt to remove the target file. There
                    // is a small possibility that the existing file has
                    // been extracted successfully, overwriting a previously
                    // existing file, and an exception was thrown after that
                    // but before final completion (setting times, etc). In
                    // that case the file will remain, even though some
                    // error occurred.  Nothing to be done about it.
                    if (_fileSystem.Exists(targetFileName) && !fileExistsBeforeExtraction)
                        _fileSystem.Delete(targetFileName);
                }
            }
        }

        /// <summary>
        /// Extract to a stream
        /// In other words, you can extract to a stream or to a directory (filesystem), but not both!
        /// The Password param is required for encrypted entries.
        /// </summary>
        void InternalExtractToStream(Stream outStream, string password, ZipContainer zipContainer, ZipEntrySource zipEntrySource, string fileName)
        {
            // workitem 7958
            if (zipContainer == null)
                throw new BadStateException("This entry is an orphan");

            // workitem 10355
            if (zipContainer.ZipFile == null)
                throw new InvalidOperationException("Use Extract() only with ZipFile.");

            zipContainer.ZipFile.Reset(false);

            if (zipEntrySource != ZipEntrySource.ZipFile)
                throw new BadStateException("You must call ZipFile.Save before calling any Extract method");

            OnBeforeExtract(this, null, zipContainer.ZipFile);

            _ioOperationCanceled = false;

            try
            {
                ValidateCompression(_CompressionMethod_FromZipFile, fileName, GetUnsupportedCompressionMethod(_CompressionMethod));
                ValidateEncryption(Encryption, fileName, _UnsupportedAlgorithmId);

                if (IsDoneWithOutputToStream())
                {
                    WriteStatus("extract dir {0}...", null);
                    // if true, then the entry was a directory and has been created.
                    // We need to fire the Extract Event.
                    OnAfterExtract(null);
                    return;
                }

                // If no password explicitly specified, use the password on the entry itself,
                // or on the zipfile itself.
                if (_Encryption_FromZipFile != EncryptionAlgorithm.None)
                    EnsurePassword(password);

                WriteStatus("extract entry {0} to stream...", fileName);

                var archiveStream = ArchiveStream;

                if (ExtractToStream(archiveStream, outStream, Encryption, _Crc32))
                    goto ExitTry;

                OnAfterExtract(null);

                ExitTry: ;
            }
            catch (Exception)
            {
                _ioOperationCanceled = true;
                throw;
            }
        }

        bool ExtractToStream(Stream archiveStream, Stream output, EncryptionAlgorithm encryptionAlgorithm, int expectedCrc32)
        {
            if (_ioOperationCanceled)
                return true;

            try
            {
                var calculatedCrc32 = ExtractAndCrc(archiveStream, output,
                    _CompressionMethod_FromZipFile, _CompressedFileDataSize,
                    UncompressedSize);

                if (_ioOperationCanceled)
                    return true;

                VerifyCrcAfterExtract(calculatedCrc32, encryptionAlgorithm, expectedCrc32, archiveStream, UncompressedSize);
                return false;
            }
            finally
            {
                var zss = archiveStream as ZipSegmentedStream;
                if (zss != null)
                {
                    // need to dispose it
                    zss.Dispose();
                    _archiveStream = null;
                }
            }
        }

        void MoveFileInPlace(
            bool fileExistsBeforeExtraction,
            VirtualPath targetFileName,
            VirtualPath tmpPath, bool checkLaterForResetDirTimes)
        {
            // workitem 10639
            // move file to permanent home
            VirtualPath zombie = null;

            if (fileExistsBeforeExtraction)
            {
                // An AV program may hold the target file open, which means
                // File.Delete() will succeed, though the actual deletion
                // remains pending. This will prevent a subsequent
                // File.Move() from succeeding. To avoid this, when the file
                // already exists, we need to replace it in 3 steps:
                //
                //     1. rename the existing file to a zombie name;
                //     2. rename the extracted file from the temp name to
                //        the target file name;
                //     3. delete the zombie.
                //
                zombie = targetFileName + Path.GetRandomFileName() + ".PendingOverwrite";
                FileHelpers.MoveFile(_fileSystem, targetFileName, _fileSystem, zombie, _logger);
            }

            FileHelpers.MoveFile(_fileSystem, tmpPath, _fileSystem, targetFileName, _logger);
            _SetTimes(targetFileName, true);

            if (zombie != null && _fileSystem.Exists(zombie))
                ReallyDelete(zombie, _fileSystem);

            // workitem 8264
            if (checkLaterForResetDirTimes)
            {
                // This is sort of a hack.  What I do here is set the time on
                // the parent directory, every time a file is extracted into
                // it.  If there is a directory with 1000 files, then I set
                // the time on the dir, 1000 times. This allows the directory
                // to have times that reflect the actual time on the entry in
                // the zip archive.

                // String.Contains is not available on .NET CF 2.0
                if (FileName.IndexOf('/') != -1)
                {
                    var dirname = Path.GetDirectoryName(FileName);
                    if (_container.ZipFile[dirname] == null)
                        _SetTimes(targetFileName.Container, false);
                }
            }
            
            // workitem 7926 - version made by OS can be zero (FAT) or 10 (NTFS)
            //if ((_VersionMadeBy & 0xFF00) == 0x0a00 || (_VersionMadeBy & 0xFF00) == 0x0000)
            //    NetCfFile.SetAttributes(targetFileName, (uint)_ExternalFileAttrs); // LOGAN STUBBED OUT
        }

        void EnsurePassword(string password)
        {
            var p = password ?? _Password ?? _container.Password;
            if (p == null) throw new BadPasswordException();
            SetupCryptoForExtract(p);
        }

        Stream OpenFileStream(VirtualPath tmpPath, ref bool checkLaterForResetDirTimes)
        {
            VirtualPath dirName = tmpPath.Container;
            // ensure the target path exists
            if (!_fileSystem.Exists(dirName))
            {
                // we create the directory here, but we do not set the
                // create/modified/accessed times on it because it is being
                // created implicitly, not explcitly. There's no entry in the
                // zip archive for the directory.
                //_fileSystem.CreateDirectory(dirName);
            }
            else
            {
                // workitem 8264
                if (_container.ZipFile != null)
                    checkLaterForResetDirTimes = _container.ZipFile._inExtractAll;
            }

            // File.Create(CreateNew) will overwrite any existing file.
            if (_fileSystem.Exists(tmpPath))
            {
                _fileSystem.Delete(tmpPath);
            }

            return _fileSystem.OpenStream(tmpPath, FileOpenMode.CreateNew, FileAccessMode.ReadWrite);// new FileStream(tmpPath, FileMode.CreateNew);
        }

        internal void VerifyCrcAfterExtract(Int32 calculatedCrc32, EncryptionAlgorithm encryptionAlgorithm, int expectedCrc32, Stream archiveStream, long uncompressedSize)
        {
            if (calculatedCrc32 != expectedCrc32)
                throw new BadCrcException("CRC error: the file being extracted appears to be corrupted. " +
                                          String.Format("Expected 0x{0:X8}, Actual 0x{1:X8}", expectedCrc32, calculatedCrc32));
        }

        int CheckExtractExistingFile(VirtualPath baseDir, VirtualPath targetFileName)
        {
            int loop = 0;
            // returns: 0 == extract, 1 = don't, 2 = cancel
            do
            {
                switch (ExtractExistingFile)
                {
                    case ExtractExistingFileAction.OverwriteSilently:
                        WriteStatus("the file {0} exists; will overwrite it...", targetFileName);
                        return 0;

                    case ExtractExistingFileAction.DoNotOverwrite:
                        WriteStatus("the file {0} exists; not extracting entry...", FileName);
                        OnAfterExtract(baseDir);
                        return 1;

                    case ExtractExistingFileAction.InvokeExtractProgressEvent:
                        if (loop>0)
                            throw new ZipException(String.Format("The file {0} already exists.", targetFileName));
                        OnExtractExisting(baseDir);
                        if (_ioOperationCanceled)
                            return 2;

                        // loop around
                        break;

                    case ExtractExistingFileAction.Throw:
                    default:
                        throw new ZipException(String.Format("The file {0} already exists.", targetFileName));
                }
                loop++;
            }
            while (true);
        }

        void _CheckRead(int nbytes)
        {
            if (nbytes == 0)
                throw new BadReadException(String.Format("bad read of entry {0} from compressed archive.",
                             FileName));
        }

        Stream _inputDecryptorStream;

        int ExtractAndCrc(Stream archiveStream, Stream targetOutput,
            short compressionMethod,
            long compressedFileDataSize,
            long uncompressedSize)
        {
            int crcResult;
            var input = archiveStream;

            // change for workitem 8098
            input.Seek(FileDataPosition, SeekOrigin.Begin);
            // workitem 10178
            SharedUtilities.Workaround_Ladybug318918(input);

            var bytes = new byte[BufferSize];

            // The extraction process varies depending on how the entry was
            // stored.  It could have been encrypted, and it coould have
            // been compressed, or both, or neither. So we need to check
            // both the encryption flag and the compression flag, and take
            // the proper action in all cases.

            var leftToRead = (compressionMethod != (short)CompressionMethod.None)
                ? uncompressedSize
                : compressedFileDataSize;

            // Get a stream that either decrypts or not.
            _inputDecryptorStream = GetExtractDecryptor(input);

            var input3 = GetExtractDecompressor(_inputDecryptorStream);

            var bytesWritten = 0L;
            // As we read, we maybe decrypt, and then we maybe decompress. Then we write.
            using (var s1 = new Durandal.Common.Compression.CrcCalculatorStream(input3))
            {
                while (leftToRead > 0)
                {
                    //Console.WriteLine("ExtractOne: LeftToRead {0}", LeftToRead);

                    // Casting LeftToRead down to an int is ok here in the else clause,
                    // because that only happens when it is less than bytes.Length,
                    // which is much less than MAX_INT.
                    int len = (leftToRead > bytes.Length) ? bytes.Length : (int)leftToRead;
                    int n = s1.Read(bytes, 0, len);

                    // must check data read - essential for detecting corrupt zip files
                    _CheckRead(n);

                    targetOutput.Write(bytes, 0, n);
                    leftToRead -= n;
                    bytesWritten += n;

                    // fire the progress event, check for cancels
                    OnExtractProgress(bytesWritten, uncompressedSize);

                    if (_ioOperationCanceled)
                        break;
                }

                crcResult = s1.Crc;
            }

            return crcResult;
        }

        Stream GetExtractDecompressor(Stream input2)
        {
            if (input2 == null) throw new ArgumentNullException("input2");

            // get a stream that either decompresses or not.
            switch (_CompressionMethod_FromZipFile)
            {
                case (short)CompressionMethod.None:
                    return input2;
                case (short)CompressionMethod.Deflate:
                    return new ZLib.DeflateStream(input2, ZLib.CompressionMode.Decompress, true);
                case (short)CompressionMethod.BZip2:
                    return new BZip2.BZip2InputStream(input2, true);
            }

            throw new Exception(string.Format("Failed to find decompressor matching {0}",
                _CompressionMethod_FromZipFile));
        }

        Stream GetExtractDecryptor(Stream input)
        {
            if (input == null) throw new ArgumentNullException("input");

            Stream input2;
            if (_Encryption_FromZipFile == EncryptionAlgorithm.PkzipWeak)
                input2 = new ZipCipherStream(input, _zipCrypto_forExtract, CryptoMode.Decrypt);
            else
                input2 = input;

            return input2;
        }




        internal void _SetTimes(VirtualPath fileOrDirectory, bool isFile)
        {
            //// workitem 8807:
            //// Because setting the time is not considered to be a fatal error,
            //// and because other applications can interfere with the setting
            //// of a time on a directory, we're going to swallow IO exceptions
            //// in this method.

            //try
            //{
            //    if (_ntfsTimesAreSet)
            //    {
            //        // workitem 7944: set time should not be a fatal error on CF
            //        int rc = NetCfFile.SetTimes(fileOrDirectory, _Ctime, _Atime, _Mtime);
            //        if ( rc != 0)
            //        {
            //            WriteStatus("Warning: SetTimes failed.  entry({0})  file({1})  rc({2})",
            //                        FileName, fileOrDirectory, rc);
            //        }
            //    }
            //    else
            //    {
            //        // workitem 6191
            //        DateTime AdjustedLastModified = Durandal.Common.Compression.Zip.SharedUtilities.AdjustTime_Reverse(LastModified);
                    
            //        int rc = NetCfFile.SetLastWriteTime(fileOrDirectory, AdjustedLastModified);

            //        if ( rc != 0)
            //        {
            //            WriteStatus("Warning: SetLastWriteTime failed.  entry({0})  file({1})  rc({2})",
            //                        FileName, fileOrDirectory, rc);
            //        }
            //    }
            //}
            //catch (System.IO.IOException ioexc1)
            //{
            //    WriteStatus("failed to set time on {0}: {1}", fileOrDirectory, ioexc1.Message);
            //}
        }


        #region Support methods

        // workitem 7968

        static string GetUnsupportedAlgorithm(uint unsupportedAlgorithmId)
        {
            string alg;
            switch (unsupportedAlgorithmId)
            {
                case 0:
                    alg = "--";
                    break;
                case 0x6601:
                    alg = "DES";
                    break;
                case 0x6602: // - RC2 (version needed to extract < 5.2)
                    alg = "RC2";
                    break;
                case 0x6603: // - 3DES 168
                    alg = "3DES-168";
                    break;
                case 0x6609: // - 3DES 112
                    alg = "3DES-112";
                    break;
                case 0x660E: // - AES 128
                    alg = "PKWare AES128";
                    break;
                case 0x660F: // - AES 192
                    alg = "PKWare AES192";
                    break;
                case 0x6610: // - AES 256
                    alg = "PKWare AES256";
                    break;
                case 0x6702: // - RC2 (version needed to extract >= 5.2)
                    alg = "RC2";
                    break;
                case 0x6720: // - Blowfish
                    alg = "Blowfish";
                    break;
                case 0x6721: // - Twofish
                    alg = "Twofish";
                    break;
                case 0x6801: // - RC4
                    alg = "RC4";
                    break;
                case 0xFFFF: // - Unknown algorithm
                default:
                    alg = String.Format("Unknown (0x{0:X4})", unsupportedAlgorithmId);
                    break;
            }
            return alg;
        }

        // workitem 7968

        static string GetUnsupportedCompressionMethod(short compressionMethod)
        {
            string meth;
            switch ((int) compressionMethod)
            {
                case 0:
                    meth = "Store";
                    break;
                case 1:
                    meth = "Shrink";
                    break;
                case 8:
                    meth = "DEFLATE";
                    break;
                case 9:
                    meth = "Deflate64";
                    break;
                case 12:
                    meth = "BZIP2"; // only if BZIP not compiled in
                    break;
                case 14:
                    meth = "LZMA";
                    break;
                case 19:
                    meth = "LZ77";
                    break;
                case 98:
                    meth = "PPMd";
                    break;
                default:
                    meth = String.Format("Unknown (0x{0:X4})", compressionMethod);
                    break;
            }
            return meth;
        }

        static void ValidateEncryption(EncryptionAlgorithm encryptionAlgorithm, string fileName, uint unsupportedAlgorithmId)
        {
            if (encryptionAlgorithm != EncryptionAlgorithm.PkzipWeak &&
                encryptionAlgorithm != EncryptionAlgorithm.None)
            {
                // workitem 7968
                if (unsupportedAlgorithmId != 0)
                    throw new ZipException(string.Format("Cannot extract: Entry {0} is encrypted with an algorithm not supported by DotNetZip: {1}",
                                                         fileName, GetUnsupportedAlgorithm(unsupportedAlgorithmId)));
                throw new ZipException(string.Format("Cannot extract: Entry {0} uses an unsupported encryption algorithm ({1:X2})",
                                                     fileName, (int)encryptionAlgorithm));
            }
        }

        static void ValidateCompression(short compressionMethod, string fileName, string compressionMethodName)
        {
            if ((compressionMethod != (short)CompressionMethod.None) &&
                (compressionMethod != (short)CompressionMethod.Deflate)
                && (compressionMethod != (short)CompressionMethod.BZip2)
                )
                throw new ZipException(String.Format("Entry {0} uses an unsupported compression method (0x{1:X2}, {2})",
                                                          fileName, compressionMethod, compressionMethodName));
        }


        void SetupCryptoForExtract(string password)
        {
            //if (password == null) return;
            if (_Encryption_FromZipFile == EncryptionAlgorithm.None) return;

            if (_Encryption_FromZipFile == EncryptionAlgorithm.PkzipWeak)
            {
                if (password == null)
                    throw new ZipException("Missing password.");

                this.ArchiveStream.Seek(this.FileDataPosition - 12, SeekOrigin.Begin);
                // workitem 10178
                Durandal.Common.Compression.Zip.SharedUtilities.Workaround_Ladybug318918(this.ArchiveStream);
                _zipCrypto_forExtract = ZipCrypto.ForRead(password, this);
            }
            
        }



        /// <summary>
        /// Validates that the args are consistent; returning whether the caller can return
        /// because it's done, or not (caller should continue)
        /// </summary>
        bool IsDoneWithOutputToBaseDir(VirtualPath baseDir, out VirtualPath outFileName)
        {
            if (baseDir == null) throw new ArgumentNullException("baseDir");
            // Sometimes the name on the entry starts with a slash.
            // Rather than unpack to the root of the volume, we're going to
            // drop the slash and unpack to the specified base directory.
            var f = FileName.Replace('\\', '/');

            // workitem 11772: remove drive letter with separator
            if (f.IndexOf(':') == 1)
                f = f.Substring(2);

            if (f.StartsWith("/"))
                f = f.Substring(1);

            f = SharedUtilities.SanitizePath(f);

            // String.Contains is not available on .NET CF 2.0
            outFileName = _container.ZipFile.FlattenFoldersOnExtract
                ? baseDir.Combine((f.IndexOf('/') != -1 ? Path.GetFileName(f) : f))
                : baseDir.Combine(f);

            // workitem 10639
            // outFileName = outFileName.Replace('/', Path.DirectorySeparatorChar);

            // check if it is a directory
            if (IsDirectory || FileName.EndsWith("/"))
            {
                if (!_fileSystem.Exists(outFileName))
                {
                    _fileSystem.CreateDirectory(outFileName);
                    _SetTimes(outFileName, false);
                }
                else
                {
                    // the dir exists, maybe we want to overwrite times.
                    if (ExtractExistingFile == ExtractExistingFileAction.OverwriteSilently)
                        _SetTimes(outFileName, false);
                }
                return true;  // true == all done, caller will return
            }
            return false;  // false == work to do by caller.
        }

        /// <summary>
        /// Validates that the args are consistent; returning whether the caller can return
        /// because it's done, or not (caller should continue)
        /// </summary>
        bool IsDoneWithOutputToStream()
        {
            return IsDirectory || FileName.EndsWith("/");
        }

        #endregion

    }
}
