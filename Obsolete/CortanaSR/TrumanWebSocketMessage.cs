using Durandal.Common.IO;
using Durandal.Common.Speech.SR.Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class TrumanWebSocketMessage
    {
        public IDictionary<string, string> Headers = new Dictionary<string, string>();
        public string Content;

        public static TrumanWebSocketMessage Parse(SkymanWebsocketMessage message)
        {
            byte[] packet = message.Data;
            TrumanWebSocketMessage returnVal = new TrumanWebSocketMessage();
            // Find the '\r\n\r\n' which delimits the data
            int headerLength = 0;
            while (headerLength < packet.Length - 4 &&
                !(packet[headerLength + 0] == (byte)'\r' &&
                packet[headerLength + 1] == (byte)'\n' &&
                packet[headerLength + 2] == (byte)'\r' &&
                packet[headerLength + 3] == (byte)'\n'))
            {
                headerLength++;
            }

            headerLength = headerLength + 4;

            string allHeaders = Encoding.UTF8.GetString(packet, 0, headerLength);
            string[] headerParts = allHeaders.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in headerParts)
            {
                int sep = part.IndexOf(":");
                if (sep < 0)
                {
                    continue;
                }

                if (sep < part.Length - 1)
                {
                    returnVal.Headers[part.Substring(0, sep)] = part.Substring(sep + 1);
                }
                else
                {
                    returnVal.Headers[part.Substring(0, sep)] = string.Empty;
                }
            }
            returnVal.Content = Encoding.UTF8.GetString(packet, headerLength, packet.Length - headerLength);
            return returnVal;
        }
    }
}
