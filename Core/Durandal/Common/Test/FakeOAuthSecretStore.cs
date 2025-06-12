using Durandal.Common.Security.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Instrumentation;
using Durandal.API;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Test
{
    public class FakeOAuthSecretStore : IOAuthSecretStore
    {
        private OAuthToken _fakeToken = null;
        private int _disposed = 0;

        public FakeOAuthSecretStore()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FakeOAuthSecretStore()
        {
            Dispose(false);
        }
#endif

        public void SetMockToken(OAuthToken token)
        {
            _fakeToken = token;
        }

        public Task DeleteState(string stateId, Guid? traceId = null)
        {
            _fakeToken = null;
            return Task.Delay(0);
        }

        public Task<RetrieveResult<OAuthState>> RetrieveState(string stateId, Guid? traceId = null)
        {
            return Task.FromResult(new RetrieveResult<OAuthState>());
        }

        public Task<RetrieveResult<OAuthState>> RetrieveState(string durandalUserId, PluginStrongName durandalPlugin, OAuthConfig config, Guid? traceId = null)
        {
            if (_fakeToken == null)
            {
                return Task.FromResult(new RetrieveResult<OAuthState>());
            }

            OAuthState returnVal = new OAuthState()
            {
                Config = config,
                DurandalPluginId = durandalPlugin.PluginId,
                DurandalUserId = durandalUserId,
                OriginalTraceId = traceId.HasValue ? CommonInstrumentation.FormatTraceId(traceId.Value) : null,
                UniqueId = Guid.NewGuid().ToString("N"),
                Token = _fakeToken
            };

            return Task.FromResult(new RetrieveResult<OAuthState>(returnVal));
        }

        public Task SaveState(OAuthState state, Guid? traceId = null)
        {
            return Task.Delay(0);
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
            }
        }
    }
}
