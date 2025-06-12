using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.File
{
    public class RealFileSystemWatcher : IFileSystemWatcher
    {
        private readonly string _directoryRoot;
        private readonly FileSystemWatcher _watcher;
        private readonly ILogger _logger;
        private int _disposed = 0;

        public RealFileSystemWatcher(string rootDirectory, string watchDirectory, string filter, bool includeSubdirectories, ILogger logger)
        {
            // For some reason, the .Net documentation claims that empty string is equal to wildcard,
            // however in practice that doesn't seem to be the case. So make it explicit here.
            if (string.IsNullOrWhiteSpace(filter))
            {
                filter = "*";
            }

            _directoryRoot = rootDirectory;
            _logger = logger;

            if (!Directory.Exists(watchDirectory))
            {
                throw new FileNotFoundException("Watch directory \"" + watchDirectory + "\" does not exist");
            }

            ChangedEvent = new AsyncEvent<FileSystemChangedEventArgs>();
            _watcher = new FileSystemWatcher(watchDirectory, filter);
            _watcher.IncludeSubdirectories = includeSubdirectories;
            _watcher.Changed += HandleChanged;
            _watcher.Created += HandleCreated;
            _watcher.Deleted += HandleDeleted;
            _watcher.Renamed += HandleRenamed;
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName; // only check for file-level changes for now; in the future we could add NotifyFilters.DirectoryName
            _watcher.EnableRaisingEvents = true;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RealFileSystemWatcher()
        {
            Dispose(false);
        }
#endif

        public AsyncEvent<FileSystemChangedEventArgs> ChangedEvent { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _watcher.Dispose();
            }
        }

        private VirtualPath ConvertToVirtualPath(string path)
        {
            return new VirtualPath(path.Substring(_directoryRoot.Length));
        }

        private void HandleChanged(object source, FileSystemEventArgs args)
        {
            OnChanged(new FileSystemChangedEventArgs()
            {
                ChangeType = FileSystemChangeType.FileChanged,
                AffectedPath = ConvertToVirtualPath(args.FullPath),
                RenamedPath = null
            });
        }

        private void HandleCreated(object source, FileSystemEventArgs args)
        {
            OnChanged(new FileSystemChangedEventArgs()
            {
                ChangeType = FileSystemChangeType.FileCreated,
                AffectedPath = ConvertToVirtualPath(args.FullPath),
                RenamedPath = null
            });
        }

        private void HandleDeleted(object source, FileSystemEventArgs args)
        {
            OnChanged(new FileSystemChangedEventArgs()
            {
                ChangeType = FileSystemChangeType.FileDeleted,
                AffectedPath = ConvertToVirtualPath(args.FullPath),
                RenamedPath = null
            });
        }

        private void HandleRenamed(object source, RenamedEventArgs args)
        {
            OnChanged(new FileSystemChangedEventArgs()
            {
                ChangeType = FileSystemChangeType.FileRenamed,
                AffectedPath = ConvertToVirtualPath(args.OldFullPath),
                RenamedPath = ConvertToVirtualPath(args.FullPath)
            });
        }

        private void OnChanged(FileSystemChangedEventArgs args)
        {
            try
            {
                ChangedEvent.FireInBackground(this, args, _logger, DefaultRealTimeProvider.Singleton);
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
        }
    }
}
