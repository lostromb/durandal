using Durandal.Common.IO.Hashing;
using Durandal.Common.Security;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class OAuthConfig
    {
        /// <summary>
        /// The variant of authorization to use. Currently only OAuth2 is supported
        /// </summary>
        public OAuthFlavor Type { get; set; }

        /// <summary>
        /// A unique name for this configuration if one plugin uses multiple configs
        /// </summary>
        public string ConfigName { get; set; }

        /// <summary>
        /// OAuth client id
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// OAuth client secret
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// URL on the remote service where a token can be obtained
        /// </summary>
        public string TokenUri { get; set; }

        /// <summary>
        /// URL on the remote service that presents an authorization page
        /// </summary>
        public string AuthUri { get; set; }

        /// <summary>
        /// Scope string which defines how this token should be used
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// If true, use PKCE verification when obtaining the token
        /// </summary>
        public bool UsePKCE { get; set; }

        /// <summary>
        /// If your token endpoint requires an Authorization header, set the value of that header here
        /// </summary>
        public string AuthorizationHeader { get; set; }

        //public string UserAgent { get; set; }
        //public string AuthProviderX509CertUrl { get; set; }
        //public string LoginHint { get; set; }
        //public string DeviceUri { get; set; }
        //public bool CodeVerifier { get; set; }
        //public string RevokeUri { get; set; }
        //public string Domain { get; set; }

        public byte[] HashConfiguration()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder configBuilder = pooledSb.Builder;
                configBuilder.Append(AuthorizationHeader ?? "NULL");
                configBuilder.Append(AuthUri ?? "NULL");
                configBuilder.Append(ClientId ?? "NULL");
                configBuilder.Append(ClientSecret ?? "NULL");
                configBuilder.Append(ConfigName ?? "NULL");
                configBuilder.Append(Scope ?? "NULL");
                configBuilder.Append(TokenUri ?? "NULL");
                switch (Type)
                {
                    case OAuthFlavor.OAuth2:
                        configBuilder.Append("OAuth2");
                        break;
                }
                configBuilder.Append(UsePKCE.ToString());

                byte[] rawBytes = Encoding.UTF8.GetBytes(configBuilder.ToString());
                SHA256 hasher = new SHA256();
                return hasher.ComputeHash(rawBytes);
            }
        }
    }
}
