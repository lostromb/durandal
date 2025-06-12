using Durandal.Common.Events;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.File
{
    public sealed class NullFileSystemWatcher : IFileSystemWatcher
    {
        public static readonly IFileSystemWatcher Singleton = new NullFileSystemWatcher();

        private NullFileSystemWatcher()
        {
        }

        public AsyncEvent<FileSystemChangedEventArgs> ChangedEvent => new AsyncEvent<FileSystemChangedEventArgs>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Static singleton class is never disposed")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Static singleton class is never disposed")]
        public void Dispose() { }
    }
}
