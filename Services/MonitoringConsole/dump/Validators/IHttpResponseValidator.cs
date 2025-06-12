using Photon;
using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.Common.Schemas;

namespace Photon.Common.Validators
{
    public interface IHttpResponseValidator
    {
        Task<SingleTestResult> Validate(HttpResponse httpResponse);
    }
}
