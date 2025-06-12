using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.Net.Http2.HPack;
using Durandal.Common.Net.Http2.Session;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    public partial class Http2Session
    {
        private async Task RunReadThread(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                byte[] scratch = new byte[Http2Constants.HTTP2_CONNECTION_PREFACE.Length];
                using (NonRealTimeCancellationTokenSource nrtSettingsTimeout = new NonRealTimeCancellationTokenSource(realTime, _sessionPreferences.SettingsTimeout))
                using (CancellationTokenSource settingsTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, nrtSettingsTimeout.Token))
                {
                    try
                    {
                        await HandleConnectionInitiation(scratch, _socket, settingsTimeoutSource.Token, realTime).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        realTime.Merge(); // this is to prevent deadlocks in unit tests mostly
                        realTime = null;

                        // Try and determine the source of the cancel
                        if (nrtSettingsTimeout.Token.IsCancellationRequested)
                        {
                            // Timed out while waiting for incoming settings
                            await Shutdown(Http2ErrorCode.SettingsTimeout).ConfigureAwait(false);
                        }
                        else
                        {
                            // The caller triggered cancellation, meaning the local peer initiated session
                            // shutdown (prematurely?)
                            await Shutdown(Http2ErrorCode.InternalError).ConfigureAwait(false);
                        }
                        return;
                    }
                    catch (Http2ProtocolException e)
                    {
                        _logger.Log("Failed to parse incoming HTTP2 frame", LogLevel.Err);
                        _logger.Log(e, LogLevel.Err);
                        realTime.Merge();
                        realTime = null;
                        await Shutdown(Http2ErrorCode.ProtocolError, e.Message).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                        realTime.Merge();
                        realTime = null;
                        await Shutdown(Http2ErrorCode.ProtocolError, e.Message).ConfigureAwait(false);
                        return;
                    }
                }

                HPackDecoder headerDecoder = new HPackDecoder(
                    new HPackDecoder.Options()
                    {
                        DynamicTableSizeLimit = _localSettingsDesired.HeaderTableSize
                    });

                while (!cancelToken.IsCancellationRequested)
                {
                    Http2Frame parsedFrame;
                    try
                    {
                        parsedFrame = await Http2Frame.ReadFromSocket(
                            _socket,
                            _localSettings.MaxFrameSize,
                            scratch,
                            !_isClient,
                            cancelToken,
                            realTime).ConfigureAwait(false);

                        if (parsedFrame == null)
                        {
                            _logger.Log("Got null or unrecognized HTTP2 frame", LogLevel.Wrn);
                            await Shutdown(Http2ErrorCode.ProtocolError, "Unable to parse frame").ConfigureAwait(false);
                            continue;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Read thread is shutting down
                        continue;
                    }
                    catch (Http2ProtocolException e)
                    {
                        _logger.Log("Failed to parse incoming HTTP2 frame", LogLevel.Err);
                        _logger.Log(e, LogLevel.Err);
                        await Shutdown(Http2ErrorCode.ProtocolError, e.Message).ConfigureAwait(false);
                        continue;
                    }

                    // This scope will handle disposal of the pooled frame data buffer automatically
                    using (Http2Frame incomingFrame = parsedFrame)
                    {
#if HTTP2_DEBUG
                        _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Got incoming HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                            incomingFrame.FrameType, incomingFrame.StreamId, incomingFrame.Flags, incomingFrame.PayloadLength);
#endif

                        if (incomingFrame.FrameType == Http2FrameType.Headers)
                        {
                            await HandleIncomingHeadersFrame(
                                incomingFrame as Http2HeadersFrame,
                                _socket,
                                _localSettings,
                                headerDecoder,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.Data)
                        {
                            await HandleIncomingDataFrame(
                                incomingFrame as Http2DataFrame,
                                _localSettings,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.Settings)
                        {
                            HandleIncomingSettingsFrame(incomingFrame as Http2SettingsFrame, firstFrame: false);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.RstStream)
                        {
                            HandleIncomingRstStreamFrame(incomingFrame as Http2RstStreamFrame);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.GoAway)
                        {
                            HandleIncomingGoAwayFrame(incomingFrame as Http2GoAwayFrame);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.Ping)
                        {
                            HandleIncomingPingFrame(incomingFrame as Http2PingFrame);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.Priority)
                        {
                            HandleIncomingPriorityFrame(incomingFrame as Http2PriorityFrame);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.PushPromise)
                        {
                            await HandleIncomingPushPromiseFrame(incomingFrame as Http2PushPromiseFrame, headerDecoder, _localSettings, realTime).ConfigureAwait(false);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.WindowUpdate)
                        {
                            await HandleIncomingWindowUpdateFrame(incomingFrame as Http2WindowUpdateFrame).ConfigureAwait(false);
                        }
                        else if (incomingFrame.FrameType == Http2FrameType.Continuation)
                        {
                            _logger.Log("Got a continuation frame without headers, this is an error", LogLevel.Err);
                            await Shutdown(Http2ErrorCode.ProtocolError, "Recieved CONTINUATION frame without prior headers").ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.Log("Got an unknown frame type, this is an error", LogLevel.Err);
                            await Shutdown(Http2ErrorCode.ProtocolError, "Unknown frame type " + ((int)incomingFrame.FrameType).ToString("X2")).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                // can't just catch (SocketException) because that type is not defined at this level of code...
                if (SocketHelpers.DoesExceptionIndicateSocketClosed(e))
                {
                    // Simplify the exception message
                    _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.EndUserIdentifiableInformation, "HTTP2 session aborted by remote host {0}", _socket.RemoteEndpointString);
                }
                else
                {
                    _logger.Log(e, LogLevel.Err);
                }
            }
            finally
            {
                _logger.Log("Shut down HTTP/2 read thread", LogLevel.Vrb);
                realTime?.Merge();
            }
        }

        private async Task HandleConnectionInitiation(byte[] scratch, ISocket socket, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // If we are a server, wait for the client to send the PRI * connection prefix
            if (!_isClient)
            {
                int initialBytesRead = await socket.ReadAsync(scratch, 0, Http2Constants.HTTP2_CONNECTION_PREFACE.Length, cancelToken, realTime).ConfigureAwait(false);
                if (initialBytesRead != Http2Constants.HTTP2_CONNECTION_PREFACE.Length ||
                    !ArrayExtensions.ArrayEquals(scratch, 0, Http2Constants.HTTP2_CONNECTION_PREFACE, 0, initialBytesRead))
                {
                    await Shutdown(Http2ErrorCode.ProtocolError, "Expected PRI * HTTP/2.0 connection preface but did not find it").ConfigureAwait(false);
                }
            }

            // Whether we are a client or server, wait for a preliminary settings frame
            Http2Frame initialFrame = await Http2Frame.ReadFromSocket(
                socket,
                Http2Constants.DEFAULT_MAX_FRAME_SIZE,
                scratch,
                !_isClient,
                cancelToken,
                realTime).ConfigureAwait(false);

            if (initialFrame == null)
            {
                _logger.Log("Got null or unrecognized HTTP2 frame", LogLevel.Wrn);
                await Shutdown(Http2ErrorCode.ProtocolError, "Unable to parse frame").ConfigureAwait(false); // fixme this will cause deadlock in unit tests because the read thread time is not merged....
            }
            else if (initialFrame.FrameType == Http2FrameType.Settings)
            {
                HandleIncomingSettingsFrame(initialFrame as Http2SettingsFrame, firstFrame: true);
            }
            else
            {
                await Shutdown(Http2ErrorCode.ProtocolError, "Initial frame was not a SETTINGS frame, instead got \"" + initialFrame.FrameType + "\"").ConfigureAwait(false);
            }
        }

        private async Task HandleIncomingHeadersFrame(
            Http2HeadersFrame frame,
            ISocket socket,
            Http2Settings localSettings,
            HPackDecoder headerDecoder,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            List<HeaderField> decodedHeaders = await DecodeIncomingHeadersBlock(
                frame,
                socket,
                localSettings,
                headerDecoder,
                cancelToken,
                realTime).ConfigureAwait(false);

            // TODO Make sure other side sent required pseudoheaders
            // Also make sure it did not send a "Host" header
            // TODO reject any headers that are not lowercase
            HttpHeaders convertedHeaders = new HttpHeaders(decodedHeaders.Count);
            int? responseStatusCode = null;
            foreach (var headerField in decodedHeaders)
            {
                if (string.Equals(headerField.Name, Http2Constants.PSEUDOHEADER_STATUS_CODE, StringComparison.Ordinal))
                {
                    responseStatusCode = int.Parse(headerField.Value);
                }
                else
                {
                    convertedHeaders.Add(headerField.Name, headerField.Value);
                }
            }

            if (frame.StreamId > _lastProcessedRemoteStreamId)
            {
                // TODO This is not quite accurate but it's close. Needs some improvement
                _lastProcessedRemoteStreamId = frame.StreamId;
            }

            if (_isClient)
            {
                // Is this trailers or is it headers?
                Http2Stream responseStream;
                bool streamExists = _activeStreams.TryGetValue(frame.StreamId, out responseStream);

                if (streamExists && responseStream.Headers != null)
                {
                    // It's trailers. We can finally close the response stream.
                    responseStream.Trailers = convertedHeaders;
                    responseStream.WriteStream.Dispose();
                }
                // Case: we are a client and this is the beginning of a response to a request we already made
                // Assert that the remote host sent the right headers
                else if (!responseStatusCode.HasValue)
                {
                    // A response must contain a status code. Stream error
                    _logger.Log("Remote endpoint sent HTTP response headers which did not contain a status code", LogLevel.Wrn);
                    _outgoingCommands.Enqueue(
                        new ResetStreamCommand(frame.StreamId, Http2ErrorCode.RefusedStream),
                        HttpPriority.StreamControl); // FIXME is this correct?
                }
                else if (streamExists)
                {
                    // It's headers.
                    // Set the response fields on the already active stream, and set the signal to
                    // tell the initator of the original request that they can start reading the response.
                    responseStream.Headers = convertedHeaders;
                    responseStream.ResponseStatusCode = responseStatusCode;
                    responseStream.ReceievedHeadersSignal.Set();
                }
                else
                {
                    // We got a response to a request that we apparently did not make. Reset stream
                    // This could also happen if the request timed out on our side and we deleted the stream context.
                    if (!_recentlyCanceledStreams.Contains(frame.StreamId))
                    {
                        _logger.Log("Remote endpoint sent HTTP response headers for a nonexistent request stream (could happen if our client timed out)", LogLevel.Wrn);
                        _outgoingCommands.Enqueue(
                            new ResetStreamCommand(frame.StreamId, Http2ErrorCode.RefusedStream),
                            HttpPriority.StreamControl);
                    }
                }
            }
            else
            {
                // Case: We are a server and the client is making a request to us.

                Http2Stream incomingStream;
                if (_activeStreams.TryGetValueOrSet(frame.StreamId, out incomingStream, () => NewStream(frame.StreamId, StreamState.Open)))
                {
                    // If the stream already exists, then assume this is a trailers frame.
                    _logger.Log("Got some headers on an existing stream, assuming they are trailers...", LogLevel.Vrb);
                    if (incomingStream.Headers == null)
                    {
                        _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Invalid headers on an existing stream {0}", frame.StreamId);
                        _outgoingCommands.Enqueue(
                            new ResetStreamCommand(frame.StreamId, Http2ErrorCode.ProtocolError),
                            HttpPriority.StreamControl);
                        return;
                    }

                    incomingStream.Trailers = convertedHeaders;
                    
                    if (frame.EndStream)
                    {
                        incomingStream.WriteStream.Dispose();
                    }
                }
                else
                {
                    // Create a new stream context
                    // and set a signal that we have a new incoming connection
                    incomingStream.Headers = convertedHeaders;
                    incomingStream.RequestPath = convertedHeaders[Http2Constants.PSEUDOHEADER_PATH];
                    incomingStream.RequestMethod = convertedHeaders[Http2Constants.PSEUDOHEADER_METHOD];
                    incomingStream.RequestScheme = convertedHeaders[Http2Constants.PSEUDOHEADER_SCHEME];
                    incomingStream.RequestAuthority = convertedHeaders[Http2Constants.PSEUDOHEADER_AUTHORITY];

                    // OPT these should have just been extracted during the conversion;
                    convertedHeaders.Remove(Http2Constants.PSEUDOHEADER_PATH);
                    convertedHeaders.Remove(Http2Constants.PSEUDOHEADER_METHOD);
                    convertedHeaders.Remove(Http2Constants.PSEUDOHEADER_SCHEME);
                    convertedHeaders.Remove(Http2Constants.PSEUDOHEADER_AUTHORITY);

                    incomingStream.ReceievedHeadersSignal.Set();
                    await _incomingRequestStreamIds.SendAsync(incomingStream.StreamId).ConfigureAwait(false);

                    // If there's no data to expect, close the content stream now
                    if (frame.EndStream)
                    {
                        incomingStream.WriteStream.Dispose();
                    }

                    //_outgoingCommands.Enqueue(
                    //    new ResetStreamCommand(frame.StreamId, Http2ErrorCode.RefusedStream),
                    //    priority: PRIORITY_STREAM_CONTROL);
                }
            }
        }

        private async Task HandleIncomingDataFrame(
            Http2DataFrame frame,
            Http2Settings localSettings,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            // Is this associated with any stream?
            Http2Stream existingStream;
            if (!_activeStreams.TryGetValue(frame.StreamId, out existingStream))
            {
                // There's a bit of a jank race condition here, but we try a best effort to not send redundant RST_STREAM commands if we receive data
                // on an incoming stream that we have requested a cancel on
                if (!_recentlyCanceledStreams.Contains(frame.StreamId))
                {
                    // Send RST_STREAM with refusal code
                    _outgoingCommands.Enqueue(new ResetStreamCommand(frame.StreamId, Http2ErrorCode.RefusedStream), HttpPriority.StreamControl);
                    _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Remote endpoint sent data for a nonreserved stream {0}", frame.StreamId);
                }
            }
            else
            {
                ArraySegment<byte> data = frame.PayloadData;
                await existingStream.WriteStream.WriteAsync(data.Array, data.Offset, data.Count, cancelToken, realTime).ConfigureAwait(false);
                existingStream.IncomingFlowControlWindow.AugmentCredits(0 - data.Count);
                _overallConnectionIncomingFlowWindow.AugmentCredits(0 - data.Count);

                if (frame.EndStream)
                {
                    existingStream.WriteStream.Dispose();
                }
                else if (existingStream.IncomingFlowControlWindow.AvailableCredits < (localSettings.InitialWindowSize / 2))
                {
                    int amountToIncrease = localSettings.InitialWindowSize - existingStream.IncomingFlowControlWindow.AvailableCredits;
                    existingStream.IncomingFlowControlWindow.AugmentCredits(amountToIncrease);
                    // Send a window update for this stream if we are at less than half of the quota of what we're willing to receive
                    // But don't bother updating the window if there's not going to be any more incoming frames
                    _outgoingCommands.Enqueue(
                        new SendFrameCommand(
                            Http2WindowUpdateFrame.CreateOutgoing(
                            frame.StreamId,
                            amountToIncrease)),
                        HttpPriority.StreamControl);
                }

                // Also send a window update for the entire connection
                if (_overallConnectionIncomingFlowWindow.AvailableCredits < (_sessionPreferences.DesiredGlobalConnectionFlowWindow / 2))
                {
                    int amountToIncrease = _sessionPreferences.DesiredGlobalConnectionFlowWindow - _overallConnectionIncomingFlowWindow.AvailableCredits;
                    _overallConnectionIncomingFlowWindow.AugmentCredits(amountToIncrease);
                    _outgoingCommands.Enqueue(
                        new SendFrameCommand(
                            Http2WindowUpdateFrame.CreateOutgoing(
                            0,
                            amountToIncrease)),
                        HttpPriority.StreamControl);
                }
            }
        }

        private void HandleIncomingSettingsFrame(Http2SettingsFrame frame, bool firstFrame)
        {
            //if (!firstFrame && _remoteSettings.HeaderTableSize > headerDecoder.DynamicTableSizeLimit)
            //{
            //    await Shutdown(Http2ErrorCode.CompressionError, "Cannot dynamically increase header decoder table size after session has begun").ConfigureAwait(false);
            //}

            if (frame.StreamId != 0)
            {
                // If an endpoint receives a SETTINGS frame whose stream identifier field is anything other than 0x0,
                // the endpoint MUST respond with a connection error (Section 5.4.1) of type PROTOCOL_ERROR.
                _logger.Log("Remote endpoint has sent settings on wrong stream ID " + frame.StreamId, LogLevel.Err);
                _outgoingCommands.Enqueue(new CloseConnectionCommand(Http2ErrorCode.ProtocolError, "Settings frame on non-zero stream"), HttpPriority.SessionControl);
                return;
            }

            if (!frame.Ack)
            {
                if (frame.Settings == null)
                {
                    // A badly formed or incomplete SETTINGS frame MUST be treated as a connection error (Section 5.4.1) of type PROTOCOL_ERROR.
                    _logger.Log("Remote endpoint has sent null settings", LogLevel.Err);
                    _outgoingCommands.Enqueue(new CloseConnectionCommand(Http2ErrorCode.ProtocolError, "Could not parse settings frame"), HttpPriority.SessionControl);
                    return;
                }

                if ((frame.PayloadLength % 6) != 0)
                {
                    // A SETTINGS frame with a length other than a multiple of 6 octets MUST be treated as a connection error (Section 5.4.1) of type FRAME_SIZE_ERROR.
                    _logger.Log("Remote endpoint has sent settings with invalid size", LogLevel.Err);
                    _outgoingCommands.Enqueue(new CloseConnectionCommand(Http2ErrorCode.FrameSizeError, "Settings frame with invalid size"), HttpPriority.SessionControl);
                    return;
                }

#if HTTP2_DEBUG
                _logger.Log("Remote endpoint has sent settings", LogLevel.Vrb);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote EnablePush: {0}", frame.Settings.EnablePush);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote HeaderTableSize: {0}", frame.Settings.HeaderTableSize);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote InitialWindowSize: {0}", frame.Settings.InitialWindowSize);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote MaxConcurrentStreams: {0}", frame.Settings.MaxConcurrentStreams);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote MaxFrameSize: {0}", frame.Settings.MaxFrameSize);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote MaxHeaderListSize: {0}", frame.Settings.MaxHeaderListSize);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tRemote EnableConnectProtocol: {0}", frame.Settings.EnableConnectProtocol);
#endif

                if (!frame.Settings.Valid)
                {
                    // The SETTINGS frame affects connection state. A badly formed or incomplete SETTINGS frame
                    // MUST be treated as a connection error (Section 5.4.1) of type PROTOCOL_ERROR.
                    _logger.Log("Remote endpoint has sent invalid settings", LogLevel.Err);
                    _outgoingCommands.Enqueue(new CloseConnectionCommand(Http2ErrorCode.ProtocolError, "Invalid settings"), HttpPriority.SessionControl);
                }
                else if (_isClient && frame.Settings.EnablePush)
                {
                    // Clients MUST reject any attempt to change the SETTINGS_ENABLE_PUSH setting to a
                    // value other than 0 by treating the message as a connection error (Section 5.4.1) of type PROTOCOL_ERROR.
                    _logger.Log("Remote endpoint has sent invalid settings", LogLevel.Err);
                    _outgoingCommands.Enqueue(new CloseConnectionCommand(Http2ErrorCode.ProtocolError, "Server may not set ENABLE_PUSH 1"), HttpPriority.SessionControl);
                }
                else
                {
                    // Apply flow control updates right now
                    int changeToOutgoingFlowControlWindow = frame.Settings.InitialWindowSize - _remoteSettings.InitialWindowSize;
                    if (changeToOutgoingFlowControlWindow != 0)
                    {
                        // settings updates do not apply to the entire connection flow control window (rfc 7540 6.9.2)
                        //_overallConnectionIncomingFlowWindow.AugmentCredits(changeToIncomingFlowControlWindow);
                        // Update the control window on all incoming streams.
                        var streamEnumerator = _activeStreams.GetValueEnumerator();
                        while (streamEnumerator.MoveNext())
                        {
                            streamEnumerator.Current.Value.OutgoingFlowControlWindow.AugmentCredits(changeToOutgoingFlowControlWindow);
                            streamEnumerator.Current.Value.OutgoingFlowControlAvailableSignal.Set();
                        }
                    }

                    // Apply the settings sent from the server
                    _remoteSettings = frame.Settings;

                    // Send an ACK back. Make sure we don't ACK an ACK itself
                    _outgoingCommands.Enqueue(new AcknowledgeSettingsCommand(frame.Settings), HttpPriority.SessionControl);

                    // Signal (if we are a client) that the server has sent its settings frame.
                    _remoteSentSettingsSignal?.Set();
                }
            }
            else
            {
                _logger.Log("Remote endpoint has ACKed settings", LogLevel.Vrb);
                if (frame.PayloadLength > 0)
                {
                    // Receipt of a SETTINGS frame with the ACK flag set and a length field value other
                    // than 0 MUST be treated as a connection error (Section 5.4.1) of type FRAME_SIZE_ERROR.
                    _logger.Log("Remote endpoint has sent settings along with an ACK", LogLevel.Err);
                    _outgoingCommands.Enqueue(new CloseConnectionCommand(Http2ErrorCode.FrameSizeError, "Settings found in ACK frame"), HttpPriority.SessionControl);
                }
            }
        }

        private void HandleIncomingPingFrame(Http2PingFrame frame)
        {
            if (frame.Ack)
            {
                // Remote endpoint is responding to a ping we sent. This would ostensibly be used to measure latency but that's not implemented yet.
            }
            else
            {
                // Remote endpoint is initiating ping. Respond appropriately.
                _logger.Log("Got PING", LogLevel.Vrb);
                // The incoming data buffer is going to be deallocated unfortunately so we
                // need to do this tiny buffer rental and copy rather than just reusing the buffer...
                PooledBuffer<byte> copiedPingData = BufferPool<byte>.Rent(8);
                ArrayExtensions.MemCopy(frame.PingData.Buffer, 0, copiedPingData.Buffer, 0, 8);
                _outgoingCommands.Enqueue(new SendFrameCommand(Http2PingFrame.CreateOutgoing(copiedPingData, ack: true)), HttpPriority.Ping);
            }
        }

        private void HandleIncomingRstStreamFrame(Http2RstStreamFrame frame)
        {
#if HTTP2_DEBUG
            _logger.LogFormat(frame.Error == Http2ErrorCode.NoError ? LogLevel.Vrb : LogLevel.Wrn,
                DataPrivacyClassification.SystemMetadata, "Got RST_STREAM on stream {0} with code {1}", frame.StreamId, frame.Error);
#endif

            Http2Stream stream;
            if (_activeStreams.TryGetValueAndRemove(frame.StreamId, out stream))
            {
                stream.State = StreamState.Closed;

                // wake up any threads if they are waiting for outgoing flow control to send more data
                stream.OutgoingFlowControlAvailableSignal.Set();
                _overallConnectionOutgoingFlowWindowAvailable.Set();

                // Also, if this is a request for which the caller is still waiting for response headers, signal them
                // so they know the request has been canceled.
                stream.ReceievedHeadersSignal.Set();
            }
        }

        private void HandleIncomingGoAwayFrame(Http2GoAwayFrame frame)
        {
            if (frame.DebugData.Count > 0)
            {
                _logger.LogFormat(frame.Error == Http2ErrorCode.NoError ? LogLevel.Vrb : LogLevel.Wrn,
                    DataPrivacyClassification.SystemMetadata, "Got GOAWAY with code {0} and message {1}", frame.Error, frame.DebugString);
            }
            else
            {
                _logger.LogFormat(frame.Error == Http2ErrorCode.NoError ? LogLevel.Vrb : LogLevel.Wrn,
                    DataPrivacyClassification.SystemMetadata, "Got GOAWAY with code {0}", frame.Error);
            }

            if (frame.Error != Http2ErrorCode.NoError && !string.IsNullOrEmpty(frame.DebugString))
            {
                _logger.Log(frame.DebugString, LogLevel.Wrn, privacyClass: DataPrivacyClassification.SystemMetadata);
            }

            RudeShutdown();
        }

        private void HandleIncomingPriorityFrame(Http2PriorityFrame frame)
        {
            //_logger.Log("Got a priority frame, still need to implement this", LogLevel.Vrb);
        }

        private async Task HandleIncomingPushPromiseFrame(Http2PushPromiseFrame frame, HPackDecoder headerDecoder, Http2Settings localSettings, IRealTimeProvider realTime)
        {
            // Refuse the frame if we are not a client
            if (!_isClient)
            {
                await Shutdown(Http2ErrorCode.ProtocolError, "Server received a PUSH_PROMISE frame").ConfigureAwait(false);
            }

            // or if we don't support pushes
            if (!_localSettings.EnablePush)
            {
                await Shutdown(Http2ErrorCode.ProtocolError, "Client does not support PUSH_PROMISE frames").ConfigureAwait(false);
            }

            // Assert that the promised stream ID is valid
            if (frame.PromisedStreamId == 0 ||
                (frame.PromisedStreamId % 2) != 0)
            {
                await Shutdown(Http2ErrorCode.ProtocolError, "Promised stream ID is invalid").ConfigureAwait(false);
            }

            // Decode headers
            // FIXME this doesn't handle continuation headers
            // We should parse the remainder of the block in-line here so we can throw an error if anything besides CONTINUATION comes.
            List<HeaderField> decodedHeaders = new List<HeaderField>();
            var decodeFragmentResult = headerDecoder.DecodeHeaderBlockFragment(frame.HeaderData, (uint)localSettings.MaxFrameSize, decodedHeaders);

            string requestPath = null;
            string requestMethod = null;
            string requestAuthority = null;
            string requestScheme = null;

            IDictionary<string, string> extraHeaders = null;
            foreach (var headerField in decodedHeaders)
            {
                if (string.Equals(headerField.Name, Http2Constants.PSEUDOHEADER_AUTHORITY, StringComparison.Ordinal))
                {
                    requestAuthority = headerField.Value;
                }
                else if (string.Equals(headerField.Name, Http2Constants.PSEUDOHEADER_SCHEME, StringComparison.Ordinal))
                {
                    requestScheme = headerField.Value;
                }
                else if (string.Equals(headerField.Name, Http2Constants.PSEUDOHEADER_METHOD, StringComparison.Ordinal))
                {
                    requestMethod = headerField.Value;
                }
                else if (string.Equals(headerField.Name, Http2Constants.PSEUDOHEADER_PATH, StringComparison.Ordinal))
                {
                    requestPath = headerField.Value;
                }
                else
                {
                    if (extraHeaders == null)
                    {
                        extraHeaders = new SmallDictionary<string, string>(Math.Max(1, decodedHeaders.Count - 4));
                    }

                    extraHeaders.Add(headerField.Name, headerField.Value);
                }
            }

            // Make sure other side sent required pseudoheaders
            if (string.IsNullOrEmpty(requestPath) ||
                string.IsNullOrEmpty(requestMethod) ||
                string.IsNullOrEmpty(requestAuthority) ||
                string.IsNullOrEmpty(requestScheme))
            {
                // For an invalid push promise header such as invalid authority, the spec says to "treat this as a stream
                // error of type PROTOCOL_ERROR", however it is not clear if the affected stream is the one containing the
                // push promise itself, or the promised stream ID. Opting for the latter.
                _outgoingCommands.Enqueue(new ResetStreamCommand(frame.PromisedStreamId, Http2ErrorCode.ProtocolError), HttpPriority.StreamControl);
                return;
            }

            // TODO compare authority and scheme with local values

            // Parse the URL
            string requestBasePath;
            string requestFragment;
            HttpFormParameters getParameters;
            if (!HttpHelpers.TryParseRelativeUrl(
                requestPath,
                out requestBasePath,
                out getParameters,
                out requestFragment) ||
                !string.IsNullOrEmpty(requestFragment))
            {
                _logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "Invalid push-promise URL {0}", requestPath);
                await Shutdown(Http2ErrorCode.ProtocolError, "Cannot parse push-promised request URL").ConfigureAwait(false);
                return;
            }

            PushPromiseHeaders pushPromiseHeaders = new PushPromiseHeaders(
                requestBasePath,
                requestMethod,
                frame.PromisedStreamId,
                realTime.Time,
                getParameters,
                extraHeaders);

            // Register the stream for the response
            Http2Stream newStream = NewStream(frame.PromisedStreamId, StreamState.ReservedRemote);
            Http2Stream existingStream;
            if (_activeStreams.TryGetValueOrSet(frame.PromisedStreamId, out existingStream, newStream))
            {
                // Promised stream ID already exists. Error
                await Shutdown(Http2ErrorCode.ProtocolError, "Promised stream ID " + newStream.StreamId + " is already in use").ConfigureAwait(false);
            }

            // Register the push promise frame
            FastConcurrentHashSet<PushPromiseHeaders> ppHeaders;
            _pushPromises.TryGetValueOrSet(requestBasePath, out ppHeaders, () => new FastConcurrentHashSet<PushPromiseHeaders>());
            lock (ppHeaders)
            {
                ppHeaders.Add(pushPromiseHeaders);
            }

            // TODO also prune old promise headers here, that should probably be a separate function
        }

        private async ValueTask HandleIncomingWindowUpdateFrame(Http2WindowUpdateFrame frame)
        {
            Http2Stream stream;
            if (frame.StreamId == 0)
            {
                if (frame.WindowSizeIncrement <= 0)
                {
                    await Shutdown(Http2ErrorCode.ProtocolError, "Non-positive window update on stream 0").ConfigureAwait(false);
                    return;
                }

                _overallConnectionOutgoingFlowWindow.AugmentCredits(frame.WindowSizeIncrement);
                if (_overallConnectionOutgoingFlowWindow.CreditsOverflow)
                {
                    _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Global flow control credits exceed maximum value");
                    await Shutdown(Http2ErrorCode.FlowControlError, "Global flow control credits exceed maximum value").ConfigureAwait(false);
                }

                _overallConnectionOutgoingFlowWindowAvailable.Set();
            }
            else if (_activeStreams.TryGetValue(frame.StreamId, out stream))
            {
                if (frame.WindowSizeIncrement <= 0)
                {
                    _outgoingCommands.Enqueue(new ResetStreamCommand(frame.StreamId, Http2ErrorCode.ProtocolError), HttpPriority.StreamControl);
                    _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Non-positive window update on stream {0}", frame.StreamId);
                    return;
                }

                stream.OutgoingFlowControlWindow.AugmentCredits(frame.WindowSizeIncrement);
                if (stream.OutgoingFlowControlWindow.CreditsOverflow)
                {
                    _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Flow control credits exceed maximum value for stream {0}", frame.StreamId);
                    _outgoingCommands.Enqueue(new ResetStreamCommand(frame.StreamId, Http2ErrorCode.FlowControlError), HttpPriority.StreamControl);
                }

                stream.OutgoingFlowControlAvailableSignal.Set();
            }
            else if (!_recentlyCanceledStreams.Contains(frame.StreamId))
            {
                _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Got a window update frame for nonexistent stream {0}", frame.StreamId);
            }
        }

        /// <summary>
        /// Decodes an initial HEADERS frame, and if necessary further CONTINUATION frames read from the socket,
        /// and returns the list of decoded headers. Or shuts down the session if there is an error.
        /// </summary>
        /// <param name="initialHeadersFrame"></param>
        /// <param name="socket"></param>
        /// <param name="localSettings"></param>
        /// <param name="headerDecoder"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async Task<List<HeaderField>> DecodeIncomingHeadersBlock(
            Http2HeadersFrame initialHeadersFrame,
            ISocket socket,
            Http2Settings localSettings,
            HPackDecoder headerDecoder,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            List<HeaderField> decodedHeaders = new List<HeaderField>();

            // Allow table updates at the start of header header block.
            // This will be reset once the first header was decoded and will
            // persist also during the continuation frame
            uint allowedHeadersSize = (uint)localSettings.MaxFrameSize;
            headerDecoder.AllowTableSizeUpdates = true;
            var decodeFragmentResult = headerDecoder.DecodeHeaderBlockFragment(initialHeadersFrame.HeaderData, allowedHeadersSize, decodedHeaders);
            if (decodeFragmentResult.Status != DecoderExtensions.DecodeStatus.Success)
            {
                await Shutdown(Http2ErrorCode.CompressionError, "Error decoding incoming headers").ConfigureAwait(false);
                return decodedHeaders;
            }

            allowedHeadersSize -= decodeFragmentResult.HeaderFieldsSize;

            if (!initialHeadersFrame.EndHeaders)
            {
                // Expect CONTINUATION frames.
                // We should parse the remainder of the block in-line here so we can throw an error if anything besides CONTINUATION comes.
                byte[] scratch = new byte[9];
                bool endOfHeaders = false;
                while (!endOfHeaders)
                {
                    Http2Frame nextFrame = await Http2Frame.ReadFromSocket(socket, localSettings.MaxFrameSize, scratch, !_isClient, cancelToken, realTime).ConfigureAwait(false);
                    if (nextFrame == null || nextFrame.FrameType != Http2FrameType.Continuation)
                    {
                        await Shutdown(Http2ErrorCode.ProtocolError, "HEADERS not followed by appropriate CONTINUATION frames").ConfigureAwait(false);
                    }

                    Http2ContinuationFrame continuationFrame = nextFrame as Http2ContinuationFrame;
                    decodeFragmentResult = headerDecoder.DecodeHeaderBlockFragment(continuationFrame.HeaderData, allowedHeadersSize, decodedHeaders);
                    if (decodeFragmentResult.Status != DecoderExtensions.DecodeStatus.Success)
                    {
                        await Shutdown(Http2ErrorCode.CompressionError, "Error decoding incoming headers").ConfigureAwait(false);
                        return decodedHeaders;
                    }

                    allowedHeadersSize -= decodeFragmentResult.HeaderFieldsSize;
                    endOfHeaders = continuationFrame.EndHeaders;
                }
            }

            if (!headerDecoder.HasInitialState)
            {
                // If the decoder has not returned to initial state, it means the incoming header block was incomplete.
                await Shutdown(Http2ErrorCode.CompressionError, "Received incomplete header block").ConfigureAwait(false);
            }

            return decodedHeaders;
        }
    }
}
