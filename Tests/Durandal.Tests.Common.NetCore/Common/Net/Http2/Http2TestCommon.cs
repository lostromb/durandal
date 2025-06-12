using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http2;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Net.Http2
{
    internal static class Http2TestCommon
    {
        public static async Task ReadClientConnectionPrefix(ISocket socket, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            byte[] headerScratch = new byte[Http2Constants.HTTP2_CONNECTION_PREFACE.Length];
            await socket.ReadAsync(headerScratch, 0, headerScratch.Length, cancelToken, realTime).ConfigureAwait(false);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(headerScratch, Http2Constants.HTTP2_CONNECTION_PREFACE));
        }

        public static async Task WriteClientConnectionPrefix(ISocket socket, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await socket.WriteAsync(
                Http2Constants.HTTP2_CONNECTION_PREFACE,
                0,
                Http2Constants.HTTP2_CONNECTION_PREFACE.Length,
                cancelToken,
                realTime).ConfigureAwait(false);
        }

        public static async Task AssertSocketIsEmpty(ISocket socket)
        {
            byte[] scratch = new byte[9];
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
            {
                try
                {
                    int amountRead = await socket.ReadAnyAsync(scratch, 0, 1, cts.Token, DefaultRealTimeProvider.Singleton);
                    socket.Unread(scratch, 0, 1);
                    try
                    {
                        Http2Frame frame = await Http2Frame.ReadFromSocket(socket, Http2Constants.DEFAULT_MAX_FRAME_SIZE, scratch, true, cts.Token, DefaultRealTimeProvider.Singleton);
                        Assert.Fail("Expected socket to be empty, instead got an HTTP2 frame of type " + frame.FrameType.ToString());
                    }
                    catch (Exception)
                    {
                        Assert.Fail("Expected socket to be empty, got something else instead");
                    }
                }
                catch (OperationCanceledException) { }
            }
        }

        public static async Task ExpectFrame<T>(
            ISocket socket,
            ILogger logger,
            Http2Settings settings,
            Queue<Http2Frame> frameQueue,
            bool isServer,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            Func<T, bool> validator) where T : Http2Frame
        {
            using (T frame = await ExpectFrameAndReturn<T>(socket, logger, settings, frameQueue, isServer, cancelToken, realTime, validator)) { }
        }

        public static async Task<T> ExpectFrameAndReturn<T>(
            ISocket socket,
            ILogger logger,
            Http2Settings settings,
            Queue<Http2Frame> frameQueue,
            bool isServer,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            Func<T, bool> validator) where T : Http2Frame
        {
            try
            {
                Queue<Http2Frame> backlog = new Queue<Http2Frame>();
                byte[] scratch = new byte[9];
                Http2Frame frame;

                while (!cancelToken.IsCancellationRequested)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    if (frameQueue.Count > 0)
                    {
                        frame = frameQueue.Dequeue();
                    }
                    else
                    {
                        frame = await Http2Frame.ReadFromSocket(socket, settings.MaxFrameSize, scratch, isServer, cancelToken, realTime).ConfigureAwait(false);
                    }

                    if (frame != null && frame is T)
                    {
                        logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata,
                            "UnitTest-Expect: incoming HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                            frame.FrameType, frame.StreamId, frame.Flags, frame.PayloadLength);

                        if (validator(frame as T))
                        {
                            // Rebuffer unused frames. Make sure we don't add the validated frame to this current list.
                            while (frameQueue.Count > 0)
                            {
                                backlog.Enqueue(frameQueue.Dequeue());
                            }

                            while (backlog.Count > 0)
                            {
                                frameQueue.Enqueue(backlog.Dequeue());
                            }

                            return frame as T;
                        }
                        else
                        {
                            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Incoming {0} did not pass validation", frame.GetType().Name);
                            backlog.Enqueue(frame);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new Exception("Timed out while waiting for an HTTP2 frame of type \"" + typeof(T).Name + "\"");
            }

            throw new Exception("Did not find an HTTP2 frame of type \"" + typeof(T).Name + "\"");
        }
    }
}
