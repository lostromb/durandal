using Durandal.API;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Security.OAuth
{
    public class InMemoryOAuthSecretStore : IOAuthSecretStore
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly Dictionary<string, OAuthState> _states = new Dictionary<string, OAuthState>();
        private int _disposed = 0;

        public InMemoryOAuthSecretStore()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~InMemoryOAuthSecretStore()
        {
            Dispose(false);
        }
#endif

        public async Task<RetrieveResult<OAuthState>> RetrieveState(string stateId, Guid? traceId = null)
        {
            _lock.EnterReadLock();
            try
            {
                OAuthState returnVal;
                if (_states.TryGetValue(stateId, out returnVal))
                {
                    return new RetrieveResult<OAuthState>(returnVal);
                }

                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                return new RetrieveResult<OAuthState>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public async Task<RetrieveResult<OAuthState>> RetrieveState(string durandalUserId, PluginStrongName durandalPlugin, OAuthConfig config, Guid? traceId = null)
        {
            string desiredHash = Convert.ToBase64String(config.HashConfiguration());
            _lock.EnterReadLock();
            try
            {
                foreach (OAuthState state in _states.Values)
                {
                    if (string.Equals(state.DurandalPluginId, durandalPlugin.PluginId) &&
                        string.Equals(state.DurandalUserId, durandalUserId) &&
                        string.Equals(state.Config.ConfigName, config.ConfigName))
                    {
                        // Check the hash as well
                        string hash = Convert.ToBase64String(state.Config.HashConfiguration());
                        if (string.Equals(desiredHash, hash))
                        {
                            return new RetrieveResult<OAuthState>(state);
                        }
                    }
                }

                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                return new RetrieveResult<OAuthState>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public async Task SaveState(OAuthState state, Guid? traceId = null)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_states.ContainsKey(state.UniqueId))
                {
                    _states.Remove(state.UniqueId);
                }

                _states[state.UniqueId] = state;
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public async Task DeleteState(string stateId, Guid? traceId = null)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_states.ContainsKey(stateId))
                {
                    _states.Remove(stateId);
                }

                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Only implemented for InMemorySecretStore, intended for unit tests
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _states.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

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
                _lock?.Dispose();
            }
        }
    }
}
