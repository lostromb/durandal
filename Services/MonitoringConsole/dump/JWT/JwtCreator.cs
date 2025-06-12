using Durandal.Common.Net;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Photon.Common.JWT
{
    public class JwtCreator
    {
        private readonly X509Certificate2 _certificate;
        private readonly string _thumbprint;
        private readonly string _audience;
        private readonly string _issuer;
        private readonly string _tokenPrefix;

        public JwtCreator(string thumbprint, string audience, string issuer, string tokenPrefix = null)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new ArgumentNullException(nameof(thumbprint));
            }
            if (string.IsNullOrEmpty(audience))
            {
                throw new ArgumentNullException(nameof(audience));
            }
            if (string.IsNullOrEmpty(issuer))
            {
                throw new ArgumentNullException(nameof(issuer));
            }

            _thumbprint = thumbprint;
            _audience = audience;
            _issuer = issuer;
            _tokenPrefix = tokenPrefix;
            _certificate = CertificateHelper.GetCertificateByThumbprint(_thumbprint);
            if (_certificate == null)
            {
                throw new Exception("Could not find JWT certificate with thumbprint " + _thumbprint);
            }
        }

        public string GetJwt()
        {
            string jwt = JwtHelper.GetJwt(_certificate, _audience, _issuer);

            if (!string.IsNullOrEmpty(_tokenPrefix))
            {
                return $"{_tokenPrefix}{jwt}";
            }
            else
            {
                return jwt;
            }
        }
    }
}
