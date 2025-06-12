using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.ServiceMgmt
{
    /// <summary>
    /// Wrapper which enforces that an IServiceResolver for IDisposable objects should always return
    /// a WeakPointer instead of a strong reference to the disposable object itself.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DisposableServiceResolver<T> : IServiceResolver<WeakPointer<T>> where T : class, IDisposable
    {
        private ServiceResolver<T> _inner;

        public DisposableServiceResolver(ServiceResolver<T> inner)
        {
            _inner = inner.AssertNonNull(nameof(inner));
        }

        /// <inheritdoc />
        public WeakPointer<T> ResolveService()
        {
            return new WeakPointer<T>(_inner.ResolveService());
        }

        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}
