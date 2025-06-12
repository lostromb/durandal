using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.ICM
{
    public class DRITeam
    {
        /// <summary>
        /// The friendly name of this team, used within Photon only
        /// </summary>
        public string TeamFriendlyName { get; set; }

        /// <summary>
        /// The team's ID in ICM, for example "BINGPLATCORTANALANGUAGEUNDERSTANDING\ConversationsEngineering"
        /// </summary>
        public string IcmId { get; set; }

        /// <summary>
        /// The GUID of the ICM connector that hooks up to this team
        /// </summary>
        public Guid ConnectorId { get; set; }

        /// <summary>
        /// The thumbprint of the certificate to use for the ICM connector
        /// </summary>
        public string CertThumbprint { get; set; }
    }
}
