using System;
using System.Threading;

// This code is taken from Stephen Cleary's Disposables package, which I cherry-picked because it is not entirely NetStandard 1.1 compatible
// https://github.com/StephenCleary/Disposables
// MIT License
namespace Durandal.Common.ServiceMgmt
{
    /// <summary>
    /// A base class for disposables that need exactly-once semantics in a threadsafe way. All disposals of this instance block until the disposal is complete.
    /// </summary>
    /// <typeparam name="T">The type of "context" for the derived disposable. Since the context should not be modified, strongly consider making this an immutable type.</typeparam>
    /// <remarks>
    /// <para>If <see cref="Dispose()"/> is called multiple times, only the first call will execute the disposal code. Other calls to <see cref="Dispose()"/> will wait for the disposal to complete.</para>
    /// </remarks>
#pragma warning disable CA1063 // Implement IDisposable Correctly
    public abstract class SingleDisposable<T> : IDisposable
#pragma warning restore CA1063 // Implement IDisposable Correctly
    {
        /// <summary>
        /// The context. This is never <c>null</c>. This is empty if this instance has already been disposed (or is being disposed).
        /// </summary>
        private readonly BoundActionField<T> _context;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly ManualResetEventSlim _mre = new ManualResetEventSlim();
#pragma warning restore CA2213 // Disposable fields should be disposed

        /// <summary>
        /// Creates a disposable for the specified context.
        /// </summary>
        /// <param name="context">The context passed to <see cref="Dispose(T)"/>.</param>
        protected SingleDisposable(T context)
        {
            _context = new BoundActionField<T>(Dispose, context);
        }

        /// <summary>
        /// Whether this instance is currently disposing or has been disposed.
        /// </summary>
        public bool IsDisposeStarted
        {
            get
            {
                return _context.IsEmpty;
            }
        }

        /// <summary>
        /// Whether this instance is disposed (finished disposing).
        /// </summary>
        public bool IsDisposed
        {
            get
            {
                return _mre.IsSet;
            }
        }

        /// <summary>
        /// Whether this instance is currently disposing, but not finished yet.
        /// </summary>
        public bool IsDisposing
        {
            get
            {
                return IsDisposeStarted && !IsDisposed;
            }
        }

        /// <summary>
        /// The actul disposal method, called only once from <see cref="Dispose()"/>.
        /// </summary>
        /// <param name="context">The context for the disposal operation.</param>
        protected abstract void Dispose(T context);


        /// <summary>
        /// Disposes this instance.
        /// </summary>
        /// <remarks>
        /// <para>If <see cref="Dispose()"/> is called multiple times, only the first call will execute the disposal code. Other calls to <see cref="Dispose()"/> will wait for the disposal to complete.</para>
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "I'll trust Stephen Cleary on this one")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "I'll trust Stephen Cleary on this one")]
        public void Dispose()
        {
            var context = _context.TryGetAndUnset();
            if (context == null)
            {
                _mre.Wait();
                return;
            }
            try
            {
                context.Invoke();
            }
            finally
            {
                _mre.Set();
            }
        }

        /// <summary>
        /// Attempts to update the stored context. This method returns <c>false</c> if this instance has already been disposed (or is being disposed).
        /// </summary>
        /// <param name="contextUpdater">The function used to update an existing context. This may be called more than once if more than one thread attempts to simultanously update the context.</param>
        protected bool TryUpdateContext(Func<T, T> contextUpdater)
        {
            return _context.TryUpdateContext(contextUpdater);
        }
    }
}
