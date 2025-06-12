namespace Durandal.Common.Security
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstracts the logic of <see cref="X509Store"/> which normally fetches certificates from the current machine's
    /// certificate store. Making it an interface simplifies unit tests.
    /// </summary>
    public interface IPhysicalCertificateStore : IDisposable
    {
        /// <summary>
        /// Fetches a list of certificates from a physical store.
        /// This method will not return expired and invalid certificates.
        /// </summary>
        /// <param name="certificateId">The criteria used to find certificates, either by subject name, thumbprint, etc.</param>
        /// <param name="storeName">The store name - usually StoreName.My.</param>
        /// <param name="storeLocation">The store location - local machine or current user.</param>
        /// <param name="withPrivateKey">If true, only return certs for which we have the private key.</param>
        /// <returns>An enumerable of 0 or more certificates.</returns>
        IEnumerable<X509Certificate2> GetCertificates(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            bool withPrivateKey);
    }
}
