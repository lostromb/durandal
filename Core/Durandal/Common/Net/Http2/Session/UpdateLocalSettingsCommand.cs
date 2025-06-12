using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class UpdateLocalSettingsCommand : ISessionCommand
    {
        public UpdateLocalSettingsCommand(
            Http2Settings newSettings)
        {
            NewSettings = newSettings;
        }

        public Http2Settings NewSettings { get; private set; }
    }
}
