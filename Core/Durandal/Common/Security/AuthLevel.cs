using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security
{
    public enum AuthLevel
    {
        /// <summary>
        /// The client has not provided any kind of authentication
        /// </summary>
        Unknown,

        /// <summary>
        /// The client has provided some authentication, but it is not fully trusted by the server
        /// </summary>
        Unverified,

        /// <summary>
        /// The client has provided authentication which was strictly rejected by the server
        /// </summary>
        Unauthorized,

        /// <summary>
        /// The client has attempted to use or reuse tokens which have already expired
        /// </summary>
        RequestExpired,

        /// <summary>
        /// The client has provided authentication that is accepted by the server and has not expired
        /// </summary>
        Authorized
    }
}
