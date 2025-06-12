using System;
using System.Collections.Generic;
using System.IdentityModel.Protocols.WSTrust;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;

namespace Photon.Common.JWT
{
    public class JwtHelper
    {
        public static string GetJwt(X509Certificate2 certificate, string audience, string issuer)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (string.IsNullOrWhiteSpace(audience))
            {
                throw new ArgumentException("Value cannot be empty", nameof(audience));
            }

            if (string.IsNullOrWhiteSpace(issuer))
            {
                throw new ArgumentException("Value cannot be empty", nameof(issuer));
            }

            var now = DateTime.Now;
            var descriptor = new SecurityTokenDescriptor
            {
                EncryptingCredentials = new X509EncryptingCredentials(certificate),
                SigningCredentials = new X509SigningCredentials(certificate, SecurityAlgorithms.RsaSha256Signature, SecurityAlgorithms.Sha256Digest),
                TokenIssuerName = issuer,
                AppliesToAddress = audience,
                Lifetime = new Lifetime(now.AddMinutes(-5), now.AddMinutes(5))
            };
            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(descriptor) as JwtSecurityToken;
            var data = handler.WriteToken(token);
            return data;
        }

        public static IDictionary<string, string> ReadClaimsFromJwt(AuthenticationHeaderValue authorization)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException("authorization");
            }

            if (string.IsNullOrEmpty(authorization.Parameter))
            {
                throw new ArgumentNullException("authorization parameter");
            }

            var jwtHandler = new JwtSecurityTokenHandler();
            var token = authorization.Parameter;

            var tokenInfo = jwtHandler.ReadToken(token) as JwtSecurityToken;
            var claims = tokenInfo.Claims.ToDictionary(x => x.Type, x => x.Value, StringComparer.OrdinalIgnoreCase);
            return claims;
        }
    }
}
