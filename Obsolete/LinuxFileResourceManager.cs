using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stromberg.Utils.IO
{
    using System.IO;

    using Stromberg.Logger;
    using System.Threading.Tasks;

    public class LinuxFileResourceManager : IResourceManager
    {
        private const int READ_BUFFER_SIZE = 32768;
        private const int WRITE_BUFFER_SIZE = 32768;
        private string _workingDir;
        private ILogger _logger;

        public LinuxFileResourceManager(ILogger logger, string workingDirectory = null)
        {
            _logger = logger.Clone("FileResourceManager");
            _workingDir = workingDirectory;
            if (string.IsNullOrEmpty(_workingDir))
            {
                _workingDir = Environment.CurrentDirectory;
            }
            DirectoryInfo rootDirectory = new DirectoryInfo(_workingDir);
            if (!rootDirectory.Exists)
            {
                _logger.Log("Could not create resource manager linked to directory \"" + _workingDir + "\" because the directory does not exist!", LogLevel.Err);
                _workingDir = Environment.CurrentDirectory;
            }

            _workingDir = rootDirectory.FullName.TrimEnd('\\');
            _logger.Log("File resource manager is initialized - working directory is \"" + _workingDir + "\"");
        }

        public Stream WriteStream(ResourceName resourceName)
        {
            FileInfo targetFile = new FileInfo(MapResourceToFileName(resourceName));
            if (!targetFile.Directory.Exists)
            {
                // Do we need to create subdirectories for this file?
                Directory.CreateDirectory(targetFile.Directory.FullName);
            }

            return new BufferedStream(new FileStream(targetFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None), WRITE_BUFFER_SIZE);
        }

        public bool WriteLines(ResourceName resourceName, IEnumerable<string> data)
        {
            try
            {
                File.WriteAllLines(MapResourceToFileName(resourceName), data);
                return true;
            }
            catch (Exception e)
            {
                _logger.Log(e.Message, LogLevel.Err);
            }
            return false;
        }

        public Stream ReadStream(ResourceName resourceName)
        {
            if (File.Exists(MapResourceToFileName(resourceName)))
            {
                return new BufferedStream(new FileStream(MapResourceToFileName(resourceName), FileMode.Open, FileAccess.Read, FileShare.Read), READ_BUFFER_SIZE);
            }
            return null;
        }

        public IEnumerable<string> ReadLines(ResourceName resourceName)
        {
            if (File.Exists(MapResourceToFileName(resourceName)))
            {
                return File.ReadAllLines(MapResourceToFileName(resourceName));
            }
            return null;
        }

        public bool Delete(ResourceName resourceName)
        {
            try
            {
                if (File.Exists(MapResourceToFileName(resourceName)))
                {
                    File.Delete(MapResourceToFileName(resourceName));
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.Log(e.Message, LogLevel.Err);
            }
            return false;
        }

        public bool Exists(ResourceName resourceName)
        {
            string path = MapResourceToFileName(resourceName);
            return new FileInfo(path).Exists || new DirectoryInfo(path).Exists;
        }

        public bool IsContainer(ResourceName resourceName)
        {
            return new DirectoryInfo(MapResourceToFileName(resourceName)).Exists;
        }

        public IEnumerable<ResourceName> ListResources(ResourceName resourceContainerName)
        {
            if (!IsContainer(resourceContainerName))
            {
                return null;
            }

            DirectoryInfo dir = new DirectoryInfo(MapResourceToFileName(resourceContainerName));
            if (dir.Exists)
            {
                FileInfo[] fileInfos = dir.GetFiles();
                ResourceName[] fileNames = new ResourceName[fileInfos.Length];
                for (int c = 0; c < fileInfos.Length; c++)
                {
                    // TODO: HACKISH CONVERSION
                    fileNames[c] = new ResourceName(fileInfos[c].FullName.Remove(0, _workingDir.Length + 1)); // +1 to catch the slash as well
                }
                return fileNames;
            }
            return null;
        }

        public IEnumerable<ResourceName> ListContainers(ResourceName resourceContainerName)
        {
            if (!IsContainer(resourceContainerName))
            {
                return null;
            }

            DirectoryInfo dir = new DirectoryInfo(MapResourceToFileName(resourceContainerName));
            if (dir.Exists)
            {
                DirectoryInfo[] dirInfos = dir.GetDirectories();
                ResourceName[] containerNames = new ResourceName[dirInfos.Length];
                for (int c = 0; c < dirInfos.Length; c++)
                {
                    // TODO: HACKISH CONVERSION
                    containerNames[c] = new ResourceName(dirInfos[c].FullName.Remove(0, _workingDir.Length + 1)); // +1 to catch the slash as well
                }
                return containerNames;
            }
            return null;
        }

        public Task<Stream> WriteStreamAsync(ResourceName resourceName)
        {
            return Task.Run(() => WriteStream(resourceName));
        }

        public Task<bool> WriteLinesAsync(ResourceName resourceName, IEnumerable<string> data)
        {
            return Task.Run(() => WriteLines(resourceName, data));
        }

        public Task<Stream> ReadStreamAsync(ResourceName resourceName)
        {
            return Task.Run(() => ReadStream(resourceName));
        }

        public Task<IEnumerable<string>> ReadLinesAsync(ResourceName resourceName)
        {
            return Task.Run(() => ReadLines(resourceName));
        }

        public Task<bool> DeleteAsync(ResourceName resourceName)
        {
            return Task.Run(() => Delete(resourceName));
        }

        public Task<bool> ExistsAsync(ResourceName resourceName)
        {
            return Task.Run(() => Exists(resourceName));
        }

        public Task<bool> IsContainerAsync(ResourceName resourceName)
        {
            return Task.Run(() => IsContainer(resourceName));
        }

        public Task<IEnumerable<ResourceName>> ListResourcesAsync(ResourceName resourceContainerName)
        {
            return Task.Run(() => ListResources(resourceContainerName));
        }

        public Task<IEnumerable<ResourceName>> ListContainersAsync(ResourceName resourceContainerName)
        {
            return Task.Run(() => ListContainers(resourceContainerName));
        }

        private string MapResourceToFileName(ResourceName resource)
        {
            return _workingDir + Path.DirectorySeparatorChar + resource.FullName;
        }
    }
}