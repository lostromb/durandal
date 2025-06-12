using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class AcknowledgeSettingsCommand : ISessionCommand
    {
        public AcknowledgeSettingsCommand(
            Http2Settings remoteSettingsBeingAcknowledged)
        {
            RemoteSettingsBeingAcknowledged = remoteSettingsBeingAcknowledged;
        }

        public Http2Settings RemoteSettingsBeingAcknowledged { get; private set; }
    }
}
