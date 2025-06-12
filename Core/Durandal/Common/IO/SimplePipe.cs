//-----------------------------------------------------------------------
// <copyright file="SimplePipe.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Durandal.Common.IO
{
    /// <summary>
    /// A class that simply connects two streams together
    /// and allows them to write to each other in buffered chunks.
    /// This operation is also cancellable through use of a CancellationToken. It also supports progress reporting for long transfers.
    /// </summary>
    public class SimplePipe
    {
        private Stream input;
        private Stream output;
        private CancellationToken? token;

        /// <summary>
        /// Constructs a pipe between a given input and output stream.
        /// </summary>
        /// <param name="inputStream">The input stream</param>
        /// <param name="outputStream">The output stream</param>
        /// <param name="cancelToken">An optional cancellation token for the drain operation</param>
        public SimplePipe(Stream inputStream, Stream outputStream, CancellationToken cancelToken = default(CancellationToken))
        {
            this.input = inputStream;
            this.output = outputStream;
            this.token = null;

            if (cancelToken != default(CancellationToken) && cancelToken.CanBeCanceled)
            {
                this.token = cancelToken;
            }
        }

        /// <summary>
        /// Reads the entire input stream to the end and writes it to the output stream
        /// in buffered chunks. Be sure to close both ends of the pipe after calling this.
        /// </summary>
        public void Drain()
        {
            this.Drain(0, NullProgressReporter);
        }

        /// <summary>
        /// A stub method that accepts a number and discards it
        /// </summary>
        /// <param name="value"></param>
        private static void NullProgressReporter(double value) { }

        /// <summary>
        /// Reads the entire input stream to the end and writes it to the output stream
        /// in buffered chunks. Be sure to close both ends of the pipe after calling this.
        /// </summary>
        /// <param name="expectedNumberOfBytes">The number of bytes that are expected to be transferred (0 if unknown)</param>
        /// <param name="progressReporter">A delegate that accepts progress values from 0 to 100</param>
        public void Drain(double expectedNumberOfBytes, Action<double> progressReporter)
        {
            using (PooledBuffer<byte> pooledBuffer = BufferPool<byte>.Rent())
            {
                byte[] scratch = pooledBuffer.Buffer;
                int check = 0;
                int bytesRead = 0;
                long totalBytesRead = 0;
                try
                {
                    while (check >= 0)
                    {
                        bytesRead = this.input.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            this.output.Write(scratch, 0, bytesRead);
                            totalBytesRead += bytesRead;
                        }

                        check = this.input.ReadByte();
                        if (check >= 0)
                        {
                            this.output.WriteByte((byte)check);
                            totalBytesRead += 1;
                        }

                        // Abort the stream if the cancellation token raises a flag
                        if (this.token != null && this.token.Value.IsCancellationRequested)
                        {
                            check = -1;
                        }

                        // Report progress, if applicable
                        if (expectedNumberOfBytes > 0)
                        {
                            double percentComplete = (double)totalBytesRead / expectedNumberOfBytes * 100;
                            percentComplete = System.Math.Min(100, System.Math.Max(0, percentComplete));
                            progressReporter(percentComplete);
                        }
                    }
                }
                catch (ObjectDisposedException e)
                {
                    Debug.WriteLine("Caught ObjectDisposedException in SimplePipe " + e.Message);
                }
            }
        }
    }
}
