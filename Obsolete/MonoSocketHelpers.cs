using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DurandalPiClient.SR
{
    public static class MonoSocketHelpers
    {
        public static int ReadMessage(byte[] buffer, Socket socket, out ushort messageType)
        {
            if (buffer == null || buffer.Length < ushort.MaxValue)
            {
                throw new ArgumentException("The read buffer must be at least 64Kb in size");
            }

            ReliableRecieve(buffer, 0, 4, socket);
            int messageSize = BitConverter.ToUInt16(buffer, 0);
            messageType = BitConverter.ToUInt16(buffer, 2);
            ReliableRecieve(buffer, 0, messageSize, socket);
            return messageSize;
        }

        public static void WriteMessage(byte[] message, int messageSize, ushort messageType, Socket socket)
        {
            if (messageSize > ushort.MaxValue)
                throw new ArgumentException("Messages must be smaller than 64Kb");
            socket.Send(BitConverter.GetBytes((ushort)messageSize));
            socket.Send(BitConverter.GetBytes(messageType));
            if (messageSize > 0 && message != null && message.Length >= messageSize)
            {
                ReliableSend(message, 0, messageSize, socket);
            }
        }

        public static int ReliableRecieve(byte[] targetBuffer, int offset, int size, Socket socket)
        {
            int received = 0;
            const int CHUNK_SIZE = 256;
            while (received < size)
            {
                int nextPacketSize = Math.Min(CHUNK_SIZE, size - received);
                received += socket.Receive(targetBuffer, offset + received, nextPacketSize, SocketFlags.None);
            }
            return received;
        }

        public static int ReliableSend(byte[] sourceBuffer, int offset, int size, Socket socket)
        {
            int sent = 0;
            const int CHUNK_SIZE = 256;
            while (sent < size)
            {
                int thisPacketSize = Math.Min(CHUNK_SIZE, size - sent);
                sent += socket.Send(sourceBuffer, sent + offset, thisPacketSize, SocketFlags.None);
            }
            return sent;
        }
    }
}
