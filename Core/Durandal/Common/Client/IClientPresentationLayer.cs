using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Security;
using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using Durandal.Common.Security.Client;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Common.Client
{
    public delegate ClientContext ClientContextFactory();

    public interface IClientPresentationLayer
    {
        Task<Uri> GeneratePresentationUrlFromResponse(DialogResponse durandalResult, IRealTimeProvider realTime);

        void UpdateNextTurnTraceId(Guid? traceId);

        IDictionary<string, string> GetClientJavascriptData();

        void Initialize(
            IDialogClient dialogConnection,
            ClientContextFactory contextGenerator,
            ClientAuthenticator authenticator,
            IClientHtmlRenderer localMessageRenderer);

        Task<bool> Start(CancellationToken cancelToken, IRealTimeProvider realTime);

        Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}