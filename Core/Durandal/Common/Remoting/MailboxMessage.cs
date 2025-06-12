using Durandal.Common.IO;
using Durandal.Common.Remoting.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// Represents a single message sent via a <see cref="PostOffice"/>.
    /// Contains mailbox info, message IDs, the protocol ID, and the actual data buffer of the message.
    /// </summary>
    public class MailboxMessage
    {
        public MailboxId MailboxId { get; set; }
        public uint ProtocolId { get; set; }
        public uint MessageId { get; set; }
        public uint ReplyToId { get; set; }
        public PooledBuffer<byte> Buffer { get; set; }

        public MailboxMessage(MailboxId id, uint protocolId, PooledBuffer<byte> buffer, uint messageId = 0, uint replyToId = 0)
        {
            MailboxId = id;
            ProtocolId = protocolId;
            Buffer = buffer;
            MessageId = messageId;
            ReplyToId = replyToId;
        }

        /// <summary>
        /// Since mailbox messages use pooled buffers as their backing memory store, you need to manually
        /// free that buffer when you are done processing the message.
        /// This is automatically done by the post office after sending the message on the wire.
        /// </summary>
        public void DisposeOfBuffer()
        {
            Buffer?.Dispose();
            Buffer = null;
        }
    }
}
