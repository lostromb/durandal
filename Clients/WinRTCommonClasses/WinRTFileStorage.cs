using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using System.IO;
using Windows.Foundation;
using System.Diagnostics;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Windows.Storage.FileProperties;
using Durandal.Common.Logger;
using Durandal.Common.File;
using Durandal.Common.Utils;

namespace DurandalWinRT
{
    public class WinRTFileStorage : AbstractFileSystem
    {
        private readonly StorageFolder _root;
        private readonly ILogger _logger;

        public WinRTFileStorage(StorageFolder rootFolder, ILogger logger)
        {
            _root = rootFolder;
            _logger = logger;
        }
        
        public override async Task<Stream> OpenStreamAsync(VirtualPath file, FileOpenMode openMode, Durandal.Common.File.FileAccessMode accessMode)
        {
            bool alreadyExists = await ExistsAsync(file).ConfigureAwait(false);
            if (accessMode == Durandal.Common.File.FileAccessMode.Read)
            {
                if (!alreadyExists)
                {
                    throw new FileNotFoundException("File not found", file.FullName);
                }
                
                IStorageFile rtFile = await ResolvePathToFile(file).ConfigureAwait(false);
                return await rtFile.OpenStreamForReadAsync().ConfigureAwait(false);
            }
            else if (accessMode == Durandal.Common.File.FileAccessMode.Write)
            {
                // Ensure target directory exists
                //_logger.Log("Creating subdirectories for write stream " + file.FullName);
                await CreateDirectoryAsync(file.Container).ConfigureAwait(false);
                StorageFolder datafolder = await ResolveContainer(file.Container);
                //_logger.Log("Creating file " + file.FullName);
                StorageFile rtFile = await datafolder.CreateFileAsync(file.Name, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                //_logger.Log("Opening write stream " + file.FullName);
                return await rtFile.OpenStreamForWriteAsync().ConfigureAwait(false);
            }
            else if (accessMode == Durandal.Common.File.FileAccessMode.ReadWrite)
            {
                // Ensure target directory exists
                //_logger.Log("Creating subdirectories for write stream " + file.FullName);
                await CreateDirectoryAsync(file.Container).ConfigureAwait(false);
                IStorageFile rtFile;
                if (!alreadyExists)
                {
                    if (openMode == FileOpenMode.Open || openMode == FileOpenMode.OpenOrCreate)
                    {
                        throw new FileNotFoundException("File not found and flags prohibit creating a new one for writing", file.FullName);
                    }

                    // Create a new file
                    StorageFolder datafolder = await ResolveContainer(file.Container).ConfigureAwait(false);
                    rtFile = await datafolder.CreateFileAsync(file.Name, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                }
                else
                {
                    if (openMode == FileOpenMode.CreateNew)
                    {
                        throw new IOException("Expected to create a new file " + file.FullName + " but one with that name already exists!");
                    }

                    rtFile = await ResolvePathToFile(file).ConfigureAwait(false);
                }

                //_logger.Log("Opening readwrite stream " + file.FullName);
                var stream = await rtFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false);
                return stream.AsStream();
            }
            else
            {
                throw new ArgumentException("Unknown read mode " + accessMode);
            }
        }

        public override async Task DeleteAsync(VirtualPath path)
        {
            IStorageItem item = await ResolvePathToItem(path).ConfigureAwait(false);
            if (item != null)
            {
                await item.DeleteAsync().AsTask().ConfigureAwait(false);
            }
        }

        public override async Task<bool> ExistsAsync(VirtualPath path)
        {
            IStorageItem item = await ResolvePathToItem(path).ConfigureAwait(false);
            return item != null;
        }

        public override async Task<ResourceType> WhatIsAsync(VirtualPath resourceName)
        {
            IStorageItem item = await ResolvePathToItem(resourceName).ConfigureAwait(false);
            if (item == null)
            {
                return ResourceType.Unknown;
            }
            else if (item.IsOfType(StorageItemTypes.File))
            {
                return ResourceType.File;
            }
            else if (item.IsOfType(StorageItemTypes.Folder))
            {
                return ResourceType.Directory;
            }
            else
            {
                return ResourceType.Unknown;
            }
        }

        public override async Task<IEnumerable<VirtualPath>> ListFilesAsync(VirtualPath directoryName)
        {
            StorageFolder item = await ResolveContainer(directoryName).ConfigureAwait(false);
            //_logger.Log("Enumerating files " + directoryName.FullName);
            List<VirtualPath> returnVal = new List<VirtualPath>();
            var files = await item.GetFilesAsync().AsTask().ConfigureAwait(false);
            foreach (var file in files)
            {
                //_logger.Log("Found " + file.Name);
                returnVal.Add(directoryName.Combine(file.Name));
            }

            return returnVal;
        }

        public override async Task<IEnumerable<VirtualPath>> ListDirectoriesAsync(VirtualPath directoryName)
        {
            StorageFolder item = await ResolveContainer(directoryName).ConfigureAwait(false);
            //_logger.Log("Enumerating folders " + directoryName.FullName);
            List<VirtualPath> returnVal = new List<VirtualPath>();
            var folders = await item.GetFoldersAsync().AsTask().ConfigureAwait(false);
            foreach (var folder in folders)
            {
                //_logger.Log("Found " + folder.Name);
                returnVal.Add(directoryName.Combine(folder.Name));
            }

            return returnVal;
        }

        public override async Task<FileStat> StatAsync(VirtualPath fileName)
        {
            IStorageFile file = await ResolvePathToFile(fileName).ConfigureAwait(false);
            if (file == null)
            {
                return null;
            }

            BasicProperties fileProperties = await file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
            return new FileStat()
            {
                LastWriteTime = new DateTimeOffset(fileProperties.DateModified.DateTime),
                CreationTime = file.DateCreated,
                LastAccessTime = new DateTimeOffset(fileProperties.DateModified.DateTime),
                Size = (long)fileProperties.Size
            };
        }

        public override async Task CreateDirectoryAsync(VirtualPath path)
        {
            //_logger.Log("Creating subdirectories " + path.FullName);
            if (path.IsRoot)
            {
                //_logger.Log("It is root");
                return;
            }

            string[] pathParts = path.FullName.Split(new char[] { VirtualPath.PATH_SEPARATOR_CHAR }, StringSplitOptions.RemoveEmptyEntries);
            StorageFolder currentFolder = _root;
            foreach (string subDir in pathParts)
            {
                //_logger.Log("Pwd is " + currentFolder.Name + ", looking for " + subDir);
                // Does the subdirectory already exist?
#if WINDOWS_UWP
                IStorageItem item = await currentFolder.TryGetItemAsync(subDir).AsTask().ConfigureAwait(false);
#else
                IStorageItem item;
                try { item = await currentFolder.GetFolderAsync(subDir).AsTask().ConfigureAwait(false); } catch (Exception) { item = null; }
#endif
                if (item == null)
                {
                    currentFolder = await currentFolder.CreateFolderAsync(subDir).AsTask().ConfigureAwait(false);
                }
                else
                {
                    if (!item.IsOfType(StorageItemTypes.Folder))
                    {
                        throw new IOException("Attempted to create directory " + path.FullName + " but a file already exists there!");
                    }

                    currentFolder = await currentFolder.GetFolderAsync(subDir).AsTask().ConfigureAwait(false);
                }

                //_logger.Log("Now pwd is " + currentFolder.Name);
            }
        }

        public override async Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            // Not implemented yet...
            StorageFolder folder = await ResolveContainer(watchPath);
            StorageLibraryChangeTracker tracker = folder.TryGetChangeTracker();
            return NullFileSystemWatcher.Singleton;
        }

        private async Task<IStorageFile> ResolvePathToFile(VirtualPath resourceName)
        {
            return await ResolvePathToItem(resourceName).ConfigureAwait(false) as IStorageFile;
        }

        /// <summary>
        /// Returns the storage folder for a container path
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        private async Task<StorageFolder> ResolveContainer(VirtualPath resourceName)
        {
            //_logger.Log("Resolving container " + resourceName.FullName);
            if (resourceName.IsRoot)
            {
                //_logger.Log("It is root");
                return _root;
            }

            string[] pathParts = resourceName.FullName.Split(new char[] { VirtualPath.PATH_SEPARATOR_CHAR }, StringSplitOptions.RemoveEmptyEntries);
            StorageFolder currentFolder = _root;
            foreach (string subDir in pathParts)
            {
                //_logger.Log("Pwd is " + currentFolder.Name + ", looking for " + subDir);
#if WINDOWS_UWP
                IStorageItem item = await currentFolder.TryGetItemAsync(subDir).AsTask().ConfigureAwait(false);
#else
                IStorageItem item;
                try { item = await currentFolder.GetFolderAsync(subDir).AsTask().ConfigureAwait(false); } catch (Exception) { item = null; }
#endif
                if (item == null)
                {
                    //_logger.Log("Subdir " + subDir + " was null!");
                    return null;
                }

                if (!item.IsOfType(StorageItemTypes.Folder))
                {
                    //_logger.Log("Subdir " + subDir + " is not a directory!");
                    throw new IOException("Not a directory: " + resourceName.FullName);
                }

                currentFolder = item as StorageFolder;

                //_logger.Log("Now pwd is " + currentFolder.Name);
            }

            return currentFolder;
        }

        private async Task<IStorageItem> ResolvePathToItem(VirtualPath resourceName)
        {
            //_logger.Log("Now looking for file " + resourceName.FullName);
            if (resourceName.IsRoot)
            {
                //_logger.Log("It is root");
                return _root;
            }

            StorageFolder container = await ResolveContainer(resourceName.Container).ConfigureAwait(false);
            if (container == null)
            {
                //_logger.Log("Container not found for " + resourceName.FullName);
                return null;
            }

#if WINDOWS_UWP
            IStorageItem item = await container.TryGetItemAsync(resourceName.Name).AsTask().ConfigureAwait(false);
#else
            IStorageItem item;
            try
            {
                item = await container.GetFileAsync(resourceName.Name).AsTask().ConfigureAwait(false);
            }
            catch (Exception)
            {
                try
                {
                    item = await container.GetFolderAsync(resourceName.Name).AsTask().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    item = null;
                }
            }
#endif
            //if (item != null)
            //{
            //    _logger.Log("Found " + resourceName.FullName);
            //}
            //else
            //{
            //    _logger.Log("Did not find " + resourceName.FullName);
            //}
            return item;
        }
    }
}
