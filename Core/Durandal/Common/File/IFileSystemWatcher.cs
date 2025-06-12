using Durandal.Common.Events;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.File
{
    /// <summary>
    /// Represents a watcher for monitoring changes to a virtual filesystem directory or subdirectories.
    /// The watcher will raise events whenever a change is detected which matches filter parameters which are set at initialization time.
    /// The watcher should be active until it is disposed. If the watch folder is deleted while watching is in progress, the behavior is undefined (but NO exception should be thrown).
    /// </summary>
    public interface IFileSystemWatcher : IDisposable
    {
        AsyncEvent<FileSystemChangedEventArgs> ChangedEvent { get; }
    }
}
