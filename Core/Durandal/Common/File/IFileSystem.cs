namespace Durandal.Common.File
{
    using System;
    using System.Collections.Generic;
    using Durandal.Common.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// This interface provides a way for a program to access "files"
    /// (usually real, but sometimes fake) in a way that is actually abstracted above the file interface.
    /// This is designed for portability, so that environments which do not allow raw
    /// disk access can still be refactored easily to use some alternative such as in-memory filesystem.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Opens a stream for reading / writing to a specific file.
        /// Throws an exception if the file is not present or is a directory.
        /// </summary>
        /// <param name="file">The file to access or create</param>
        /// <param name="openMode">Specify the behavior of the file system regarding how to handle preexisting or nonexisting files, whether to expect one, create a new one, etc.</param>
        /// <param name="accessMode">Specify the type of access required by the stream, read or write. Both read + write can be specified to create a random-access read/write stream, but not all filesystems support it</param>
        /// <param name="bufferSizeHint">An optional hint for the stream's buffer size, in bytes</param>
        /// <returns>A file access stream</returns>
        NonRealTimeStream OpenStream(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null);

        /// <summary>
        /// Opens an async stream for reading / writing to a specific file.
        /// Throws an exception if the file is not present or is a directory.
        /// </summary>
        /// <param name="fileParameters">The set of input parameters.</param>
        /// <returns>A file access stream</returns>
        NonRealTimeStream OpenStream(FileStreamParams fileParameters);

        /// <summary>
        /// Opens a stream for reading / writing to a specific file as an asynchronous operation.
        /// </summary>
        /// <param name="file">The file to access or create</param>
        /// <param name="openMode">Specify the behavior of the file system regarding how to handle preexisting or nonexisting files, whether to expect one, create a new one, etc.</param>
        /// <param name="accessMode">Specify the type of access required by the stream, read or write. Both read + write can be specified to create a random-access read/write stream, but not all filesystems support it</param>
        /// <param name="bufferSizeHint">An optional hint for the stream's buffer size, in bytes</param>
        /// <returns>A file access stream</returns>
        Task<NonRealTimeStream> OpenStreamAsync(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null);

        /// <summary>
        /// Opens a stream for reading / writing to a specific file as an asynchronous operation.
        /// </summary>
        /// <param name="fileParameters">The set of input parameters.</param>
        /// <returns>A file access stream</returns>
        Task<NonRealTimeStream> OpenStreamAsync(FileStreamParams fileParameters);

        /// <summary>
        /// Writes a set of strings as text to a file
        /// </summary>
        /// <param name="targetFile">The file to write to. If one already exists it will be overwritten.</param>
        /// <param name="data">The set of strings to write.</param>
        void WriteLines(VirtualPath targetFile, IEnumerable<string> data);

        /// <summary>
        /// Writes a set of strings as text to a file as an asynchronous operation
        /// </summary>
        /// <param name="targetFile">The file to write to. If one already exists it will be overwritten.</param>
        /// <param name="data">The set of strings to write.</param>
        /// <returns>An async task.</returns>
        Task WriteLinesAsync(VirtualPath targetFile, IEnumerable<string> data);

        /// <summary>
        /// Reads a file as a set of strings, one string per line.
        /// </summary>
        /// <param name="sourceFile">The file to read from.</param>
        /// <returns>A set of strings read from the file.</returns>
        IReadOnlyCollection<string> ReadLines(VirtualPath sourceFile);

        /// <summary>
        /// Reads a file as a set of strings, one string per line, as an asynchronous operation
        /// </summary>
        /// <param name="sourceFile">The file to read from.</param>
        /// <returns>A set of strings read from the file</returns>
        Task<IReadOnlyCollection<string>> ReadLinesAsync(VirtualPath sourceFile);

        /// <summary>
        /// Deletes the file or directory.
        /// If this path is a directory, delete the directory recursively.
        /// If the file does not exist or deletion fails, return false
        /// </summary>
        /// <param name="path">The file or directory to delete.</param>
        /// <returns>True if deletion succeeded</returns>
        bool Delete(VirtualPath path);

        /// <summary>
        /// Deletes the file or directory asynchronously.
        /// If this path is a directory, delete the directory recursively.
        /// If the file does not exist or deletion fails, return false
        /// </summary>
        /// <param name="path">The file or directory to delete.</param>
        /// <returns>True if deletion succeeded</returns>
        Task<bool> DeleteAsync(VirtualPath path);

        /// <summary>
        /// Checks if a file or directory exists.
        /// </summary>
        /// <param name="path">The path to check for.</param>
        /// <returns>True if a file or directory exists with this path.</returns>
        bool Exists(VirtualPath path);

        /// <summary>
        /// Checks if a file or directory exists as an async operation.
        /// </summary>
        /// <param name="path">The path to check for.</param>
        /// <returns>True if a file or directory exists with this path.</returns>
        Task<bool> ExistsAsync(VirtualPath path);

        /// <summary>
        /// Determines what type of resource is referred to by the specified path.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>File for files, Directory for directories, or Unknown if the path was not found.</returns>
        ResourceType WhatIs(VirtualPath path);

        /// <summary>
        /// Determines what type of resource is referred to by the specified path, as an async operation.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>File for files, Directory for directories, or Unknown if the path was not found.</returns>
        Task<ResourceType> WhatIsAsync(VirtualPath path);

        /// <summary>
        /// If the given path is a directory, list files immediately within that directory.
        /// Otherwise return an empty enumerable.
        /// </summary>
        /// <param name="directoryName"></param>
        /// <returns></returns>
        IEnumerable<VirtualPath> ListFiles(VirtualPath directoryName);

        /// <summary>
        /// If the given path is a directory, list files immediately within that directory.
        /// Otherwise return an empty enumerable.
        /// </summary>
        /// <param name="directoryName"></param>
        /// <returns></returns>
        Task<IEnumerable<VirtualPath>> ListFilesAsync(VirtualPath directoryName);

        /// <summary>
        /// If the given path is a directory, list subdirectories immediately within that directory
        /// FIXME what happens if it's not a directory?
        /// </summary>
        /// <param name="directoryName"></param>
        /// <returns></returns>
        IEnumerable<VirtualPath> ListDirectories(VirtualPath directoryName);

        /// <summary>
        /// If the given path is a directory, list subdirectories immediately within that directory
        /// FIXME what happens if it's not a directory?
        /// </summary>
        /// <param name="directoryName"></param>
        /// <returns></returns>
        Task<IEnumerable<VirtualPath>> ListDirectoriesAsync(VirtualPath directoryName);

        /// <summary>
        /// Returns metadata information about the specified file, if it exists.
        /// If the file does not exist, return null.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>File information for the file, or null if the file does not exist</returns>
        FileStat Stat(VirtualPath fileName);

        /// <summary>
        /// Returns metadata information about the specified file, if it exists.
        /// If the file does not exist, return null.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>File information for the file, or null if the file does not exist</returns>
        Task<FileStat> StatAsync(VirtualPath fileName);

        /// <summary>
        /// Overwrites file creation time and/or modification time for the specified file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="newCreationTime">If non-null, set the file creation time to this value.</param>
        /// <param name="newModificationTime">If non-null, set the file modification time to this value.</param>
        void WriteStat(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime);

        /// <summary>
        /// Overwrites file creation time and/or modification time for the specified file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="newCreationTime">If non-null, set the file creation time to this value.</param>
        /// <param name="newModificationTime">If non-null, set the file modification time to this value.</param>
        Task WriteStatAsync(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime);

        /// <summary>
        /// Ensures that a directory exists with the specified path, creating them if necessary
        /// </summary>
        /// <param name="directory"></param>
        void CreateDirectory(VirtualPath directory);

        /// <summary>
        /// Ensures that a directory exists with the specified path, creating them if necessary
        /// </summary>
        /// <param name="directory"></param>
        Task CreateDirectoryAsync(VirtualPath directory);

        /// <summary>
        /// Move the specified file or directory to a different location within the same filesystem.
        /// If the source file is not found, or the target file already exists, this will throw an exception.
        /// </summary>
        /// <param name="source">The file or directory to be moved</param>
        /// <param name="target">The new desired name of the file or directory</param>
        /// <returns>True if the file system changed successfully</returns>
        void Move(VirtualPath source, VirtualPath target);

        /// <summary>
        /// Move the specified file or directory to a different location within the same filesystem.
        /// If the source file is not found, or the target file already exists, this will throw an exception.
        /// </summary>
        /// <param name="source">The file or directory to be moved</param>
        /// <param name="target">The new desired name of the file or directory</param>
        /// <returns>True if the file system changed successfully</returns>
        Task MoveAsync(VirtualPath source, VirtualPath target);

        /// <summary>
        /// Creates a watcher object that can monitor for changes to files within a directory and/or its subdirectories.
        /// Not implemented on all filesystems. Also, it does not currently monitor modification of directories, only files.
        /// </summary>
        /// <param name="watchPath">The virtual path of an (existing) directory to be monitored.</param>
        /// <param name="filter">The filter for file names to be monitored. Default is empty string for "all files". Also acceptable are wildcards ("*", "*.txt") as well as an explicit file name.</param>
        /// <param name="recurseSubdirectories">If true, also monitor for changes within all subdirectories of the watch directory.</param>
        /// <returns></returns>
        Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories);
    }
}
