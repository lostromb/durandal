using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.Util
{
    public enum SslCertValidationProcedure
    {
        /// <summary>
        /// Standard SSL validation. Ensures that the remote certificate is currently valid, issued by a trusted CA, and not on a revocation list
        /// </summary>
        Standard,

        /// <summary>
        /// SSL validation which promiscously allows any certificate regardless of expiration or trust level
        /// </summary>
        IgnoreErrors,

        /// <summary>
        /// Same as standard, but will also reject certificates which are due to expire within the next 7 days
        /// </summary>
        StandardPlusNearExpiry
    }
}
