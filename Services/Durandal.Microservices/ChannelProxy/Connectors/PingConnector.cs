using Durandal.Common.Dialog;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DurandalServices.ChannelProxy.Connectors
{
    public class PingConnector : IConnector
    {
        public string Prefix
        {
            get
            {
                return "/connectors/ping";
            }
        }

        public async Task<HttpResponse> HandleRequest(IDialogClient client, HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpResponse response = HttpResponse.OKResponse();
            response.SetContent("Doctor Grant, the phones are working");
            await DurandalTaskExtensions.NoOpTask;
            return response;
        }
    }
}
