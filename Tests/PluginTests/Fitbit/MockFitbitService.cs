using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DialogTests.Plugins.Fitbit
{
    public class MockFitbitService : IHttpServerDelegate
    {
        public async Task<HttpResponse> HandleConnection(HttpRequest request)
        {
            await Task.Delay(0);
            return HttpResponse.NotFoundResponse();
        }
    }
}
