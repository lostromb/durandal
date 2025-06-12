using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class OAuthToken
    {
        /// <summary>
        /// The secret token
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// The type of token this is, e.g. "Bearer"
        /// </summary>
        public string TokenType { get; set; }

        /// <summary>
        /// A secret token which can be used to refresh the main token when it expires
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// The time that this token was issued
        /// </summary>
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset IssuedAt { get; set; }

        /// <summary>
        /// The time that this token expires
        /// </summary>
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
