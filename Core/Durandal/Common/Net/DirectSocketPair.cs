using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net
{
    public class DirectSocketPair
    {
        public DirectSocket ClientSocket { get; private set; }
        public DirectSocket ServerSocket { get; private set; }

        public DirectSocketPair(DirectSocket client, DirectSocket server)
        {
            ClientSocket = client;
            ServerSocket = server;
        }
    }
}
