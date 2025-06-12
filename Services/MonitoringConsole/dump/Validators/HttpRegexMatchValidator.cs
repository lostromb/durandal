using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon;
using System.Text.RegularExpressions;
using Durandal.Common.Utils.Tasks;
using Durandal.Common.Net.Http;
using Photon.Common.Schemas;

namespace Photon.Common.Validators
{
    public class HttpRegexMatchValidator : IHttpResponseValidator
    {
        private readonly Regex _regex;

        public HttpRegexMatchValidator(string regex)
        {
            _regex = new Regex(regex);
        }

        public async Task<SingleTestResult> Validate(HttpResponse response)
        {
            if (response.PayloadData.Length == 0)
            {
                await DurandalTaskExtensions.NoOpTask;
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Null or empty response from service"
                };
            }

            string content = response.GetPayloadAsString();

            if (!_regex.Match(content).Success)
            {
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Response failed to match regex " + _regex.ToString() + ". Response content was: " + content
                };
            }

            return new SingleTestResult()
            {
                Success = true
            };
        }
    }
}
