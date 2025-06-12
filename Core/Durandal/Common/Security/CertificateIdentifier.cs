using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Security
{
    /// <summary>
    /// Identifies a unique security certificate, by thumbprint, subject name, etc.
    /// Only one of the fields actually needs to be populated.
    /// </summary>
    public class CertificateIdentifier : IEquatable<CertificateIdentifier>
    {
        /// <summary>
        /// The exact thumbprint of the certificate.
        /// </summary>
        public string Thumbprint { get; private set; }

        /// <summary>
        /// The subject name of the certificate (for SSL certs, this is the domain name)
        /// </summary>
        public string SubjectName { get; private set; }

        /// <summary>
        /// The subject distinguished name of the certificate
        /// </summary>
        public string SubjectDistinguishedName { get; private set; }

        /// <summary>
        /// Hide constructor so consumers have to use the predefined static factory methods.
        /// </summary>
        private CertificateIdentifier()
        {
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals((CertificateIdentifier)obj);
        }

        public bool Equals(CertificateIdentifier other)
        {
            return other != null &&
                string.Equals(Thumbprint, other.Thumbprint, StringComparison.Ordinal) &&
                string.Equals(SubjectName, other.SubjectName, StringComparison.Ordinal) &&
                string.Equals(SubjectDistinguishedName, other.SubjectDistinguishedName, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            int returnVal = 0;
            unchecked
            {
                if (Thumbprint != null)
                {
                    returnVal ^= Thumbprint.GetHashCode() * 0xA3;
                }
                if (SubjectName != null)
                {
                    returnVal ^= SubjectName.GetHashCode() * 0x5E;
                }
                if (SubjectDistinguishedName != null)
                {
                    returnVal ^= SubjectDistinguishedName.GetHashCode() * 0x38;
                }
            }

            return returnVal;
        }

        public override string ToString()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                bool first = true;
                if (!string.IsNullOrEmpty(SubjectDistinguishedName))
                {
                    pooledSb.Builder.AppendFormat("SDN={0}", SubjectDistinguishedName);
                    first = false;
                }

                if (!string.IsNullOrEmpty(SubjectName))
                {
                    if (!first)
                    {
                        pooledSb.Builder.Append(' ');
                    }

                    pooledSb.Builder.AppendFormat("SN={0}", SubjectName);
                    first = false;
                }

                if (!string.IsNullOrEmpty(Thumbprint))
                {
                    if (!first)
                    {
                        pooledSb.Builder.Append(' ');
                    }

                    pooledSb.Builder.AppendFormat("TP={0}", Thumbprint);
                }

                return pooledSb.Builder.ToString();
            }
        }

        /// <summary>
        /// Creates a certificate identifier that matches a cert with a specific thumbprint
        /// </summary>
        /// <param name="thumbprint">The thumbprint to use</param>
        /// <returns>A certificate identifier</returns>
        public static CertificateIdentifier ByThumbprint(string thumbprint)
        {
            return new CertificateIdentifier()
            {
                Thumbprint = thumbprint.AssertNonNullOrEmpty(nameof(thumbprint))
            };
        }

        /// <summary>
        /// Creates a certificate identifier that matches a cert with a specific subject name
        /// </summary>
        /// <param name="subjectName">The subject name to use</param>
        /// <returns>A certificate identifier</returns>
        public static CertificateIdentifier BySubjectName(string subjectName)
        {
            return new CertificateIdentifier()
            {
                SubjectName = subjectName.AssertNonNullOrEmpty(nameof(subjectName))
            };
        }

        /// <summary>
        /// Creates a certificate identifier that matches a cert with a specific subject distinguished name
        /// </summary>
        /// <param name="subjectDistinguishedName">The subject name to use</param>
        /// <returns>A certificate identifier</returns>
        public static CertificateIdentifier BySubjectDistinguishedName(string subjectDistinguishedName)
        {
            return new CertificateIdentifier()
            {
                SubjectDistinguishedName = subjectDistinguishedName.AssertNonNullOrEmpty(nameof(subjectDistinguishedName))
            };
        }
    }
}
