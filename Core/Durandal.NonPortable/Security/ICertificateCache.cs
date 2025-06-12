namespace Durandal.Common.Security
{
    using Durandal.Common.Time;
    using System.Collections;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Represents a provider of X509 certificates which are loaded and cached on the locally running machine.
    /// </summary>
    public interface ICertificateCache
    {
        /// <summary>
        /// Tries to fetch certificate by certificate type.
        /// <remarks>
        /// The certificate will be first looked up both in <see cref="StoreLocation.CurrentUser"/>
        /// store and if not found, then in <see cref="StoreLocation.LocalMachine"/> store as well.
        /// Only certificates with a private key will be returned.
        /// </remarks>
        /// </summary>
        /// <param name="certificateId">Identity of the certificate you are looking for.</param>
        /// <param name="realTime">A definition of wallclock time.</param>
        /// <param name="certificate">Certificate to return.</param>
        /// <returns>True if the certificate was found.</returns>
        bool TryGetCertificate(
            CertificateIdentifier certificateId,
            IRealTimeProvider realTime,
            out X509Certificate2 certificate);

        /// <summary>
        /// Tries to fetch certificate by certificate type and store location.
        /// </summary>
        /// <remarks>
        /// Only valid certificates with a private key will be returned.
        /// </remarks>
        /// <param name="certificateId">Identity of the certificate you are looking for.</param>
        /// <param name="storeName">The name of the store to look in (usually "My")</param>
        /// <param name="storeLocation">The certificate store location used for lookup.</param>
        /// <param name="realTime">A definition of wallclock time.</param>
        /// <param name="certificate">Certificate to return.</param>
        /// <returns>True if the certificate was found.</returns>
        bool TryGetCertificate(CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            IRealTimeProvider realTime,
            out X509Certificate2 certificate);

        /// <summary>
        /// Gets certificate by certificate type.
        /// </summary>
        /// <remarks>
        /// The certificate will be first looked up both in <see cref="StoreLocation.CurrentUser"/>
        /// store and if not found, then in <see cref="StoreLocation.LocalMachine"/> store as well.
        /// Throws an exception if no certificate is found.
        /// Only valid certificates with a private key will be returned.
        /// </remarks>
        /// <param name="certificateId">Identity of the certificate you are looking for.</param>
        /// <param name="realTime">A definition of wallclock time.</param>
        /// <returns>The certificate instance.</returns>
        X509Certificate2 GetCertificate(
            CertificateIdentifier certificateId,
            IRealTimeProvider realTime);

        /// <summary>
        /// Gets certificate by certificate type and store location.
        /// </summary>
        /// <remarks>
        /// Throws an exception if no certificate is found.
        /// Only valid certificates with a private key will be returned.
        /// </remarks>
        /// <param name="certificateId">Identity of the certificate you are looking for.</param>
        /// <param name="storeName">The name of the store to look in (usually "My")</param>
        /// <param name="storeLocation">The certificate store location used for lookup.</param>
        /// <param name="realTime">A definition of wallclock time.</param>
        /// <returns>The certificate instance</returns>
        X509Certificate2 GetCertificate(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            IRealTimeProvider realTime);

        /// <summary>
        /// Tries to fetch certificate collection by certificate type.
        /// </summary>
        /// <remarks>
        /// The set of returned certificates will be a union of all valid certs found in both 
        /// <see cref="StoreLocation.CurrentUser"/> and <see cref="StoreLocation.LocalMachine"/> stores.
        /// Only valid certificates with a private key will be returned.
        /// </remarks>
        /// <param name="certificateId">Identity of the certificate you are looking for.</param>
        /// <param name="realTime">A definition of wallclock time.</param>
        /// <param name="certificates">Certificates to return</param>
        /// <returns>True if any certificate was found.</returns>
        bool TryGetCertificates(
            CertificateIdentifier certificateId,
            IRealTimeProvider realTime,
            out X509Certificate2Collection certificates);

        /// <summary>
        /// Gets certificate collection by certificate type and store location.
        /// </summary>
        /// <remarks>
        /// The set of returned certificates will be a union of all valid certs found in both 
        /// <see cref="StoreLocation.CurrentUser"/> and <see cref="StoreLocation.LocalMachine"/> stores.
        /// Only valid certificates with a private key will be returned.
        /// </remarks>
        /// <param name="certificateId">Identity of the certificate you are looking for.</param>
        /// <param name="storeName">The name of the store to look in (usually "My")</param>
        /// <param name="storeLocation">The certificate store location used for lookup.</param>
        /// <param name="realTime">A definition of wallclock time.</param>
        /// <param name="certificates">Certificates to return.</param>
        /// <returns>True if any certificate was found.</returns>
        bool TryGetCertificates(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            IRealTimeProvider realTime,
            out X509Certificate2Collection certificates);

        /// <summary>
        /// Gets set of certificates by certificate type.
        /// </summary>
        /// <remarks>
        /// The set of returned certificates will be a union of all valid certs found in both 
        /// <see cref="StoreLocation.CurrentUser"/> and <see cref="StoreLocation.LocalMachine"/> stores.
        /// Throws an exception if no certificates are found.
        /// Only valid certificates with a private key will be returned.
        /// </remarks>
        /// <param name="certificateId">The certificate identifier to look up.</param>
        /// <param name="realTime">A definition of wallclock time.</param>
        /// <returns>The collection of all found certificates.</returns>
        X509Certificate2Collection GetCertificates(
            CertificateIdentifier certificateId,
            IRealTimeProvider realTime);

        /// <summary>
        /// Gets set of certificates by certificate type and store location.
        /// </summary>
        /// <remarks>
        /// The set of returned certificates will be a union of all valid certs found in both 
        /// <see cref="StoreLocation.CurrentUser"/> and <see cref="StoreLocation.LocalMachine"/> stores.
        /// Throws an exception if no certificates are found.
        /// Only valid certificates with a private key will be returned.
        /// </remarks>
        /// <param name="certificateId">Identity of the certificate you are looking for.</param>
        /// <param name="storeName">The name of the store to look in (usually "My")</param>
        /// <param name="storeLocation">The certificate store location used for lookup.</param>
        /// <param name="realTime">A definition of wallclock time.</param>
        /// <returns>The collection of all found certificates.</returns>
        X509Certificate2Collection GetCertificates(
            CertificateIdentifier certificateId,
            StoreName storeName,
            StoreLocation storeLocation,
            IRealTimeProvider realTime);
    }
}
