using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation.Profiling
{
    /// <summary>
    /// Class which reads microprofiling stream data and decodes the packed binary data into readable logs.
    /// </summary>
    public class MicroProfileReader
    {
        private static readonly object[] EMPTY_OBJECT_ARRAY = new object[0];
        private readonly Stream _fileStream;
        private readonly IDictionary<uint, long> _lastTimestampPerOperation;

        public MicroProfileReader(Stream inputStream)
        {
            _fileStream = inputStream.AssertNonNull(nameof(inputStream));
            _lastTimestampPerOperation = new Dictionary<uint, long>();
        }

        /// <summary>
        /// Reads a single event from the profiling data stream and returns it as a decoded string, or returns "null" if end of data reached.
        /// </summary>
        /// <returns></returns>
        public async Task<string> ReadNextEvent()
        {
            using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent())
            {
                byte[] readBuffer = pooledBuf.Buffer;
                int bytesRead = await _fileStream.ReadAsync(readBuffer, 0, 2).ConfigureAwait(false);
                if (bytesRead <= 0)
                {
                    return null;
                }

                ushort packetLength = BinaryHelpers.ByteArrayToUInt16LittleEndian(readBuffer, 0);
                if (packetLength <= 2)
                {
                    throw new FormatException("Invalid packet length; corrupted microprofile stream?");
                }

                bytesRead = await _fileStream.ReadAsync(readBuffer, 0, packetLength - 2).ConfigureAwait(false);
                if (bytesRead < packetLength - 2)
                {
                    return null;
                }

                MicroProfilingEventType eventType = (MicroProfilingEventType)BinaryHelpers.ByteArrayToUInt16LittleEndian(readBuffer, 0);
                long timestamp = BinaryHelpers.ByteArrayToInt64LittleEndian(readBuffer, 2);
                int threadId = BinaryHelpers.ByteArrayToInt32LittleEndian(readBuffer, 10);
                uint operationId = BinaryHelpers.ByteArrayToUInt32LittleEndian(readBuffer, 14);
                DateTimeOffset time = new DateTimeOffset(timestamp, TimeSpan.Zero);
                int numArgs = (packetLength - 18) / 4;
                object[] args;
                if (numArgs > 0)
                {
                    args = new object[numArgs];
                    for (int arg = 0; arg < numArgs; arg++)
                    {
                        args[arg] = BinaryHelpers.ByteArrayToInt32LittleEndian(readBuffer, 18 + (arg * 4));
                    }
                }
                else
                {
                    args = EMPTY_OBJECT_ARRAY;
                }

                string formatString;
                if (!FormatStringDict.TryGetValue(eventType, out formatString))
                {
                    formatString = FormatStringDict[MicroProfilingEventType.Unknown];
                }

                int msSinceLastOperationWithThisId = 0;
                long timeOfLastOperationWithThisId;
                if (_lastTimestampPerOperation.TryGetValue(operationId, out timeOfLastOperationWithThisId))
                {
                    msSinceLastOperationWithThisId = (int)((timestamp - timeOfLastOperationWithThisId) / 10000);
                }

                _lastTimestampPerOperation[operationId] = timestamp;

                string formattedMessage = string.Format(formatString, args);
                return string.Format("{0}\t{1:D5}\t{2:X8}\t{3:X8}\t{4:X8}\t{5}",
                    time.ToString("HH:mm:ss.fffff"),
                    msSinceLastOperationWithThisId,
                    threadId,
                    operationId,
                    (int)eventType,
                    formattedMessage);
            }
        }

        private static readonly Dictionary<MicroProfilingEventType, string> FormatStringDict = new Dictionary<MicroProfilingEventType, string>()
        {
            { MicroProfilingEventType.Unknown, "(Unknown message)" },
            { MicroProfilingEventType.KeepAlive, "Keepalive" },
            { MicroProfilingEventType.UnitTest, "Example message for unit testing" },

            { MicroProfilingEventType.MMIO_Read_EnterReadAnyMethod, "MMIO Entered ReadAny method, stream {0:X8}" },
            { MicroProfilingEventType.MMIO_Read_PreRead, "MMIO Preread AmountCanSafelyRead={0}" },
            { MicroProfilingEventType.MMIO_Read_Spinwait_Start, "MMIO Read spinwait start" },
            { MicroProfilingEventType.MMIO_Read_Spinwait_End, "MMIO Read spinwait end after {0}ms" },
            { MicroProfilingEventType.MMIO_Read_ReadStart, "MMIO Read Start AmountToActuallyRead={0}" },
            { MicroProfilingEventType.MMIO_Read_ReadDataFinish, "MMIO Read data finished" },
            { MicroProfilingEventType.MMIO_Read_ReadFinish, "MMIO Read finished" },
            { MicroProfilingEventType.MMIO_Read_Single_Spinwait_Start, "MMIO Read single spinwait start" },
            { MicroProfilingEventType.MMIO_Read_Single_Spinwait_End, "MMIO Read single spinwait end after {0}ms" },

            { MicroProfilingEventType.MMIO_Write_EnterWriteMethod, "MMIO Write, stream {0:X8}" },
            { MicroProfilingEventType.MMIO_Write_PreWrite, "MMIO Prewrite AmountCanSafelyWrite={0}" },
            { MicroProfilingEventType.MMIO_Write_StallBufferFull, "MMIO Write stall (buffer full)" },
            { MicroProfilingEventType.MMIO_Write_WriteStart, "MMIO Write Start AmountToActuallyWrite={0}" },
            { MicroProfilingEventType.MMIO_Write_WriteDataFinish, "MMIO Write data finished" },
            { MicroProfilingEventType.MMIO_Write_WriteFinish, "MMIO Write finished" },
            { MicroProfilingEventType.Bug_Repro_Trigger_Stutter, "Triggering stutter detected with time of {0}ms" },

            { MicroProfilingEventType.KeepAlive_Ping_SendRequestStart, "Keepalive Send Request Start" },
            { MicroProfilingEventType.KeepAlive_Ping_SendRequestFinish, "Keepalive Send Request Finish" },
            { MicroProfilingEventType.KeepAlive_Ping_RecvRequestStart, "Keepalive Recv Request Start" },
            { MicroProfilingEventType.KeepAlive_Ping_RecvRequestFinish, "Keepalive Recv Request Finish" },
            { MicroProfilingEventType.KeepAlive_Ping_SendResponseStart, "Keepalive Send Response Start" },
            { MicroProfilingEventType.KeepAlive_Ping_SendResponseFinish, "Keepalive Send Response Finish" },
            { MicroProfilingEventType.KeepAlive_Ping_RecvResponseStart, "Keepalive Recv Response Start" },
            { MicroProfilingEventType.KeepAlive_Ping_RecvResponseFinish, "Keepalive Recv Response Finish" },
        };
    }
}
