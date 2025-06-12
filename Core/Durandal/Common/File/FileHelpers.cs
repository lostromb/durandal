using Durandal.Common.IO;
using Durandal.Common.IO.Crc;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.File
{
    public static class FileHelpers
    {
        /// <summary>
        /// Calculates the CRC32C checksum of a single file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileSystem"></param>
        /// <returns></returns>
        public static async Task<int> CalculateFileCRC32C(VirtualPath fileName, IFileSystem fileSystem)
        {
            if (!fileSystem.Exists(fileName))
            {
                return -1;
            }

            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            using (Stream fileIn = fileSystem.OpenStream(fileName, FileOpenMode.Open, FileAccessMode.Read))
            {
                ICRC32C crc = CRC32CFactory.Create();
                CRC32CState crcState = new CRC32CState();
                int readSize = 1;
                while (readSize > 0)
                {
                    readSize = await fileIn.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length).ConfigureAwait(false);
                    if (readSize > 0)
                    {
                        crc.Slurp(ref crcState, scratch.Buffer.AsSpan(0, readSize));
                    }
                }

                return (int)crcState.Checksum;
            }
        }

        /// <summary>
        /// Copies a single file between file systems. If the target already exists it will be overwritten without warning
        /// </summary>
        /// <param name="source">Source filesystem</param>
        /// <param name="sourceFileName">Source file name</param>
        /// <param name="target">Target filesystem (can be same as source)</param>
        /// <param name="targetFileName">Target file name</param>
        /// <param name="logger">Logger for the operation</param>
        public static bool CopyFile(IFileSystem source, VirtualPath sourceFileName, IFileSystem target, VirtualPath targetFileName, ILogger logger)
        {
            if (!source.Exists(sourceFileName))
            {
                logger.Log("The source file " + sourceFileName.FullName + " doesn't exist!", LogLevel.Wrn);
                return false;
            }

            if (sourceFileName.Equals(targetFileName))
            {
                logger.Log("Copying " + sourceFileName.FullName + "...");
            }
            else
            {
                logger.Log("Copying " + sourceFileName.FullName + " to " + targetFileName.FullName + "...");
            }

            using (Stream readStream = source.OpenStream(sourceFileName, FileOpenMode.Open, FileAccessMode.Read))
            using (Stream writeStream = target.OpenStream(targetFileName, FileOpenMode.Create, FileAccessMode.Write))
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int readSize = 1;
                while (readSize > 0)
                {
                    readSize = readStream.Read(scratch.Buffer, 0, scratch.Buffer.Length);
                    if (readSize > 0)
                    {
                        writeStream.Write(scratch.Buffer, 0, readSize);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Copies a single file between file systems. If the target already exists it will be overwritten without warning
        /// </summary>
        /// <param name="source">Source filesystem</param>
        /// <param name="sourceFileName">Source file name</param>
        /// <param name="target">Target filesystem (can be same as source)</param>
        /// <param name="targetFileName">Target file name</param>
        /// <param name="logger">Logger for the operation</param>
        public static async Task<bool> CopyFileAsync(IFileSystem source, VirtualPath sourceFileName, IFileSystem target, VirtualPath targetFileName, ILogger logger)
        {
            if (!source.Exists(sourceFileName))
            {
                logger.Log("The source file " + sourceFileName.FullName + " doesn't exist!", LogLevel.Wrn);
                return false;
            }

            if (sourceFileName.Equals(targetFileName))
            {
                logger.Log("Copying " + sourceFileName.FullName + "...");
            }
            else
            {
                logger.Log("Copying " + sourceFileName.FullName + " to " + targetFileName.FullName + "...");
            }

            using (Stream readStream = source.OpenStream(sourceFileName, FileOpenMode.Open, FileAccessMode.Read))
            using (Stream writeStream = target.OpenStream(targetFileName, FileOpenMode.Create, FileAccessMode.Write))
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int readSize = 1;
                while (readSize > 0)
                {
                    readSize = await readStream.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length).ConfigureAwait(false);
                    if (readSize > 0)
                    {
                        await writeStream.WriteAsync(scratch.Buffer, 0, readSize).ConfigureAwait(false);
                    }
                }
            }

            return true;
        }

        public static void MoveFile(IFileSystem source, VirtualPath sourceFileName, IFileSystem target, VirtualPath targetFileName, ILogger logger)
        {
            bool copySucceed = CopyFile(source, sourceFileName, target, targetFileName, logger);
            if (!copySucceed)
            {
                return;
            }

            source.Delete(sourceFileName);
        }

        /// <summary>
        /// Copies all files in the directory specified by rootDir between filesystems
        /// </summary>
        /// <param name="source">A resource manager representing the source filesystem</param>
        /// <param name="sourceDir">The directory name to start the copy from.</param>
        /// <param name="target">A resource manager representing the target filesystem</param>
        /// <param name="targetDir">The directory name to output the copy to.</param>
        /// <param name="logger">A logger for the operation</param>
        /// <param name="recursive">If true, recurse into all subdirectories of the root after processing the current one</param>
        public static void CopyAllFiles(IFileSystem source, VirtualPath sourceDir, IFileSystem target, VirtualPath targetDir, ILogger logger, bool recursive = true)
        {
            if (source.Exists(sourceDir))
            {
                if (source.WhatIs(sourceDir) == ResourceType.Directory)
                {
                    foreach (var file in source.ListFiles(sourceDir))
                    {
                        CopyFile(source, file, target, targetDir.Combine(file.Name), logger);
                    }

                    if (recursive)
                    {
                        foreach (var dir in source.ListDirectories(sourceDir))
                        {
                            CopyAllFiles(source, dir, target, targetDir.Combine(dir.Name), logger, true);
                        }
                    }
                }
                else
                {
                    logger.Log("Copy source path " + sourceDir.FullName + " is not a directory!", LogLevel.Err);
                }
            }
            else
            {
                logger.Log("Copy source directory " + sourceDir.FullName + " not found!", LogLevel.Err);
            }
        }

        /// <summary>
        /// Copies all files in the directory specified by rootDir between filesystems
        /// </summary>
        /// <param name="source">A resource manager representing the source filesystem</param>
        /// <param name="sourceDir">The directory name to start the copy from.</param>
        /// <param name="target">A resource manager representing the target filesystem</param>
        /// <param name="targetDir">The directory name to output the copy to.</param>
        /// <param name="logger">A logger for the operation</param>
        /// <param name="recursive">If true, recurse into all subdirectories of the root after processing the current one</param>
        public static async Task CopyAllFilesAsync(IFileSystem source, VirtualPath sourceDir, IFileSystem target, VirtualPath targetDir, ILogger logger, bool recursive = true)
        {
            if (!(await source.ExistsAsync(sourceDir).ConfigureAwait(false)))
            {
                logger.Log("Copy source directory " + sourceDir.FullName + " not found!", LogLevel.Err);
                return;
            }

            if ((await source.WhatIsAsync(sourceDir).ConfigureAwait(false)) != ResourceType.Directory)
            {
                logger.Log("Copy source path " + sourceDir.FullName + " is not a directory!", LogLevel.Err);
                return;
            }

            foreach (var file in source.ListFiles(sourceDir))
            {
                await CopyFileAsync(source, file, target, targetDir.Combine(file.Name), logger).ConfigureAwait(false);
            }

            if (recursive)
            {
                foreach (var dir in source.ListDirectories(sourceDir))
                {
                    await CopyAllFilesAsync(source, dir, target, targetDir.Combine(dir.Name), logger, true).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Deletes a directory and all contents recursively
        /// </summary>
        /// <param name="fileSystem"></param>
        /// <param name="rootDir"></param>
        /// <param name="logger"></param>
        public static void DeleteAllFiles(IFileSystem fileSystem, VirtualPath rootDir, ILogger logger)
        {
            if (fileSystem.Exists(rootDir))
            {
                foreach (var file in fileSystem.ListFiles(rootDir))
                {
                    fileSystem.Delete(file);
                }
                
                foreach (var dir in fileSystem.ListDirectories(rootDir))
                {
                    DeleteAllFiles(fileSystem, dir, logger);
                }

                fileSystem.Delete(rootDir);
            }
            else
            {
                logger.Log("Deletion directory " + rootDir.FullName + " not found!", LogLevel.Wrn);
            }
        }
    }
}
