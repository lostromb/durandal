using Durandal.Common.File;
using Durandal.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.File
{
    /// <summary>
    /// A file system which is able to virtually map certain resource paths to other file systems,
    /// allowing for setups such as "keep all files in memory, except map the /cache directory to disk" or similar.
    /// </summary>
    public class HybridFileSystem : AbstractFileSystem
    {
        private List<Route> _routes;

        public HybridFileSystem(IFileSystem defaultHandler)
        {
            _routes = new List<Route>();
            _routes.Add(new Route()
            {
                Prefix = VirtualPath.Root,
                Target = defaultHandler,
                Chroot = null
            });
        }

        public override NonRealTimeStream OpenStream(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            Route route = ResolveFilePathToRoute(file);
            return route.Target.OpenStream(route.RewritePathForwards(file), openMode, accessMode, bufferSizeHint);
        }

        public override NonRealTimeStream OpenStream(FileStreamParams fileParams)
        {
            Route route = ResolveFilePathToRoute(fileParams.Path);
            return route.Target.OpenStream(fileParams.WithFile(route.RewritePathForwards(fileParams.Path)));
        }

        public override Task<NonRealTimeStream> OpenStreamAsync(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            Route route = ResolveFilePathToRoute(file);
            return route.Target.OpenStreamAsync(route.RewritePathForwards(file), openMode, accessMode, bufferSizeHint);
        }

        public override Task<NonRealTimeStream> OpenStreamAsync(FileStreamParams fileParams)
        {
            Route route = ResolveFilePathToRoute(fileParams.Path);
            return route.Target.OpenStreamAsync(fileParams.WithFile(route.RewritePathForwards(fileParams.Path)));
        }

        public override void WriteLines(VirtualPath targetFile, IEnumerable<string> data)
        {
            Route route = ResolveFilePathToRoute(targetFile);
            route.Target.WriteLines(route.RewritePathForwards(targetFile), data);
        }

        public override async Task WriteLinesAsync(VirtualPath targetFile, IEnumerable<string> data)
        {
            Route route = ResolveFilePathToRoute(targetFile);
            await route.Target.WriteLinesAsync(route.RewritePathForwards(targetFile), data).ConfigureAwait(false);
        }

        public override IReadOnlyCollection<string> ReadLines(VirtualPath sourceFile)
        {
            Route route = ResolveFilePathToRoute(sourceFile);
            return route.Target.ReadLines(route.RewritePathForwards(sourceFile));
        }

        public override Task<IReadOnlyCollection<string>> ReadLinesAsync(VirtualPath sourceFile)
        {
            Route route = ResolveFilePathToRoute(sourceFile);
            return route.Target.ReadLinesAsync(route.RewritePathForwards(sourceFile));
        }

        public override bool Delete(VirtualPath path)
        {
            // See if the path is a virtual directory first
            if (IsVirtualSubcontainer(path))
            {
                // This is kind of tough. We can't (shouldn't?) allow deletion of fixed routes, but we don't want to report a failure either....
                return false;
            }

            Route route = ResolveFilePathToRoute(path);
            return route.Target.Delete(route.RewritePathForwards(path));
        }

        public override async Task<bool> DeleteAsync(VirtualPath path)
        {
            // See if the path is a virtual directory first
            if (IsVirtualSubcontainer(path))
            {
                return false;
            }

            Route route = ResolveFilePathToRoute(path);
            return await route.Target.DeleteAsync(route.RewritePathForwards(path)).ConfigureAwait(false);
        }

        public override bool Exists(VirtualPath path)
        {
            // See if the path is a virtual directory first
            if (path.IsRoot || IsVirtualSubcontainer(path))
            {
                return true;
            }

            Route route = ResolveFilePathToRoute(path);
            return route.Target.Exists(route.RewritePathForwards(path));
        }

        public override async Task<bool> ExistsAsync(VirtualPath path)
        {
            // See if the path is a virtual directory first
            if (path.IsRoot || IsVirtualSubcontainer(path))
            {
                return true;
            }

            Route route = ResolveFilePathToRoute(path);
            return await route.Target.ExistsAsync(route.RewritePathForwards(path)).ConfigureAwait(false);
        }

        public override ResourceType WhatIs(VirtualPath path)
        {
            if (IsVirtualSubcontainer(path))
            {
                return ResourceType.Directory;
            }

            Route route = ResolveFilePathToRoute(path);
            return route.Target.WhatIs(route.RewritePathForwards(path));
        }

        public override async Task<ResourceType> WhatIsAsync(VirtualPath path)
        {
            // See if the resource is a virtual directory first
            if (IsVirtualSubcontainer(path))
            {
                return ResourceType.Directory;
            }

            Route route = ResolveFilePathToRoute(path);
            return await route.Target.WhatIsAsync(route.RewritePathForwards(path)).ConfigureAwait(false);
        }

        public override IEnumerable<VirtualPath> ListFiles(VirtualPath directoryName)
        {
            // See if the path is a virtual directory first
            //HashSet<VirtualPath> virtualDirs = ListVirtualSubContainers(resourceContainerName);

            Route route = ResolveDirectoryPathToRoute(directoryName);
            VirtualPath chrootPath = route.RewritePathForwards(directoryName);
            if (!route.Target.Exists(chrootPath))
            {
                return new List<VirtualPath>();
            }

            IEnumerable<VirtualPath> intermediateResults = route.Target.ListFiles(chrootPath);
            List<VirtualPath> returnVal = new List<VirtualPath>();
            // Undo chroot if the target route is isolated
            foreach (VirtualPath r in intermediateResults)
            {
                returnVal.Add(route.RewritePathBackwards(r));
            }

            return returnVal;
        }

        public override async Task<IEnumerable<VirtualPath>> ListFilesAsync(VirtualPath directoryName)
        {
            // See if the path is a virtual directory first
            //HashSet<VirtualPath> virtualDirs = ListVirtualSubContainers(resourceContainerName);

            Route route = ResolveDirectoryPathToRoute(directoryName);
            VirtualPath chrootPath = route.RewritePathForwards(directoryName);

            if (!route.Target.Exists(chrootPath))
            {
                return new List<VirtualPath>();
            }

            IEnumerable<VirtualPath> intermediateResults = await route.Target.ListFilesAsync(chrootPath).ConfigureAwait(false);
            List<VirtualPath> returnVal = new List<VirtualPath>();
            // Undo chroot if the target route is isolated
            foreach (VirtualPath r in intermediateResults)
            {
                returnVal.Add(route.RewritePathBackwards(r));
            }

            return returnVal;
        }

        public override IEnumerable<VirtualPath> ListDirectories(VirtualPath directoryName)
        {
            // See if the path is a virtual directory first
            HashSet<VirtualPath> returnVal = ListVirtualSubContainers(directoryName);

            Route route = ResolveDirectoryPathToRoute(directoryName);
            VirtualPath chrootPath = route.RewritePathForwards(directoryName);
            if (route.Target.Exists(chrootPath))
            {
                IEnumerable<VirtualPath> realContainers = route.Target.ListDirectories(chrootPath);

                // Undo chroot if the target route is isolated
                foreach (VirtualPath r in realContainers)
                {
                    VirtualPath rewritten = route.RewritePathBackwards(r);
                    if (!returnVal.Contains(rewritten))
                    {
                        returnVal.Add(rewritten);
                    }
                }
            }

            return returnVal;
        }

        public override async Task<IEnumerable<VirtualPath>> ListDirectoriesAsync(VirtualPath directoryName)
        {
            // See if the path is a virtual directory first
            HashSet<VirtualPath> returnVal = ListVirtualSubContainers(directoryName);

            Route route = ResolveDirectoryPathToRoute(directoryName);
            VirtualPath chrootPath = route.RewritePathForwards(directoryName);
            if (route.Target.Exists(chrootPath))
            {
                IEnumerable<VirtualPath> realContainers = await route.Target.ListDirectoriesAsync(chrootPath).ConfigureAwait(false);

                // Undo chroot if the target route is isolated
                foreach (VirtualPath r in realContainers)
                {
                    VirtualPath rewritten = route.RewritePathBackwards(r);
                    if (!returnVal.Contains(rewritten))
                    {
                        returnVal.Add(rewritten);
                    }
                }
            }

            return returnVal;
        }

        public override FileStat Stat(VirtualPath fileName)
        {
            Route route = ResolveFilePathToRoute(fileName);
            return route.Target.Stat(route.RewritePathForwards(fileName));
        }

        public override async Task<FileStat> StatAsync(VirtualPath fileName)
        {
            Route route = ResolveFilePathToRoute(fileName);
            return await route.Target.StatAsync(route.RewritePathForwards(fileName)).ConfigureAwait(false);
        }

        public override async Task WriteStatAsync(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            Route route = ResolveFilePathToRoute(fileName);
            await route.Target.WriteStatAsync(route.RewritePathForwards(fileName), newCreationTime, newModificationTime).ConfigureAwait(false);
        }

        public override void CreateDirectory(VirtualPath path)
        {
            throw new NotImplementedException();
        }

        public override Task CreateDirectoryAsync(VirtualPath path)
        {
            throw new NotImplementedException();
        }

        public override Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            return Task.FromResult(NullFileSystemWatcher.Singleton);
        }

        /// <summary>
        /// Maps a virtual path starting with the given prefix to the specified filesystem.
        /// The path that is sent to that filesystem is unchanged.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="target"></param>
        /// <param name="chrootPath">If non-null, modify the path that is sent to the target filesystem by replacing {PREFIX} with {CHROOT}.
        /// So if the path is "dir\subdir\file.dat" the prefix is "dir", and the chroot is "new", the target filesystem is given "new\subdir\file.dat"</param>
        public void AddRoute(VirtualPath prefix, IFileSystem target, VirtualPath chrootPath = null)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (prefix == null)
            {
                throw new ArgumentNullException("Path prefix is null");
            }
            if (prefix.FullName.StartsWith(".") || prefix.FullName.EndsWith("."))
            {
                throw new ArgumentException("Path prefix cannot begin or end with a period: " + prefix);
            }
            if (prefix.FullName.Contains("*"))
            {
                throw new ArgumentException("Path prefix should not use wildcards: " + prefix);
            }

            _routes.Add(new Route()
            {
                Prefix = prefix,
                Target = target,
                Chroot = chrootPath
            });
        }

        /// <summary>
        /// Returns true if the given path is one of the prefixes for a virtual subdirectory,
        /// and is therefore considered to always exist
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        private bool IsVirtualSubcontainer(VirtualPath container)
        {
            foreach (Route route in _routes)
            {
                string comparePrefix = route.Prefix.FullName + "\\";

                if (string.Equals(container.FullName, route.Prefix.FullName) ||
                    comparePrefix.StartsWith(container.FullName))
                {
                    return true;
                }
            }

            return false;
        }

        private HashSet<VirtualPath> ListVirtualSubContainers(VirtualPath container)
        {
            HashSet<VirtualPath> returnVal = new HashSet<VirtualPath>();
            bool queryingRoot = string.Equals("\\", container.FullName);
            foreach (Route route in _routes)
            {
                if (!string.Equals(container.FullName, route.Prefix.FullName) &&
                    route.Prefix.FullName.StartsWith(container.FullName))
                {
                    string nextDirectory;
                    if (queryingRoot)
                    {
                        nextDirectory = route.Prefix.FullName.Substring(1);
                    }
                    else
                    {
                        nextDirectory = route.Prefix.FullName.Substring(container.FullName.Length + 1);
                    }

                    if (nextDirectory.Contains("\\"))
                    {
                        nextDirectory = nextDirectory.Substring(0, nextDirectory.IndexOf('\\'));
                    }
                    VirtualPath virtualSubcontainer;
                    if (queryingRoot)
                    {
                        virtualSubcontainer = new VirtualPath("\\" + nextDirectory);
                    }
                    else
                    {
                        virtualSubcontainer = new VirtualPath(container.FullName + "\\" + nextDirectory);
                    }

                    if (!returnVal.Contains(virtualSubcontainer))
                    {
                        returnVal.Add(virtualSubcontainer);
                    }
                }
            }

            return returnVal;
        }

        /// <summary>
        /// The primary function that inspects the route and determines what route to take
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private Route ResolveFilePathToRoute(VirtualPath path)
        {
            Route returnVal = null;
            foreach (Route route in _routes)
            {
                // This should find the route with the longest prefix match
                if (path.Container.FullName.StartsWith(route.Prefix.FullName) &&
                    (returnVal == null || route.Prefix.FullName.Length > returnVal.Prefix.FullName.Length))
                {
                    returnVal = route;
                }
            }

            // Should never return null since the default prefix should always match
            return returnVal;
        }

        private Route ResolveDirectoryPathToRoute(VirtualPath path)
        {
            Route returnVal = null;
            foreach (Route route in _routes)
            {
                if ((string.Equals(path.FullName, route.Prefix.FullName) ||
                    path.FullName.StartsWith(route.Prefix.FullName)) &&
                    (returnVal == null || route.Prefix.FullName.Length > returnVal.Prefix.FullName.Length))
                {
                    returnVal = route;
                }
            }

            // Should never return null since the default prefix should always match
            return returnVal;
        }
        
        private class Route
        {
            /// <summary>
            /// The route's prefix, e.g. "\cache"
            /// </summary>
            public VirtualPath Prefix;

            /// <summary>
            /// The target filesystem that handles this route
            /// </summary>
            public IFileSystem Target;

            /// <summary>
            /// If non-null, replace the prefix with this path when passing to the target filesystem
            /// </summary>
            public VirtualPath Chroot;

            /// <summary>
            /// Indicates whether this route has had its directory deleted.
            /// For example, if we have a virtual route "/bin/logs", and some other operation
            /// deletes the entire /bin directory, what will happen is that 1. all files in the
            /// target of /bin/logs will be deleted, and 2. /bin/logs itself will be "virtually"
            /// deleted, not returning itself in listings or anything until something else
            /// recreates it, at which time file calls to the route will go back to that
            /// route's original target file system.
            /// </summary>
            public bool IsVirtuallyDeleted;

            public VirtualPath RewritePathForwards(VirtualPath path)
            {
                if (Chroot != null)
                {
                    string modPath = path.FullName.Substring(Prefix.FullName.Length);
                    if (modPath.Length == 0)
                    {
                        return Chroot;
                    }
                    else
                    {
                        return Chroot.Combine(modPath);
                    }
                }
                else
                {
                    return path;
                }
            }

            public VirtualPath RewritePathBackwards(VirtualPath path)
            {
                if (Chroot != null)
                {
                    string modPath = path.FullName.Substring(Chroot.FullName.Length);
                    if (modPath.Length == 0)
                    {
                        return Prefix;
                    }
                    else
                    {
                        return Prefix.Combine(modPath);
                    }
                }
                else
                {
                    return path;
                }
            }
        }
    }
}
