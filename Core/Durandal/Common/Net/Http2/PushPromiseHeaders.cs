using Durandal.Common.Collections;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    internal class PushPromiseHeaders
    {
        public string QueryBasePath { get; private set; }
        public string RequestMethod { get; private set; }
        public HttpFormParameters QueryParameters { get; private set; }
        public IDictionary<string, string> ExtraHeaders { get; private set; }
        public int PromisedStreamId { get; private set; }
        public DateTimeOffset PromisedAtTime { get; private set; }

        public PushPromiseHeaders(
            string queryBasePath,
            string requestMethod,
            int promisedStreamId,
            DateTimeOffset promisedAtTime,
            HttpFormParameters queryParameters,
            IDictionary<string, string> extraHeaders)
        {
            QueryParameters = queryParameters;
            ExtraHeaders = extraHeaders;
            QueryBasePath = queryBasePath.AssertNonNullOrEmpty(nameof(queryBasePath));
            RequestMethod = requestMethod.AssertNonNullOrEmpty(nameof(requestMethod));
            PromisedStreamId = promisedStreamId;
            PromisedAtTime = promisedAtTime;
        }

        public bool DoesRequestMatch(HttpRequest request)
        {
            // we don't check scheme or authority here because those should have happened when the promise stream was first created
            if (!string.Equals(request.RequestFile, QueryBasePath, StringComparison.Ordinal) ||
                !string.Equals(request.RequestMethod, RequestMethod, StringComparison.OrdinalIgnoreCase) ||
                (QueryParameters != null && request.GetParameters != null && request.GetParameters.KeyCount != QueryParameters.KeyCount))
            {
                return false;
            }

            // Check that all query parameters are the same
            if (QueryParameters != null)
            {
                foreach (var queryParam in QueryParameters)
                {
                    foreach (var value in queryParam.Value)
                    {
                        if (!request.GetParameters.ContainsKey(queryParam.Key) ||
                            request.GetParameters.GetAllParameterValues(queryParam.Key).Contains(value))
                        {
                            return false;
                        }
                    }
                }
            }

            // Check that extra headers are present, if any (this seems like a very rare and error-prone case)
            if (ExtraHeaders != null)
            {
                foreach (var requiredHeader in ExtraHeaders)
                {
                    string headerValue;
                    if (!request.RequestHeaders.TryGetValue(requiredHeader.Key, out headerValue) ||
                        !string.Equals(requiredHeader.Value, headerValue, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
