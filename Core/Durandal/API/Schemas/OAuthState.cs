using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class OAuthState
    {
        /// <summary>
        /// The configuration that generated this state
        /// </summary>
        public OAuthConfig Config { get; set; }

        /// <summary>
        /// The user's current oauth token, if any
        /// </summary>
        public OAuthToken Token { get; set; }

        /// <summary>
        /// A random unique identifier for this state
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// The plugin domain that owns and manages this secret
        /// </summary>
        public string DurandalPluginId { get; set; }

        /// <summary>
        /// The ID of the user that owns this secret
        /// </summary>
        public string DurandalUserId { get; set; }

        /// <summary>
        /// If the auth flow uses PKCE, this is the verifier token that was originally sent with the auth request
        /// </summary>
        public string PKCECodeVerifier { get; set; }

        /// <summary>
        /// Stores the temporary auth code that was returned as part of the authorization grant flow
        /// </summary>
        public string AuthCode { get; set; }

        /// <summary>
        /// The timestamp that the auth code was received
        /// </summary>
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset AuthCodeIssuedAt { get; set; }

        /// <summary>
        /// The trace ID that was set when the state was originally created
        /// </summary>
        public string OriginalTraceId { get; set; }
    }
}
