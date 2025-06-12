using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting
{
    internal class Mailbox : IDisposable
    {
        // polling granularity for new messages; only applies in debug mode
        private const int MESSAGE_POLL_GRANULARITY = 10;

        private static readonly RecyclableMemoryStreamManager MailboxFragmentationStreamManager = new RecyclableMemoryStreamManager(ushort.MaxValue, ushort.MaxValue, 100 * ushort.MaxValue);

        private MailboxId _id;
        private Dictionary<uint, RecyclableMemoryStream> _fragments;
        private BufferedChannel<MailboxMessage> _messageQueue;
        private DateTimeOffset _lastMessageReceiveTime;
        private int _disposed = 0;

        public Mailbox(MailboxId id, IRealTimeProvider realTime)
        {
            _id = id;
            _messageQueue = new BufferedChannel<MailboxMessage>(MESSAGE_POLL_GRANULARITY);
            _lastMessageReceiveTime = realTime.Time;
            _fragments = new Dictionary<uint, RecyclableMemoryStream>(4);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~Mailbox()
        {
            Dispose(false);
        }
#endif

        public void Reinitialize(MailboxId newId, IRealTimeProvider realTime)
        {
            _id = newId;
            _lastMessageReceiveTime = realTime.Time;
            _fragments.Clear();
            _messageQueue.Clear();
        }

        public ValueTask PutMessageFragment(MailboxWireMessage fragment, IRealTimeProvider realTime, ILogger queryLogger)
        {
            // Is this a oneshot message?
            if ((fragment.Flags & MailboxWireMessage.FLAG_BEGIN) != 0 &&
                (fragment.Flags & MailboxWireMessage.FLAG_END) != 0)
            {
                MailboxMessage convertedMessage = new MailboxMessage(new MailboxId(fragment.MailboxId), fragment.ProtocolId, fragment.Buffer, fragment.MessageId, fragment.ReplyToId);
                return PutMessage(convertedMessage, realTime, queryLogger);
            }

            // It's a multipart message
            RecyclableMemoryStream fragmentStream;
            if ((fragment.Flags & MailboxWireMessage.FLAG_BEGIN) != 0)
            {
                // It's the first packet in a set - make a buffer to hold it
                fragmentStream = new RecyclableMemoryStream(MailboxFragmentationStreamManager);
                fragmentStream.Write(fragment.Buffer.Buffer, 0, fragment.Buffer.Length);
                fragment.DisposeOfBuffer();
                _fragments[fragment.MessageId] = fragmentStream;
            }
            else if ((fragment.Flags & MailboxWireMessage.FLAG_END) != 0)
            {
                // It's the last fragment in the set - defragment it now and put the completed message in the queue
                if (!_fragments.TryGetValue(fragment.MessageId, out fragmentStream))
                {
                    queryLogger.Log("Got END mailbox message before START; check your race conditions", LogLevel.Err);
                }
                else
                {
                    fragmentStream.Write(fragment.Buffer.Buffer, 0, fragment.Buffer.Length);
                    fragment.DisposeOfBuffer();
                    PooledBuffer<byte> recombinedBuffer = fragmentStream.ToPooledBuffer();
                    fragmentStream.Dispose();
                    _fragments.Remove(fragment.MessageId);
                    MailboxMessage convertedMessage = new MailboxMessage(new MailboxId(fragment.MailboxId), fragment.ProtocolId, recombinedBuffer, fragment.MessageId, fragment.ReplyToId);
                    return PutMessage(convertedMessage, realTime, queryLogger);
                }
            }
            else
            {
                // It's the middle packet in a long message. Add to existing queue
                if (!_fragments.TryGetValue(fragment.MessageId, out fragmentStream))
                {
                    queryLogger.Log("Got MIDDLE mailbox message before START; check your race conditions", LogLevel.Err);
                }
                else
                {
                    fragmentStream.Write(fragment.Buffer.Buffer, 0, fragment.Buffer.Length);
                    fragment.DisposeOfBuffer();
                }
            }

            return new ValueTask();
        }

        public ValueTask PutMessage(MailboxMessage message, IRealTimeProvider realTime, ILogger queryLogger)
        {
            //queryLogger.Log("Trying to put message in box " + _id.Id, LogLevel.Vrb);
            _lastMessageReceiveTime = realTime.Time;
            return _messageQueue.SendAsync(message);
        }

        public ValueTask<RetrieveResult<MailboxMessage>> TryGetMessage(CancellationToken cancelToken, IRealTimeProvider realTime, ILogger queryLogger, TimeSpan timeout)
        {
            //queryLogger.Log("Trying to fetch message from box " + _id.Id, LogLevel.Vrb);
            return _messageQueue.TryReceiveAsync(cancelToken, realTime, timeout);
        }

        public ValueTask<MailboxMessage> GetMessage(CancellationToken cancelToken, IRealTimeProvider realTime, ILogger queryLogger)
        {
            //queryLogger.Log("Trying to fetch message from box " + _id.Id, LogLevel.Vrb);
            return _messageQueue.ReceiveAsync(cancelToken, realTime);
        }

        public bool IsExpired(TimeSpan maxAllowedLifetime, IRealTimeProvider realTime)
        {
            return (realTime.Time - _lastMessageReceiveTime) > maxAllowedLifetime;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                foreach (RecyclableMemoryStream stream in _fragments.Values)
                {
                    stream.Dispose();
                }

                _fragments.Clear();
                _messageQueue.Dispose();
            }
        }
    }
}
