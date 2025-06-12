using Stromberg.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Speech.SR.Remote;

namespace DurandalPiClient.SR
{
    public class MonoSocketProvider : ISRSocketProvider
    {
        private string _hostname;
        private int _port;

        public MonoSocketProvider(string hostName, int port)
        {
            _hostname = hostName;
            _port = port;
        }

        public string GetConnectionString()
        {
            return _hostname + ":" + _port;
        }

        public ISRSocket Connect(ILogger logger)
        {
            MonoSocket returnVal = new MonoSocket(logger);

            try
            {
                returnVal.Connect(_hostname, _port);
            }
            catch (Exception e)
            {
                logger.Log("Exception occurred while creating new MonoSocket: " + e.GetType().Name + " " + e.Message, LogLevel.Err);
                returnVal = null;
            }

            return returnVal;
        }
    }
}
