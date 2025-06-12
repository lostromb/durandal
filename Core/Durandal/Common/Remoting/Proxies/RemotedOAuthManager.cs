using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Tasks;
using Durandal.API;
using Durandal.Common.Remoting.Protocol;
using System.Threading;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Audio;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Dialog.Services;

namespace Durandal.Common.Remoting.Proxies
{
    public class RemotedOAuthManager : IOAuthManager
    {
        private readonly RemoteDialogMethodDispatcher _dispatcher;

        public RemotedOAuthManager(RemoteDialogMethodDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public Task<Uri> CreateAuthUri(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _dispatcher.OAuth_CreateAuthUri(durandalUserId, owningPlugin, config, realTime, cancelToken);
        }

        public Task DeleteToken(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _dispatcher.OAuth_DeleteToken(durandalUserId, owningPlugin, config, realTime, cancelToken);
        }

        public Task<OAuthToken> GetToken(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _dispatcher.OAuth_GetToken(durandalUserId, owningPlugin, config, realTime, cancelToken);
        }
    }
}