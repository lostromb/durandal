using Durandal.Common.Dialog;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DurandalServices.ChannelProxy.Connectors
{
    public interface IConnector
    {
        Task<HttpResponse> HandleRequest(IDialogClient client, HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime);
        string Prefix { get; }
    }
}
