using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Utils
{
    public class TokenRefreshResult<T>
    {
        /// <summary>
        /// Indicates that the token refresh was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Indicates that the token refresh explicitly returned an unauthorized response, indicating that the token is invalid or revoked
        /// </summary>
        public bool ExplicitlyDenied { get; set; }

        /// <summary>
        /// If refresh was successful, this is the token
        /// </summary>
        public T Token { get; set; }

        /// <summary>
        /// Indicates the actual amount of time that the token will be valid
        /// </summary>
        public TimeSpan? TokenLifetime { get; set; }

        /// <summary>
        /// In cases where token refresh may be throttled (such as with HTTP 429 response), you can return this value
        /// to indicate how long the refresher should wait until trying again
        /// </summary>
        public TimeSpan? SuggestedBackoffTime { get; set; }

        /// <summary>
        /// An error message that indicates why token fetching failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Creates a new successful token refresh response
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="tokenLifetime">The token's lifetime</param>
        public TokenRefreshResult(T token, TimeSpan tokenLifetime)
        {
            Success = true;
            ExplicitlyDenied = false;
            Token = token;
            SuggestedBackoffTime = null;
            TokenLifetime = tokenLifetime;
            ErrorMessage = null;
        }

        /// <summary>
        /// Creates a new failure token refresh response
        /// </summary>
        /// <param name="explicitlyDenied">Hints that the service has explicitly rejected this request (for invalid credentials, etc.) and that immediately retrying will likely not resolve the issue.</param>
        /// <param name="errorMessage">An error message, if any</param>
        /// <param name="suggestedBackoffTime">The minimum amount of time we should wait before attempting to fetch this token again</param>
        public TokenRefreshResult(bool explicitlyDenied, string errorMessage, TimeSpan? suggestedBackoffTime = null)
        {
            Success = false;
            ExplicitlyDenied = explicitlyDenied;
            Token = default(T);
            TokenLifetime = null;
            SuggestedBackoffTime = suggestedBackoffTime;
            ErrorMessage = errorMessage;
        }
    }
}
