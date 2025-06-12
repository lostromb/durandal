using Durandal.Common.Net;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Compression;
using System.IO;
using Durandal.Common.Instrumentation;
using System.Diagnostics;
using Durandal.Common.Cache;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.IO.Crc;

namespace Durandal.Common.Remoting
{
    public class PostOffice : IDisposable, IMetricSource
    {
        private static readonly byte[] PACKET_HEADER = new byte[] { (byte)'D', (byte)'P', (byte)'o', (byte)'P' };
        // private static readonly uint PACKET_HEADER_4BYTE = ((uint)PACKET_HEADER[3] << 0) | ((uint)PACKET_HEADER[2] << 8) | ((uint)PACKET_HEADER[1] << 16) | ((uint)PACKET_HEADER[0] << 24);
        private const int WIRE_MESSAGE_HEADER_LENGTH = 36;
        private const uint MAX_TRANSIENT_BOXES = 1000000;
        private const int PRUNE_INTERVAL = 5;
        private const int MAX_BOXES_TO_PRUNE_AT_ONCE = 10;

        // Maximum amount of payload data we can send per fragment
        // Ensure that all fragments (plus header) can fit within 64Kb so they can rely on fast-path buffer pools
        // Fragment cannot have more than 64K of payload anyways because the size header is 2 bytes
        private const int MAX_FRAGMENT_MESSAGE_LENGTH = ushort.MaxValue - WIRE_MESSAGE_HEADER_LENGTH;

        private readonly ISocket _socket;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _socketLock; // Controls access to the socket WRITE pipe only
        private readonly FastConcurrentDictionary<uint, Mailbox> _transientMailboxes;
        private readonly FastConcurrentDictionary<uint, Mailbox> _permanentMailboxes;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly byte[] _wireHeaderScratchBuffer = new byte[WIRE_MESSAGE_HEADER_LENGTH];
        private readonly CancellationTokenSource _readTaskCancel;
        private readonly TimeSpan _maxBoxLifetime;
        private readonly FastRandom _rand;
        private readonly BufferedChannel<Tuple<MailboxId, DateTimeOffset>> _newlyCreatedMailboxes;
        private readonly RateCounter _transientBoxCreationRate;
        private readonly ConcurrentQueue<Mailbox> _reclaimedMailboxes = new ConcurrentQueue<Mailbox>();
        private readonly CarpoolAlgorithm<PostOfficeReaderState> _carpoolAlgorithm;
        private readonly PostOfficeReaderState _readerState;
        private readonly ICRC32C _crc;
        private readonly Task _readerThread;
        private readonly bool _useDedicatedThread;
        // preallocated delegates to avoid boxing
        private readonly CarpoolAlgorithm<PostOfficeReaderState>.CheckForCompletedWorkDelegate<StubType, Tuple<MailboxId, DateTimeOffset>> _newMailboxCreatedDelegate;
        private readonly CarpoolAlgorithm<PostOfficeReaderState>.CheckForCompletedWorkDelegate<MailboxId, MailboxMessage> _newMessageOnMailboxDelegate;
        private int _pruneCtr = 0;
        private int _nextMessageId = 0;
        private int _nextTransientBoxId = 0;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new post office which will manage incoming/outgoing messages on the given socket for the lifetime of that socket.
        /// </summary>
        /// <param name="socket">The socket to use. This post office will take ownership of the socket.</param>
        /// <param name="logger"></param>
        /// <param name="mailboxLifetime"></param>
        /// <param name="isServer"></param>
        /// <param name="realTime"></param>
        /// <param name="metrics"></param>
        /// <param name="metricDimensions"></param>
        /// <param name="useDedicatedThread"></param>
        public PostOffice(
            ISocket socket,
            ILogger logger,
            TimeSpan mailboxLifetime,
            bool isServer,
            IRealTimeProvider realTime = null,
            WeakPointer<IMetricCollector> metrics = default(WeakPointer<IMetricCollector>),
            DimensionSet metricDimensions = null,
            bool useDedicatedThread = false)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _socket = socket.AssertNonNull(nameof(socket));
            _logger = logger;
            _maxBoxLifetime = mailboxLifetime;
            _metrics = metrics.DefaultIfNull(NullMetricCollector.Singleton);
            _dimensions = metricDimensions ?? DimensionSet.Empty;
            _rand = new FastRandom();
            _socketLock = new SemaphoreSlim(1);
            _transientBoxCreationRate = new RateCounter(TimeSpan.FromSeconds(10), realTime);
            _transientMailboxes = new FastConcurrentDictionary<uint, Mailbox>();
            _permanentMailboxes = new FastConcurrentDictionary<uint, Mailbox>();
            _newlyCreatedMailboxes = new BufferedChannel<Tuple<MailboxId, DateTimeOffset>>(1);
            _nextTransientBoxId = isServer ? 0 : (int)(MAX_TRANSIENT_BOXES / 2);
            _crc = CRC32CFactory.Create();
            _readerState = new PostOfficeReaderState();
            _readTaskCancel = new CancellationTokenSource();
            _newMailboxCreatedDelegate = NewMailboxCreatedDelegate;
            _newMessageOnMailboxDelegate = NewMessageOnMailboxDelegate;
            _useDedicatedThread = useDedicatedThread;
            if (_useDedicatedThread)
            {
                CancellationToken readTaskCancelToken = _readTaskCancel.Token;
                IRealTimeProvider readerTaskThreadTime = realTime.Fork("PostOfficeReader");
                _readerThread = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
                {
                    _logger.Log("Post office reader thread started");
                    try
                    {
                        while (!readTaskCancelToken.IsCancellationRequested)
                        {
                            await DoSinglePostOfficeRead(_readerState, readTaskCancelToken, readerTaskThreadTime).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        _logger.Log("Post office reader thread stopped");
                        readerTaskThreadTime.Merge();
                    }
                });
            }
            else
            {
                _carpoolAlgorithm = new CarpoolAlgorithm<PostOfficeReaderState>(DoSinglePostOfficeRead);
            }

            _metrics.Value.AddMetricSource(this);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PostOffice()
        {
            Dispose(false);
        }
#endif

        public MailboxId CreateTransientMailbox(IRealTimeProvider realTime)
        {
            uint boxId = (uint)Interlocked.Increment(ref _nextTransientBoxId);
            while (boxId >= MAX_TRANSIENT_BOXES)
            {
                // If transient box ID exceeds the maximum, this resets it back to zero
                boxId = (uint)Interlocked.Add(ref _nextTransientBoxId, 0 - (int)MAX_TRANSIENT_BOXES);
            }
            
            MailboxId newBoxId = new MailboxId(boxId);
            Mailbox createdBox = CreateTransientMailboxInternal(newBoxId, realTime);
            PruneTransientMailboxesIfNeeded(realTime);
            _transientMailboxes[newBoxId.Id] = createdBox;
            return newBoxId;
        }

        /// <summary>
        /// Creates a new permanent mailbox ID that will never expire
        /// </summary>
        /// <param name="realTime">Real time, used for internal box operations</param>
        /// <param name="boxId">The "well-known" ID of this box, which you should establish out-of-band between your host and client.
        /// Think of this like the TCP port number. The protocol (e.g. HTTP) defines a well-known permanent port that it operates on (80).
        /// The box ID is analagous to that well-known port number.</param>
        /// <returns></returns>
        public MailboxId CreatePermanentMailbox(IRealTimeProvider realTime, ushort boxId)
        {
            MailboxId box = new MailboxId(boxId + MAX_TRANSIENT_BOXES);
            _permanentMailboxes[box.Id] = new Mailbox(box, realTime);
            //_logger.Log("Creating new permanent mailbox " + box.Id, LogLevel.Vrb);
            return box;
        }

        public async ValueTask<MailboxId> WaitForMessagesOnNewMailbox(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Tuple<MailboxId, DateTimeOffset> newBoxInfo;
            if (_useDedicatedThread)
            {
                newBoxInfo = await _newlyCreatedMailboxes.ReceiveAsync(cancelToken, realTime);
            }
            else
            {
                RetrieveResult<Tuple<MailboxId, DateTimeOffset>> rr = new RetrieveResult<Tuple<MailboxId, DateTimeOffset>>();
                while (!rr.Success)
                {
                    rr = await _carpoolAlgorithm.WorkOnePhase<StubType, Tuple<MailboxId, DateTimeOffset>>(_readerState, StubType.Empty, _newMailboxCreatedDelegate, cancelToken, realTime).ConfigureAwait(false);
                }

                newBoxInfo = rr.Result;
            }

            TimeSpan mailboxIdTransitTime = HighPrecisionTimer.GetCurrentUTCTime() - newBoxInfo.Item2;
#if PARANOID_MMIO_METRICS
            _metrics.Value.ReportPercentile("PO Mailbox Id Handoff ms", mailboxIdTransitTime.TotalMilliseconds, _dimensions);
#endif
            return newBoxInfo.Item1;
        }

        private ValueTask<RetrieveResult<Tuple<MailboxId, DateTimeOffset>>> NewMailboxCreatedDelegate(StubType dummyReaderInput, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _newlyCreatedMailboxes.TryReceiveAsync(cancelToken, realTime, TimeSpan.Zero);
        }

        public async ValueTask<RetrieveResult<MailboxMessage>> TryReceiveMessage(MailboxId boxId, CancellationToken cancelToken, TimeSpan timeout, IRealTimeProvider realTime)
        {
            if (_useDedicatedThread)
            {
                // Does the mailbox ID refer to a permanent box?
                Mailbox thisMailbox;
                if (boxId.Id >= MAX_TRANSIENT_BOXES)
                {
#pragma warning disable CA2000 // Dispose objects before losing scope
                    // This will atomically either fetch the existing mailbox, or create a new one if one doesn't exist already.
                    _permanentMailboxes.TryGetValueOrSet(boxId.Id, out thisMailbox, CreatePermanentMailboxInternal, boxId, realTime);
#pragma warning restore CA2000 // Dispose objects before losing scope
                }
                else
                {
                    if (!_transientMailboxes.TryGetValue(boxId.Id, out thisMailbox))
                    {
                        // FIXME wait for the transient box to be created?
                        return new RetrieveResult<MailboxMessage>();
                    }
                }

                Interlocked.Increment(ref _nextMessageId);

                return await thisMailbox.TryGetMessage(cancelToken, realTime, _logger, timeout).ConfigureAwait(false);
            }
            else
            {
                using (NonRealTimeCancellationTokenSource timeoutSource = new NonRealTimeCancellationTokenSource(realTime, timeout))
                using (CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancelToken))
                {
                    try
                    {
                        RetrieveResult<MailboxMessage> rr = new RetrieveResult<MailboxMessage>();
                        while (!rr.Success)
                        {
                            rr = await _carpoolAlgorithm.WorkOnePhase<MailboxId, MailboxMessage>(_readerState, boxId, _newMessageOnMailboxDelegate, linkedTokenSource.Token, realTime).ConfigureAwait(false);
                        }

                        return rr;
                    }
                    catch (OperationCanceledException) { }

                    return new RetrieveResult<MailboxMessage>();
                }
            }
        }

        private async ValueTask<RetrieveResult<MailboxMessage>> NewMessageOnMailboxDelegate(MailboxId boxId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Does the mailbox ID refer to a permanent box?
            Mailbox thisMailbox;
            if (boxId.Id >= MAX_TRANSIENT_BOXES)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                // This will atomically either fetch the existing mailbox, or create a new one if one doesn't exist already.
                _permanentMailboxes.TryGetValueOrSet(boxId.Id, out thisMailbox, CreatePermanentMailboxInternal, boxId, realTime);
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            else
            {
                if (!_transientMailboxes.TryGetValue(boxId.Id, out thisMailbox))
                {
                    // FIXME wait for the transient box to be created?
                    return new RetrieveResult<MailboxMessage>();
                }
            }

            Interlocked.Increment(ref _nextMessageId);

            return await thisMailbox.TryGetMessage(cancelToken, realTime, _logger, TimeSpan.Zero).ConfigureAwait(false);
        }

        public async ValueTask<MailboxMessage> ReceiveMessage(MailboxId boxId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_useDedicatedThread)
            {
                // Does the mailbox ID refer to a permanent box?
                Mailbox thisMailbox;
                if (boxId.Id >= MAX_TRANSIENT_BOXES)
                {
#pragma warning disable CA2000 // Dispose objects before losing scope
                    // This will atomically either fetch the existing mailbox, or create a new one if one doesn't exist already.
                    _permanentMailboxes.TryGetValueOrSet(boxId.Id, out thisMailbox, CreatePermanentMailboxInternal, boxId, realTime);
#pragma warning restore CA2000 // Dispose objects before losing scope
                }
                else
                {
                    if (!_transientMailboxes.TryGetValue(boxId.Id, out thisMailbox))
                    {
                        // FIXME wait for the transient box to be created?
                        return null;
                    }
                }

                Interlocked.Increment(ref _nextMessageId);

                return await thisMailbox.GetMessage(cancelToken, realTime, _logger).ConfigureAwait(false);
            }
            else
            {
                RetrieveResult<MailboxMessage> rr = new RetrieveResult<MailboxMessage>();
                while (!rr.Success)
                {
                    rr = await _carpoolAlgorithm.WorkOnePhase<MailboxId, MailboxMessage>(_readerState, boxId, _newMessageOnMailboxDelegate, cancelToken, realTime).ConfigureAwait(false);
                }

                return rr.Result;
            }
        }

        /// <summary>
        /// Generate a non-zero monotonously distinct message ID
        /// </summary>
        /// <returns></returns>
        public uint GenerateMessageId()
        {
            uint messageId = 0;
            while (messageId == 0)
            {
                messageId = (uint)Interlocked.Increment(ref _nextMessageId);
            }

            return messageId;
        }

        public async ValueTask SendMessage(MailboxMessage message, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ValueStopwatch operationTimer = ValueStopwatch.StartNew();
#if PARANOID_MMIO_METRICS
            double lastFragmentWriteTime = 0;
#endif
            // Fragment the message into packets, each containing 64Kb maximum.
            // This is done to prevent other communication on the channel from getting choked if a long message is sent

            // Total number of fragments = number of max-len packets               plus          an optional extra packet to hold any remainder
            int numFragments = (message.Buffer.Length / MAX_FRAGMENT_MESSAGE_LENGTH) + (message.Buffer.Length % MAX_FRAGMENT_MESSAGE_LENGTH == 0 ? 0 : 1);
            if (numFragments == 0)
            {
                numFragments = 1; // if the packet is explicitly zero-length for whatever reason, then we ensure that it gets sent as one fragment
            }

            int numBytesSent = 0;
            for (int fragmentId = 0; fragmentId < numFragments; fragmentId++)
            {
                // lastFragmentWriteTime = operationTimer.ElapsedMillisecondsPrecise();
                ushort thisFragmentLen = (ushort)FastMath.Min(MAX_FRAGMENT_MESSAGE_LENGTH, message.Buffer.Length - numBytesSent);
                ushort thisFragmentFlags = 0;
                if (fragmentId == 0)
                {
                    thisFragmentFlags |= MailboxWireMessage.FLAG_BEGIN;
                }
                if (fragmentId == numFragments - 1)
                {
                    thisFragmentFlags |= MailboxWireMessage.FLAG_END;
                }
                
                await _socketLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    //_logger.Log("Sending message fragment of length " + thisFragmentLen + " with id " + message.MessageId + " to box ID " + message.MailboxId.Id, LogLevel.Vrb);
                    // 4 bytes magic capture pattern
                    ArrayExtensions.MemCopy(PACKET_HEADER, 0, _wireHeaderScratchBuffer, 0, 4);
                    // 4 bytes uint mailbox ID
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(message.MailboxId.Id, _wireHeaderScratchBuffer, 4);
                    // 4 bytes int protocol ID
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(message.ProtocolId, _wireHeaderScratchBuffer, 8);
                    // 4 bytes int message ID and replyto ID
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(message.MessageId, _wireHeaderScratchBuffer, 12);
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(message.ReplyToId, _wireHeaderScratchBuffer, 16);
                    // 2 bytes ushort flags
                    BinaryHelpers.UInt16ToByteArrayLittleEndian(thisFragmentFlags, _wireHeaderScratchBuffer, 20);
                    // 2 bytes ushort payloadSize
                    BinaryHelpers.UInt16ToByteArrayLittleEndian(thisFragmentLen, _wireHeaderScratchBuffer, 22);
                    // 8 bytes send timestamp (for microprofiling)
                    BinaryHelpers.Int64ToByteArrayLittleEndian(HighPrecisionTimer.GetCurrentTicks(), _wireHeaderScratchBuffer, 24);
                    // Initialize the CRC to zero
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(0, _wireHeaderScratchBuffer, 32);

                    // Calculate CRC over the header and payload bytes so it can be validated by receiver
                    CRC32CState crcState = new CRC32CState();
                    _crc.Slurp(ref crcState, _wireHeaderScratchBuffer.AsSpan(0, WIRE_MESSAGE_HEADER_LENGTH));
                    _crc.Slurp(ref crcState, message.Buffer.Buffer.AsSpan(numBytesSent, thisFragmentLen));

                    // 4 bytes uint checksum
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(crcState.Checksum, _wireHeaderScratchBuffer, 32);
                    await _socket.WriteAsync(_wireHeaderScratchBuffer, 0, WIRE_MESSAGE_HEADER_LENGTH, cancelToken, realTime).ConfigureAwait(false);
                    // The rest is the payload
                    if (thisFragmentLen > 0)
                    {
                        await _socket.WriteAsync(message.Buffer.Buffer, numBytesSent, thisFragmentLen, cancelToken, realTime).ConfigureAwait(false);
                    }

                    // If the whole message is one fragment, just flush here while we have the lock
                    if (numFragments == 1)
                    {
                        await _socket.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                    }

                    //_metrics.Value.ReportPercentile("PO fragment size", _dimensions, thisFragmentLen);
                }
                finally
                {
                    _socketLock.Release();
                }

                numBytesSent += thisFragmentLen;
#if PARANOID_MMIO_METRICS
                _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_PostOffice_FragmentWriteTime, operationTimer.ElapsedMillisecondsPrecise() - lastFragmentWriteTime, _dimensions);
#endif
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_PostOffice_NumFragmentsSent, _dimensions, numFragments);
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_PostOffice_NumBytesSent, _dimensions, message.Buffer.Length + (numFragments * WIRE_MESSAGE_HEADER_LENGTH));
            }

            // If we send multiple fragments, wait until all of them have sent before flushing
            if (numFragments > 1)
            {
                await _socketLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _socket.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                }
                finally
                {
                    _socketLock.Release();
                }
            }

            message.DisposeOfBuffer();
            operationTimer.Stop();
#if PARANOID_MMIO_METRICS
            _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_PostOffice_WriteOperationTime, operationTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
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
                try
                {
                    _logger.Log("Disposing of post office", LogLevel.Vrb);
                    _metrics.Value.RemoveMetricSource(this);
                    _readTaskCancel.Cancel();

                    if (_useDedicatedThread)
                    {
                        _readerThread.Await();
                    }

                    foreach (KeyValuePair<uint, Mailbox> box in _transientMailboxes)
                    {
                        box.Value.Dispose();
                    }

                    foreach (KeyValuePair<uint, Mailbox> box in _permanentMailboxes)
                    {
                        box.Value.Dispose();
                    }

                    _transientMailboxes.Clear();
                    _permanentMailboxes.Clear();

                    _socketLock.Dispose();
                    _readTaskCancel.Dispose();
                    _newlyCreatedMailboxes.Dispose();

                    Mailbox reclaimedMailbox;
                    while (_reclaimedMailboxes.TryDequeue(out reclaimedMailbox))
                    {
                        reclaimedMailbox.Dispose();
                    }

                    // If there is dedicated reader thread, wait for it to finish
                    // Otherwise we could attempt to read from a disposed socket which throws exception
                    if (_readerThread != null)
                    {
                        try
                        {
                            _readerThread.Wait(TimeSpan.FromMilliseconds(1000));
                        }
                        catch (OperationCanceledException) { }
                    }

                    _socket.Dispose();
                    _transientBoxCreationRate.Dispose();
                }
                catch (Exception e)
                {
                    _logger.Log(e);
                }
            }
        }

        private Mailbox CreatePermanentMailboxInternal(MailboxId id, IRealTimeProvider realTime)
        {
            return new Mailbox(id, realTime);
        }

        private Mailbox CreateTransientMailboxInternal(MailboxId id, IRealTimeProvider realTime)
        {
            ValueStopwatch createTimer = ValueStopwatch.StartNew();
            //_logger.Log("Creating new mailbox " + box.Id, LogLevel.Vrb);
            Mailbox newTransientBox;
            if (_reclaimedMailboxes.TryDequeue(out newTransientBox))
            {
                newTransientBox.Reinitialize(id, realTime);
                createTimer.Stop();
                _metrics.Value.ReportInstant("PO Boxes Reclaimed/sec", _dimensions);
#if PARANOID_MMIO_METRICS
                _metrics.Value.ReportPercentile("PO Box Reclaim Time ms", createTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
            }
            else
            {
                newTransientBox = new Mailbox(id, realTime);
                createTimer.Stop();
                _metrics.Value.ReportInstant("PO Boxes Created/sec", _dimensions);
#if PARANOID_MMIO_METRICS
                _metrics.Value.ReportPercentile("PO Box Creation Time ms", createTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
            }

            _transientBoxCreationRate.Increment();

            return newTransientBox;
        }

        private void PruneTransientMailboxesIfNeeded(IRealTimeProvider realTime)
        {
            if (++_pruneCtr < PRUNE_INTERVAL)
            {
                return;
            }

            _pruneCtr = 0;
            // Based on the rate of mailboxes being created, calculate the "expected" number of boxes that should still be alive
            int expectedNumOfLiveBoxes = (int)(_transientBoxCreationRate.Rate * _maxBoxLifetime.TotalSeconds * 1.1); // 1.1 is to roughly account for the fact that mailboxes get used for a little while before becoming reclaimable
            int amountToPrune = FastMath.Min(MAX_BOXES_TO_PRUNE_AT_ONCE, (_transientMailboxes.Count - expectedNumOfLiveBoxes));

            if (amountToPrune > 0)
            {
                ValueStopwatch pruneTimer = ValueStopwatch.StartNew();
                int boxesActuallyPruned = 0;

                // Start enumerating at a random point in the dictionary
                using (IEnumerator<KeyValuePair<uint, Mailbox>> mailboxPruningEnumerator = _transientMailboxes.GetRandomEnumerator(_rand))
                {
                    while (amountToPrune-- > 0)
                    {
                        if (mailboxPruningEnumerator.MoveNext())
                        {
                            if (mailboxPruningEnumerator.Current.Value.IsExpired(_maxBoxLifetime, realTime))
                            {
                                // We can modify the dictionary while we are iterating over it only because it is a FastConcurrentDictionary
                                _reclaimedMailboxes.Enqueue(mailboxPruningEnumerator.Current.Value);
                                _transientMailboxes.Remove(mailboxPruningEnumerator.Current.Key);
                                //_logger.Log("Cleaned up expired mailbox " + key, LogLevel.Vrb);
                                boxesActuallyPruned++;
                            }
                        }
                    }
                }

                pruneTimer.Stop();
#if PARANOID_MMIO_METRICS
                _metrics.Value.ReportPercentile("PO Box Prune Time ms", pruneTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
                _metrics.Value.ReportInstant("PO Boxes Pruned/sec", _dimensions, boxesActuallyPruned);
            }
        }

        /// <summary>
        /// Captures the state of a theoretical post office message after we have spotted the capture pattern but before we have verified its checksum
        /// </summary>
        private class HypothesizedWireMessage
        {
            /// <summary>
            /// Index in the wire buffer of the first byte of the capture pattern
            /// </summary>
            public int CapturePatternStartIndex;

            /// <summary>
            /// Calculates the running checksum of this message's header + payload
            /// </summary>
            public CRC32CState CRC;

            /// <summary>
            /// If we have read the header already, this is the hypothesized length of the payload as read from the header
            /// </summary>
            public ushort PayloadLength;

            /// <summary>
            /// Total number of bytes we have read in this message, starting with first byte of capture pattern
            /// </summary>
            public int BytesRead;

            /// <summary>
            /// Flag to indicate that we have completely read the message + payload
            /// </summary>
            public bool Finished;

            /// <summary>
            /// True unless CRC validation has failed (meaning this is a garbage message)
            /// </summary>
            public bool Valid;

            /// <summary>
            /// True if we have accepted this hypothesis and sorted it into the mailbox
            /// </summary>
            public bool Sent;

            /// <summary>
            /// The timer count, in ticks, when the first fragment of this hypothesis appeared
            /// </summary>
            public long FirstFragmentTime;
        }

        private int _threadInCriticalArea = 0;

        /// <summary>
        /// Background task method for reading from the socket in a single-threaded way. This method will continuously poll the socket, read messages,
        /// and dispatch them to new or existing mailboxes to be processed by the application logic. The reader is also resilient to corruption of the
        /// input stream, using checksum validation to ensure that garbage data does not lead to garbage mailbox messages.
        /// </summary>
        /// <returns></returns>
        private async ValueTask DoSinglePostOfficeRead(PostOfficeReaderState readerState, CancellationToken readTaskCancelToken, IRealTimeProvider readerTaskThreadLocalTime)
        {
            if (Interlocked.CompareExchange(ref _threadInCriticalArea, 1, 0) != 0)
            {
                throw new InvalidOperationException("Multiple threads are inside the critical area");
            }

            // Wait for bytes to come in
            readerState.sortingTimer.Stop();
#if PARANOID_MMIO_METRICS
            _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_PostOffice_FragmentSortTime, readerState.sortingTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
            readerState.sortingTimer.Restart();
            int amountRead = await _socket.ReadAnyAsync(readerState.wireBuffer, readerState.wireBufferSize, PostOfficeReaderState.CHUNK_SIZE, readTaskCancelToken, readerTaskThreadLocalTime).ConfigureAwait(false);
            readerState.wireBufferSize += amountRead;
            readerState.sortingTimer.Stop();
#if PARANOID_MMIO_METRICS
            _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_PostOffice_ReadAnyTime, readerState.sortingTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
            readerState.sortingTimer.Restart();
            long fragmentReceiveTime = readerState.ongoingTimer.ElapsedTicks;

            readTaskCancelToken.ThrowIfCancellationRequested();

            if (amountRead > 0)
            {
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_PostOffice_NumBytesRead, _dimensions, amountRead);
            }

            bool anyMessageFinished = false;

            // Scan for new capture patterns
            for (int wireBufferProcessedIdx = readerState.wireBufferSize - amountRead; wireBufferProcessedIdx < readerState.wireBufferSize; wireBufferProcessedIdx++)
            {
                if (readerState.capturePatternMatcher.Match(readerState.wireBuffer[wireBufferProcessedIdx]))
                {
                    // Found a capture pattern - make a new hypothesis
                    CRC32CState crcState = new CRC32CState();
                    _crc.Slurp(ref crcState, PACKET_HEADER.AsSpan());

                    readerState.wireMessageHypotheses.Enqueue(new HypothesizedWireMessage()
                    {
                        CapturePatternStartIndex = wireBufferProcessedIdx - PACKET_HEADER.Length + 1,
                        CRC = crcState,
                        PayloadLength = 0,
                        BytesRead = 4,
                        Finished = false,
                        Valid = true,
                        Sent = false,
                        FirstFragmentTime = fragmentReceiveTime
                    });
                }
            }

            // Process existing hypotheses
            foreach (HypothesizedWireMessage messageHypothesis in readerState.wireMessageHypotheses)
            {
                // Process the bytes we just read; update each hypothesized message according to how far the current byte is from the capture pattern
                for (int wireBufferProcessedIdx = readerState.wireBufferSize - amountRead; wireBufferProcessedIdx < readerState.wireBufferSize; wireBufferProcessedIdx++)
                {
                    if (messageHypothesis.Finished)
                    {
                        break;
                    }

                    // Potentially skip the first part of the buffer if we have hit a match far out in the future but haven't processed the bytes leading up to it yet
                    if (wireBufferProcessedIdx < messageHypothesis.CapturePatternStartIndex + 4)
                    {
                        continue;
                    }

                    byte streamByte = readerState.wireBuffer[wireBufferProcessedIdx];

                    messageHypothesis.BytesRead++;

                    // Determine which region we are in
                    CRC32CState hypCRC = messageHypothesis.CRC;
                    if (messageHypothesis.BytesRead <= 32)
                    {
                        // Still reading header
                        _crc.Slurp(ref hypCRC, streamByte);
                    }
                    else if (messageHypothesis.BytesRead < 36)
                    {
                        // Normally the CRC field itself would be here but obviously we can't include that in the checksum so we inject zeroes
                        _crc.Slurp(ref hypCRC, 0);
                    }
                    else if (messageHypothesis.BytesRead == 36)
                    {
                        // Just finished reading header - need to extract the hypothesized payload length
                        _crc.Slurp(ref hypCRC, 0);
                        messageHypothesis.PayloadLength = BinaryHelpers.ByteArrayToUInt16LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 22);
                        if (messageHypothesis.PayloadLength == 0)
                        {
                            messageHypothesis.Finished = true;
                            anyMessageFinished = true;
                        }
                    }
                    else if (messageHypothesis.PayloadLength > 0)
                    {
                        // Reading payload
                        // OPT this is really slow to do it one byte at a time!
                        _crc.Slurp(ref hypCRC, streamByte);

                        // Reached end of payload
                        if (messageHypothesis.BytesRead == messageHypothesis.PayloadLength + WIRE_MESSAGE_HEADER_LENGTH)
                        {
                            messageHypothesis.Finished = true;
                            anyMessageFinished = true;
                        }
                    }
                    else
                    {
                        throw new Exception("Invalid state in resilient post office reader");
                    }

                    messageHypothesis.CRC = hypCRC;
                }
            }

            // Sort & validate finished messages
            if (anyMessageFinished)
            {
                foreach (HypothesizedWireMessage messageHypothesis in readerState.wireMessageHypotheses)
                {
                    if (messageHypothesis.CapturePatternStartIndex >= 0 &&
                        messageHypothesis.Finished &&
                        messageHypothesis.Valid &&
                        !messageHypothesis.Sent)
                    {
                        uint checksum = BinaryHelpers.ByteArrayToUInt32LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 32);
                        uint calculatedChecksum = (uint)messageHypothesis.CRC.Checksum;

                        if (calculatedChecksum != checksum)
                        {
                            messageHypothesis.Valid = false;
                            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_PostOffice_FragmentCRCFailures, _dimensions);
                            //_logger.Log("Incoming message failed CRC validation", LogLevel.Vrb);
                            continue;
                        }

                        uint mailboxId = BinaryHelpers.ByteArrayToUInt32LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 4);
                        uint protocolId = BinaryHelpers.ByteArrayToUInt32LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 8);
                        uint messageId = BinaryHelpers.ByteArrayToUInt32LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 12);
                        uint replyToId = BinaryHelpers.ByteArrayToUInt32LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 16);
                        ushort flags = BinaryHelpers.ByteArrayToUInt16LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 20);
                        ushort payloadLength = BinaryHelpers.ByteArrayToUInt16LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 22);
                        long messageTimestamp = BinaryHelpers.ByteArrayToInt64LittleEndian(readerState.wireBuffer, messageHypothesis.CapturePatternStartIndex + 24);

                        //_logger.Log("We got a message fragment of " + payloadLength + " bytes with id " + messageId + " going to mailbox " + mailboxId, LogLevel.Vrb);

                        // Instrument the transit time of this message fragment
                        long fragmentTransitTime = HighPrecisionTimer.GetCurrentTicks() - messageTimestamp;
                        _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_PostOffice_FragmentTransitTime, _dimensions, TimeSpan.FromTicks(fragmentTransitTime).TotalMilliseconds);

                        PooledBuffer<byte> payload = BufferPool<byte>.Rent(messageHypothesis.PayloadLength);
                        if (messageHypothesis.PayloadLength > 0)
                        {
                            ArrayExtensions.MemCopy(
                                readerState.wireBuffer,
                                messageHypothesis.CapturePatternStartIndex + WIRE_MESSAGE_HEADER_LENGTH,
                                payload.Buffer,
                                0,
                                messageHypothesis.PayloadLength);
                        }

                        MailboxWireMessage incomingWireMessage = new MailboxWireMessage(mailboxId, protocolId, payload, messageId, replyToId, flags);
                        MailboxId structuredMailboxId = new MailboxId(mailboxId);

                        // Push the message into the destination mailbox
                        Mailbox destinationMailbox;
                        if (mailboxId >= MAX_TRANSIENT_BOXES)
                        {
#pragma warning disable CA2000 // Dispose objects before losing scope
                            // Destination is a permanent mailbox. Do an atomic operation to either fetch existing or create a new mailbox
                            if (!_permanentMailboxes.TryGetValueOrSet(mailboxId, out destinationMailbox, CreatePermanentMailboxInternal, structuredMailboxId, readerTaskThreadLocalTime))
                            {
                                //_logger.Log("Sorting it into a new permanent mailbox " + structuredMailboxId.Id, LogLevel.Vrb);
                            }
                            else
                            {
                                //_logger.Log("Sorting it into an existing permanent mailbox", LogLevel.Vrb);
                            }
#pragma warning restore CA2000 // Dispose objects before losing scope

                            await destinationMailbox.PutMessageFragment(incomingWireMessage, readerTaskThreadLocalTime, _logger).ConfigureAwait(false);
                        }
                        else
                        {
                            // Destination is a transient mailbox
#pragma warning disable CA2000 // Dispose objects before losing scope
                            bool mailboxIsNew = (!_transientMailboxes.TryGetValueOrSet(mailboxId, out destinationMailbox,
                                CreateTransientMailboxInternal, structuredMailboxId, readerTaskThreadLocalTime));
#pragma warning restore CA2000 // Dispose objects before losing scope

                            await destinationMailbox.PutMessageFragment(incomingWireMessage, readerTaskThreadLocalTime, _logger).ConfigureAwait(false);
                            if (mailboxIsNew)
                            {
                                //_logger.Log("Sorting it into a new transient mailbox " + structuredMailboxId.Id, LogLevel.Vrb);
                                await _newlyCreatedMailboxes.SendAsync(new Tuple<MailboxId, DateTimeOffset>(structuredMailboxId, HighPrecisionTimer.GetCurrentUTCTime())).ConfigureAwait(false);

                                // When the other post office generates a new mailbox ID then we increment our local mailbox ID as well to keep the two sides roughly in sync
                                uint boxId = (uint)Interlocked.Increment(ref _nextTransientBoxId);
                                while (boxId >= MAX_TRANSIENT_BOXES)
                                {
                                    // If transient box ID exceeds the maximum, this resets it back to zero
                                    boxId = (uint)Interlocked.Add(ref _nextTransientBoxId, 0 - (int)MAX_TRANSIENT_BOXES);
                                }
                            }
                            else
                            {
                                //_logger.Log("Sorting it into an existing transient mailbox", LogLevel.Vrb);
                            }

                            PruneTransientMailboxesIfNeeded(readerTaskThreadLocalTime);
                        }

                        messageHypothesis.Sent = true;
                        // Alter the rest of the message hypotheses and advance the wire buffer
                        int consumedDataLength = WIRE_MESSAGE_HEADER_LENGTH + messageHypothesis.PayloadLength + messageHypothesis.CapturePatternStartIndex;
                        ArrayExtensions.MemMove(readerState.wireBuffer, consumedDataLength, 0, readerState.wireBufferSize - consumedDataLength);
                        readerState.wireBufferSize -= consumedDataLength;
                        foreach (HypothesizedWireMessage iter2 in readerState.wireMessageHypotheses)
                        {
                            iter2.CapturePatternStartIndex -= consumedDataLength;
                        }

#if PARANOID_MMIO_METRICS
                        // And finally, report some paranoid metrics
                        double msToReadEntireMessage = (1000d * (readerState.ongoingTimer.ElapsedTicks - messageHypothesis.FirstFragmentTime)) / (double)Stopwatch.Frequency;
                        _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_PostOffice_ReadSingleMessageTime, msToReadEntireMessage, _dimensions);
#endif
                    }
                }
            }

            // Prune finished hypothesized messages
            readerState.wireMessageHypothesesPruned.Clear();
            foreach (HypothesizedWireMessage hypothesizedMessage in readerState.wireMessageHypotheses)
            {
                if (!hypothesizedMessage.Finished && hypothesizedMessage.CapturePatternStartIndex >= 0)
                {
                    readerState.wireMessageHypothesesPruned.Enqueue(hypothesizedMessage);
                }
                else
                {
                    // Dispose of the hypothesis if it is finished or its start index has gone into the negative
                }
            }

            readerState.wireMessageHypothesesSwap = readerState.wireMessageHypotheses;
            readerState.wireMessageHypotheses = readerState.wireMessageHypothesesPruned;
            readerState.wireMessageHypothesesPruned = readerState.wireMessageHypothesesSwap;

            // And advance the wire buffer again if we've read some junk that doesn't contain any messages
            while (readerState.wireMessageHypotheses.Count > 0 &&
                readerState.wireMessageHypotheses.Peek().CapturePatternStartIndex > 0)
            {
                int consumedDataLength = readerState.wireMessageHypotheses.Peek().CapturePatternStartIndex;
                ArrayExtensions.MemMove(readerState.wireBuffer, consumedDataLength, 0, readerState.wireBufferSize - consumedDataLength);
                readerState.wireBufferSize -= consumedDataLength;
                foreach (HypothesizedWireMessage iter2 in readerState.wireMessageHypotheses)
                {
                    iter2.CapturePatternStartIndex -= consumedDataLength;
                }
            }

            // No hypotheses in sight (buffer is full of junk). Advance all but 4 bytes of the buffer
            // OPT if we begin hypotheses AFTER the capture pattern instead of AT the capture pattern then we don't have to preserve 4 bytes; just clear the buffer
            if (readerState.wireMessageHypotheses.Count == 0 && readerState.wireBufferSize > MAX_FRAGMENT_MESSAGE_LENGTH)
            {
                int consumedDataLength = readerState.wireBufferSize - 4;
                ArrayExtensions.MemMove(readerState.wireBuffer, consumedDataLength, 0, readerState.wireBufferSize - consumedDataLength);
                readerState.wireBufferSize -= consumedDataLength;
            }

            if (Interlocked.CompareExchange(ref _threadInCriticalArea, 0, 1) != 1)
            {
                throw new InvalidOperationException("Multiple threads are leaving the critical area");
            }
        }

        private class PostOfficeReaderState
        {
            public Queue<HypothesizedWireMessage> wireMessageHypotheses = new Queue<HypothesizedWireMessage>();
            public Queue<HypothesizedWireMessage> wireMessageHypothesesPruned = new Queue<HypothesizedWireMessage>();
            public Queue<HypothesizedWireMessage> wireMessageHypothesesSwap = null;
            public const int CHUNK_SIZE = 65536; // max size of a single read from the pipe
            public byte[] wireBuffer = new byte[MAX_FRAGMENT_MESSAGE_LENGTH + CHUNK_SIZE + WIRE_MESSAGE_HEADER_LENGTH];
            public int wireBufferSize = 0;
            public CapturePatternMatcher capturePatternMatcher = new CapturePatternMatcher(PACKET_HEADER);
            public Stopwatch ongoingTimer = Stopwatch.StartNew(); // Continuously running timer to track start + end timestamps on fragments
            public Stopwatch sortingTimer = Stopwatch.StartNew(); // Measures the time that we take to process the mailbox message after reading it from the socket
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_PostOffice_TransientBoxCount, _dimensions, _transientMailboxes.Count);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_PostOffice_PermanentBoxCount, _dimensions, _permanentMailboxes.Count);
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
        }
    }
}
