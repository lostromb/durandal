using System;

namespace Durandal.Common.Net.Http
{
    public class HttpRequestException : Exception
    {
        public HttpRequestException() : base("An error occurred during the HTTP request") { }
        public HttpRequestException(string errorMessage) : base(errorMessage) { }
        public HttpRequestException(string errorMessage, Exception innerException) : base(errorMessage, innerException) { }
    }
}
