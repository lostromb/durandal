using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation.Profiling
{
    public static class MicroProfiler
    {
        private static ReaderWriterLockMicro _clientLock = new ReaderWriterLockMicro();
        private static IMicroProfilerClient _client = null;
        private static int _operationId = 0;

        public static void Initialize(ref IMicroProfilerClient client, ILogger logger)
        {
            _clientLock.EnterWriteLock();
            try
            {
                IMicroProfilerClient oldClient = _client;
                _client = client;
                client = oldClient;
                if (oldClient != null)
                {
                    logger.Log("Switching microprofiler client while previous client is non-null; did you mean to initialize more than once?", LogLevel.Wrn);
                }
            }
            finally
            {
                _clientLock.ExitWriteLock();
            }
        }

        public static uint GenerateOperationId()
        {
            return (uint)Interlocked.Increment(ref _operationId);
        }

        public static void Send(MicroProfilingEventType type, uint operationId)
        {
            uint hLock = _clientLock.EnterReadLock();
            try
            {
                if (_client == null)
                {
                    return;
                }

                long timestamp = HighPrecisionTimer.GetCurrentTicks();

                // Length of packet = type           + timestamp    + thread ID   + operation ID
                ushort len = 2 + sizeof(ushort) + sizeof(long) + sizeof(int) + sizeof(uint);

                using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent())
                {
                    byte[] writeBuffer = pooledBuf.Buffer;
                    BinaryHelpers.UInt16ToByteArrayLittleEndian(len, writeBuffer, 0);          // Write packet length
                    BinaryHelpers.UInt16ToByteArrayLittleEndian((ushort)type, writeBuffer, 2); // Write packet message type
                    BinaryHelpers.Int64ToByteArrayLittleEndian(timestamp, writeBuffer, 4);     // Write packet timestamp
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(0, writeBuffer, 12);           // Write thread ID (this can be augmented down the line)
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(operationId, writeBuffer, 16); // Write operation ID
                    _client.SendProfilingData(writeBuffer, 0, len);
                }
            }
            finally
            {
                _clientLock.ExitReadLock(hLock);
            }
        }

        public static void Send(MicroProfilingEventType type, uint operationId, int arg0)
        {
            uint hLock = _clientLock.EnterReadLock();
            try
            {
                if (_client == null)
                {
                    return;
                }

                long timestamp = HighPrecisionTimer.GetCurrentTicks();

                // Length of packet = type           + timestamp    + thread ID   + operation ID + arg0
                ushort len =      2 + sizeof(ushort) + sizeof(long) + sizeof(int) + sizeof(uint) + sizeof(uint);

                using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent())
                {
                    byte[] writeBuffer = pooledBuf.Buffer;
                    BinaryHelpers.UInt16ToByteArrayLittleEndian(len, writeBuffer, 0);          // Write packet length
                    BinaryHelpers.UInt16ToByteArrayLittleEndian((ushort)type, writeBuffer, 2); // Write packet message type
                    BinaryHelpers.Int64ToByteArrayLittleEndian(timestamp, writeBuffer, 4);     // Write packet timestamp
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(0, writeBuffer, 12);           // Write thread ID (this can be augmented down the line)
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(operationId, writeBuffer, 16); // Write operation ID
                    BinaryHelpers.Int32ToByteArrayLittleEndian(arg0, writeBuffer, 20);         // Write arg0
                    _client.SendProfilingData(writeBuffer, 0, len);
                }
            }
            finally
            {
                _clientLock.ExitReadLock(hLock);
            }
        }

        public static void Send(MicroProfilingEventType type, uint operationId, int arg0, int arg1)
        {
            uint hLock = _clientLock.EnterReadLock();
            try
            {
                if (_client == null)
                {
                    return;
                }

                long timestamp = HighPrecisionTimer.GetCurrentTicks();

                // Length of packet = type           + timestamp    + thread ID   + operation ID + arg0    + arg1
                ushort len = 2 + sizeof(ushort) + sizeof(long) + sizeof(int) + sizeof(uint) + sizeof(uint) + sizeof(uint);

                using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent())
                {
                    byte[] writeBuffer = pooledBuf.Buffer;
                    BinaryHelpers.UInt16ToByteArrayLittleEndian(len, writeBuffer, 0);          // Write packet length
                    BinaryHelpers.UInt16ToByteArrayLittleEndian((ushort)type, writeBuffer, 2); // Write packet message type
                    BinaryHelpers.Int64ToByteArrayLittleEndian(timestamp, writeBuffer, 4);     // Write packet timestamp
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(0, writeBuffer, 12);           // Write thread ID (this can be augmented down the line)
                    BinaryHelpers.UInt32ToByteArrayLittleEndian(operationId, writeBuffer, 16); // Write operation ID
                    BinaryHelpers.Int32ToByteArrayLittleEndian(arg0, writeBuffer, 20);         // Write arg0
                    BinaryHelpers.Int32ToByteArrayLittleEndian(arg1, writeBuffer, 24);         // Write arg1
                    _client.SendProfilingData(writeBuffer, 0, len);
                }
            }
            finally
            {
                _clientLock.ExitReadLock(hLock);
            }
        }

        /// <summary>
        /// Blocking flush operation; should only ever be needed at program shutdown
        /// </summary>
        public static void Flush()
        {
            uint hLock = _clientLock.EnterReadLock();
            try
            {
                if (_client != null)
                {
                    _client.Flush();
                }
            }
            finally
            {
                _clientLock.ExitReadLock(hLock);
            }
        }
    }
}
