using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Security.Server;
using Durandal.Common.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.Net;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Service
{
    public class DialogServiceCollection : IDisposable
    {
        private int _disposed = 0;

        public DialogServiceCollection()
        {
            Disposables = new List<IDisposable>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DialogServiceCollection()
        {
            Dispose(false);
        }
#endif

        public IConversationStateCache ConversationStateCache { get; set; }
        public ICache<DialogAction> DialogActionStore { get; set; }
        public ICache<CachedWebData> WebDataStore { get; set; }
        public ICache<ClientContext> ClientContextStore { get; set; }
        public IPublicKeyStore PublicKeyStore { get; set; }
        public IUserProfileStorage UserProfileStore { get; set; }
        public IOAuthSecretStore OAuthSecretStore { get; set; }
        public IStreamingAudioCache StreamingAudioCache { get; set; }
        public IServerSocketFactory ServerSocketFactory { get; set; }

        /// <summary>
        /// Extra things to dispose along with the service collection, in addition to what's already here in this class.
        /// </summary>
        public IList<IDisposable> Disposables { get; private set; }

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
                ConversationStateCache?.Dispose();
                DialogActionStore?.Dispose();
                WebDataStore?.Dispose();
                ClientContextStore?.Dispose();
                //PublicKeyStore?.Dispose();
                //UserProfileStore?.Dispose();
                OAuthSecretStore?.Dispose();
                StreamingAudioCache?.Dispose();
                //ServerSocketFactory?.Dispose();

                foreach (IDisposable disposable in Disposables)
                {
                    disposable?.Dispose();
                }
            }
        }
    }
}
