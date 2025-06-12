using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Durandal.Common.Tasks;
using Durandal.Common.Net.Http;

namespace Durandal.Common.Monitoring.Monitors.Http
{
    /// <summary>
    /// Validator which decodes an HTTP payload as a UTF8 string and then ensures that a specific regex matches that content.
    /// </summary>
    public class HttpRegexMatchValidator : IHttpResponseValidator
    {
        private Regex _regex;
        public HttpRegexMatchValidator(string regex)
        {
            _regex = new Regex(regex);
        }

        public async Task<SingleTestResult> Validate(HttpResponse responseMessage, ArraySegment<byte> responseContent)
        {
            string content = Encoding.UTF8.GetString(responseContent.Array, responseContent.Offset, responseContent.Count);

            if (string.IsNullOrEmpty(content))
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Null or empty response from service"
                };
            }

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
