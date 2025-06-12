using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Net.Http;

namespace Durandal.Common.Monitoring.Monitors.Http
{
    public class HttpDisjunctionValidator : IHttpResponseValidator
    {
        private readonly List<IHttpResponseValidator> _validations;

        public HttpDisjunctionValidator(List<IHttpResponseValidator> validations)
        {
            _validations = validations;
        }

        public async Task<SingleTestResult> Validate(HttpResponse responseMessage, ArraySegment<byte> responseContent)
        {
            foreach (var validator in _validations)
            {
                SingleTestResult individualResult = await validator.Validate(responseMessage, responseContent).ConfigureAwait(false);
                if (individualResult.Success)
                {
                    return individualResult;
                }
            }

            return new SingleTestResult()
            {
                Success = false,
                ErrorMessage = "None of the HTTP validations passed"
            };
        }
    }
}
