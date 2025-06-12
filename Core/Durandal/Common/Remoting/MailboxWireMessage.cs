using Durandal.Common.IO;
using Durandal.Common.Remoting.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    public class MailboxWireMessage
    {
        /// <summary>
        /// Indicates this wire message is the first of a potentially longer set of fragmented messages
        /// </summary>
        public const ushort FLAG_BEGIN = 0x1 << 0;

        /// <summary>
        /// Indicates this wire message is the last of a set of fragmented messages. If the entire message is a single packet then both begin + end will be set
        /// </summary>
        public const ushort FLAG_END = 0x1 << 1;

        public uint MailboxId { get; set; }
        public uint ProtocolId { get; set; }

        public uint MessageId { get; set; }
        public uint ReplyToId { get; set; }
        public ushort Flags { get; set; }
        public ushort Length { get; set; }
        public uint Checksum { get; set; }
        public PooledBuffer<byte> Buffer { get; set; }

        public MailboxWireMessage(
            uint mailboxId,
            uint protocolId,
            PooledBuffer<byte> buffer,
            uint messageId,
            uint replyToId,
            ushort flags)
        {
            MailboxId = mailboxId;
            ProtocolId = protocolId;
            Buffer = buffer;
            MessageId = messageId;
            ReplyToId = replyToId;
            Flags = flags;
        }

        public void DisposeOfBuffer()
        {
            Buffer?.Dispose();
            Buffer = null;
        }
    }
}
