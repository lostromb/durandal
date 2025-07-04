﻿using System;

namespace Durandal.Common.Logger
{
    /// <summary>
    /// Data privacy classification for privacy-compliant logs and instrumentation
    /// </summary>
    [Flags]
    public enum DataPrivacyClassification : ushort
    {
        /// <summary>
        /// Unclassified data
        /// </summary>
        Unknown = 0x0,

        /// <summary>
        /// System-generated data that does not belong to any other class, or in other words, is not linkable to any particular user or organization.
        /// Example: runtime log messages, traces, error messages, service status
        /// </summary>
        SystemMetadata = 0x1,

        /// <summary>
        /// Data that directly identifies or could be used to identify a user or organization.
        /// Examples: all or part of an email address, user name, display name, geolocation info, IP address, local machine name, email headers, etc.
        /// </summary>
        EndUserIdentifiableInformation = 0x2,

        /// <summary>
        /// Identifiers that uniquely designate an anonymous user and could reveal user identity if combined with other information.
        /// Examples: user ID GUIDs, session IDs, hashed or encrypted EUII, machine or device IDs
        /// </summary>
        EndUserPseudonymousIdentifiers = 0x4,

        /// <summary>
        /// Data that is generated and considered "private" by a user (i.e. not publically available). Also called User Content / Customer Content.
        /// Examples: user generated storage data (emails / images / files), individual owned secrets, machine built models over personal data,
        /// search query strings, and any dialog response that contains such data.
        /// </summary>
        PrivateContent = 0x8,

        /// <summary>
        /// Publically available personal data that comes from an external source.
        /// Examples: public user profiles, screen names, tweets, publically available content generated by this user such as videos or blog posts
        /// </summary>
        PublicPersonalData = 0x10,

        /// <summary>
        /// Data that is available publically and does not explicitly identify a single person.
        /// Examples: weather, news, stock prices
        /// </summary>
        PublicNonPersonalData = 0x20,
        
        All = SystemMetadata | EndUserIdentifiableInformation | EndUserPseudonymousIdentifiers | PrivateContent | PublicPersonalData | PublicNonPersonalData,
    }
}
