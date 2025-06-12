using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Speech.SR.Remote;
using Stromberg.Logger;

namespace DurandalPiClient.SR
{
    public class MonoSocket : ISRSocket, IDisposable
    {
        private ILogger _logger;
        private Socket _socket;

        public MonoSocket(ILogger logger)
        {
            _logger = logger;
        }

        public bool Connect(string hostName, int port)
        {
            _socket = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp);

            _socket.Connect(hostName, port);
            return true;
        }
        
        public int Receive(byte[] targetBuffer, int offset, int size, int timeout)
        {
            _socket.ReceiveTimeout = timeout;
            return MonoSocketHelpers.ReliableRecieve(targetBuffer, offset, size, _socket);
        }

        public int Send(byte[] sourceBuffer, int offset, int size, int timeout)
        {
            _socket.SendTimeout = timeout;
            return MonoSocketHelpers.ReliableSend(sourceBuffer, offset, size, _socket);
        }

        public void Close()
        {
            _logger.Log("Socket closed");
            _socket.Close();
        }

        public void Dispose()
        {
            _logger.Log("Socket disposed");
            _socket.Dispose();
        }
    }
}
