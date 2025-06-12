using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.Net.Http2.HPack;
using Durandal.Common.Net.Http2.Session;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    /// <summary>
    /// Implementations of outgoing commands on an HTTP/2 session.
    /// </summary>
    public partial class Http2Session
    {
        private async Task RunWriteThread(CancellationToken cancelToken, IRealTimeProvider realTime, bool isClient)
        {
            HPackEncoder headerEncoder = new HPackEncoder();

            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    ISessionCommand nextCommand = await _outgoingCommands.Dequeue(cancelToken, realTime).ConfigureAwait(false);

                    if (nextCommand != null)
                    {
                        if (nextCommand is SendClientConnectionPrefixCommand)
                        {
                            await HandleSendClientConnectionPrefixCommand(_socket, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is SendFrameCommand)
                        {
                            await HandleSendFrameCommand(nextCommand as SendFrameCommand, _socket, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is AcknowledgeSettingsCommand)
                        {
                            await HandleAcknowledgeSettingsCommand(nextCommand as AcknowledgeSettingsCommand, _socket, headerEncoder, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is SendDataFrameCommand)
                        {
                            await HandleSendDataFrameCommand(nextCommand as SendDataFrameCommand, _socket, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is SendRequestHeaderBlockCommand)
                        {
                            await HandleSendRequestHeaderBlockCommand(
                                nextCommand as SendRequestHeaderBlockCommand,
                                _socket,
                                _remoteSettings,
                                _localSettings,
                                headerEncoder,
                                _logger,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is SendResponseHeaderBlockCommand)
                        {
                            await HandleSendResponseHeaderBlockCommand(
                                nextCommand as SendResponseHeaderBlockCommand,
                                _socket,
                                _remoteSettings,
                                headerEncoder,
                                _logger,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is SendResponseTrailerBlockCommand)
                        {
                            await HandleSendResponseTrailerBlockCommand(
                                nextCommand as SendResponseTrailerBlockCommand,
                                _socket,
                                _remoteSettings,
                                headerEncoder,
                                _logger,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is SendPushPromiseFrameCommand)
                        {
                            await HandleSendPushPromiseFrameCommand(
                                nextCommand as SendPushPromiseFrameCommand,
                                _socket,
                                _remoteSettings,
                                headerEncoder,
                                _logger,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is UpdateLocalSettingsCommand)
                        {
                            await HandleUpdateSettingsCommand(nextCommand as UpdateLocalSettingsCommand, _socket, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is ResetStreamCommand)
                        {
                            await HandleResetStreamCommand(nextCommand as ResetStreamCommand, _socket, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is SendPingCommand)
                        {
                            await HandleSendPingCommand(nextCommand as SendPingCommand, _socket, _logger, cancelToken, realTime).ConfigureAwait(false);
                        }
                        else if (nextCommand is CloseConnectionCommand)
                        {
                            await HandleCloseConnectionCommand(
                                nextCommand as CloseConnectionCommand,
                                _lastProcessedRemoteStreamId,
                                _socket,
                                _logger,
                                cancelToken,
                                realTime).ConfigureAwait(false);
                            _gracefulShutdownSignal.Set();
                        }
                        else
                        {
                            _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Unknown HTTP/2 session command {0}", nextCommand.GetType().Name);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                // If the socket was terminated, don't bother logging it here. Let it get logged by the Read thread of this class.
                if (!SocketHelpers.DoesExceptionIndicateSocketClosed(e))
                {
                    _logger.Log(e, LogLevel.Err);
                }
            }
            finally
            {
                _logger.Log("Shut down HTTP/2 write thread", LogLevel.Vrb);
                realTime.Merge();
            }
        }

        private static async ValueTask HandleSendClientConnectionPrefixCommand(ISocket socket, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            logger.Log("Sending HTTP/2 connection prefix", LogLevel.Vrb);
            await socket.WriteAsync(Http2Constants.HTTP2_CONNECTION_PREFACE, 0, Http2Constants.HTTP2_CONNECTION_PREFACE.Length, cancelToken, realTime);
        }

        private async ValueTask HandleSendFrameCommand(SendFrameCommand command, ISocket socket, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (Http2Frame frame = command.ToSend)
            {
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                    frame.FrameType, frame.StreamId, frame.Flags, frame.PayloadLength);
#endif
                await frame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
            }
        }

        private async ValueTask HandleSendDataFrameCommand(SendDataFrameCommand command, ISocket socket, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (Http2DataFrame frame = command.ToSend)
            {
                Http2Stream stream;
                if (!_activeStreams.TryGetValue(frame.StreamId, out stream))
                {
                    // FIXME this logic isn't working properly yet

                    // If the stream is not in the active streams dict, it has likely been closed by the
                    // remote peer sending RST_STREAM, or it has timed out or something.
                    // So just don't bother sending the data in this case.
                    //logger.Log(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Skipping outgoing data frame on stream {0}", frame.StreamId);
                    stream = null;
                }

                // BUGBUG There's a race condition where the settings might have changed since this command was issued,
                // and the max frame size has decreased. In that case, we'd have to split this single frame into multiples.
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                    frame.FrameType, frame.StreamId, frame.Flags, frame.PayloadLength);
#endif
                await frame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);

                if (frame.EndStream && stream != null)
                {
                    if (_isClient)
                    {
                        stream.State = StreamState.HalfClosedLocal;
                    }
                    else
                    {
                        CloseStream(stream);
                        _activeStreams.Remove(frame.StreamId); // ?
                    }
                }
            }
        }

        private async ValueTask HandleUpdateSettingsCommand(UpdateLocalSettingsCommand command, ISocket socket, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (Http2SettingsFrame frame = Http2SettingsFrame.CreateOutgoingSettingsFrame(command.NewSettings, serializeAllSettings: true))
            {
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                    frame.FrameType, frame.StreamId, frame.Flags, frame.PayloadLength);
#endif
                await frame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
                int changeToIncomingFlowControlWindow = command.NewSettings.InitialWindowSize - _localSettings.InitialWindowSize;
                if (changeToIncomingFlowControlWindow != 0)
                {
                    // settings updates do not apply to the entire connection flow control window
                    //_overallConnectionIncomingFlowWindow.AugmentCredits(changeToIncomingFlowControlWindow);
                    // Update the control window on all incoming streams.
                    foreach (var stream in _activeStreams)
                    {
                        stream.Value.IncomingFlowControlWindow.AugmentCredits(changeToIncomingFlowControlWindow);
                        // TODO do we need to set any signals on the stream now that there are more credits available?
                    }
                }

                _localSettings = command.NewSettings;

#if HTTP2_DEBUG
                _logger.Log("Applying new local settings", LogLevel.Vrb);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tLocal EnablePush: {0}", frame.Settings.EnablePush);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tLocal HeaderTableSize: {0}", frame.Settings.HeaderTableSize);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tLocal InitialWindowSize: {0}", frame.Settings.InitialWindowSize);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tLocal MaxConcurrentStreams: {0}", frame.Settings.MaxConcurrentStreams);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tLocal MaxFrameSize: {0}", frame.Settings.MaxFrameSize);
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "\tLocal MaxHeaderListSize: {0}", frame.Settings.MaxHeaderListSize);
#endif
            }
        }

        private async ValueTask HandleAcknowledgeSettingsCommand(
            AcknowledgeSettingsCommand command,
            ISocket socket,
            HPackEncoder headerEncoder,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            using (Http2Frame frame = Http2SettingsFrame.CreateOutgoingAckFrame())
            {
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                    frame.FrameType, frame.StreamId, frame.Flags, frame.PayloadLength);
#endif
                await frame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);

                // Apply the change to header encoder table size right here.
                if (headerEncoder.DynamicTableSize != _remoteSettings.HeaderTableSize)
                {
                    _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                        "Changing header encoder table size from {0} to {1}", headerEncoder.DynamicTableSize, _remoteSettings.HeaderTableSize);

                    // Technically, the table size used by the remote decoder is a theoretical maximum.
                    // We are free to encode using a smaller dynamic table if we want, like if
                    // we are concerned about avoiding things such as DDoS attacks which try and trigger high memory use.
                    headerEncoder.DynamicTableSize = _remoteSettings.HeaderTableSize;
                }
            }
        }

        private static async ValueTask HandleCloseConnectionCommand(
            CloseConnectionCommand command,
            int lastProcessedStreamId,
            ISocket socket,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            using (Http2Frame frame = Http2GoAwayFrame.CreateOutgoing(lastProcessedStreamId, command.Reason, command.DebugMessage))
            {
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type GoAway Stream {0} Flags {1:X2} Reason {2} Len {3}",
                    frame.StreamId, frame.Flags, command.Reason, frame.PayloadLength);
#endif
                await frame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
            }
        }

        private async ValueTask HandleResetStreamCommand(
            ResetStreamCommand command,
            ISocket socket,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            // TODO need to update count of active concurrent streams here
            Http2Stream stream;
            if (_activeStreams.TryGetValueAndRemove(command.StreamId, out stream))
            {
                // fixme this stream doesn't always exist. not sure how to manage its states
                //stream.State = StreamState.HalfClosedLocal;
                CloseStream(stream);
            }

            using (Http2Frame frame = Http2RstStreamFrame.CreateOutgoing(command.StreamId, command.Reason))
            {
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type RstStream Stream {0} Flags {1:X2} Reason {2} Len {3}",
                    frame.StreamId, frame.Flags, command.Reason, frame.PayloadLength);
#endif
                await frame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
            }
        }

        private async ValueTask HandleSendPingCommand(
            SendPingCommand command,
            ISocket socket,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            // don't need a Using clause as ownership of idisposable is passed to the http2 frame
            PooledBuffer<byte> pingPayload = BufferPool<byte>.Rent(8);

            using (Http2Frame frame = Http2PingFrame.CreateOutgoing(pingPayload, ack: false))
            {
                // We don't care about the ping payload itself yet, this feature is not quite implemented
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type Ping Stream {0} Flags {1:X2}",
                    frame.StreamId, frame.Flags);
#endif
                await frame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
            }
        }


        private async ValueTask HandleSendRequestHeaderBlockCommand(
            SendRequestHeaderBlockCommand command,
            ISocket socket,
            Http2Settings remoteSettings,
            Http2Settings localSettings,
            HPackEncoder headerEncoder,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            foreach (Http2Frame headerBlockFrame in Http2Helpers.ConvertRequestHeadersToHeaderBlock(
                command.Headers,
                command.RequestMethod,
                command.RequestPath,
                _remoteAuthority,
                _scheme,
                command.ReservedStreamId,
                remoteSettings,
                headerEncoder,
                endOfStream: command.NoBodyContent))
            {
                // Just write to the socket. We are on the write thread so we have exclusive access,
                // which means we can enforce the rule that all headers and continuations must be grouped together.
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                    headerBlockFrame.FrameType, headerBlockFrame.StreamId, headerBlockFrame.Flags, headerBlockFrame.PayloadLength);
#endif
                await headerBlockFrame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
                headerBlockFrame.Dispose();
            }

            Http2Stream stream;
            if (!_activeStreams.TryGetValueOrSet(command.ReservedStreamId, out stream, command.ParentStream))
            {
                _logger.LogFormat(
                    LogLevel.Wrn,
                    DataPrivacyClassification.SystemMetadata,
                    "Attempted to send outgoing headers on nonexistent stream ID {0}; creating stream now",
                    command.ReservedStreamId);
            }

            if (command.NoBodyContent)
            {
                stream.State = StreamState.HalfClosedLocal;
            }

            // Something I notice that Firefox does is to just preallocate a huge flow control window (about 12 megabytes)
            // with a WINDOW_UPDATE immediately after an outgoing request HEADERS,
            // regardless of reported initial window size in the settings.
            // Presumably this is a workaround for misconfigured servers that don't properly respect settings (?).
            // But it does make sense for the global flowcontrol window because otherwise you can only send 64K in the first round-trip time window
            int amountToIncrease = _sessionPreferences.DesiredGlobalConnectionFlowWindow - localSettings.InitialWindowSize;
            if (amountToIncrease > 0)
            {
                stream.IncomingFlowControlWindow.AugmentCredits(amountToIncrease);
                await HandleSendFrameCommand(
                    new SendFrameCommand(
                        Http2WindowUpdateFrame.CreateOutgoing(
                        command.ReservedStreamId,
                        amountToIncrease)),
                    socket,
                    logger,
                    cancelToken,
                    realTime);
            }

            // Also send a window update for the entire connection in anticipation of response data to come for this request.
            if (_overallConnectionIncomingFlowWindow.AvailableCredits < (_sessionPreferences.DesiredGlobalConnectionFlowWindow / 2))
            {
                amountToIncrease = _sessionPreferences.DesiredGlobalConnectionFlowWindow - _overallConnectionIncomingFlowWindow.AvailableCredits;
                _overallConnectionIncomingFlowWindow.AugmentCredits(amountToIncrease);
                await HandleSendFrameCommand(
                    new SendFrameCommand(
                        Http2WindowUpdateFrame.CreateOutgoing(
                        0,
                        amountToIncrease)),
                    socket,
                    logger,
                    cancelToken,
                    realTime);
            }
        }

        private async Task HandleSendResponseHeaderBlockCommand(
            SendResponseHeaderBlockCommand command,
            ISocket socket,
            Http2Settings currentSettings,
            HPackEncoder headerEncoder,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            foreach (Http2Frame headerBlockFrame in Http2Helpers.ConvertResponseHeadersToHeaderBlock(
                command.Headers,
                command.ResponseCode,
                command.StreamId,
                currentSettings,
                headerEncoder,
                endOfStream: command.EndOfStream))
            {
                // Just write to the socket. We are on the write thread so we have exclusive access,
                // which means we can enforce the rule that all headers and continuations must be grouped together.
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                    headerBlockFrame.FrameType, headerBlockFrame.StreamId, headerBlockFrame.Flags, headerBlockFrame.PayloadLength);
#endif
                await headerBlockFrame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
                headerBlockFrame.Dispose();
            }

            Http2Stream stream;
            if (command.EndOfStream && _activeStreams.TryGetValue(command.StreamId, out stream))
            {
                stream.State = StreamState.Closed;
                // should be closed by response context
                //CloseStream(stream);
            }
        }

        private async Task HandleSendResponseTrailerBlockCommand(
            SendResponseTrailerBlockCommand command,
            ISocket socket,
            Http2Settings currentSettings,
            HPackEncoder headerEncoder,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            foreach (Http2Frame headerBlockFrame in Http2Helpers.ConvertResponseTrailersToTrailerBlock(
                command.Trailers,
                command.StreamId,
                currentSettings,
                headerEncoder))
            {
                // Just write to the socket. We are on the write thread so we have exclusive access,
                // which means we can enforce the rule that all headers and continuations must be grouped together.
#if HTTP2_DEBUG
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Sending outgoing HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3}",
                    headerBlockFrame.FrameType, headerBlockFrame.StreamId, headerBlockFrame.Flags, headerBlockFrame.PayloadLength);
#endif
                await headerBlockFrame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
                headerBlockFrame.Dispose();
            }

            // End of stream is implied after writing trailers.
            Http2Stream stream;
            if (_activeStreams.TryGetValue(command.StreamId, out stream))
            {
                stream.State = StreamState.Closed;
                // should be closed by response context
                //CloseStream(stream);
            }
        }

        private async ValueTask HandleSendPushPromiseFrameCommand(
            SendPushPromiseFrameCommand command,
            ISocket socket,
            Http2Settings currentSettings,
            HPackEncoder headerEncoder,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            foreach (Http2Frame headerBlockFrame in Http2Helpers.ConvertRequestHeadersToPushPromiseHeaderBlock(
                command.RequestPatternHeaders,
                command.RequestMethod,
                command.RequestPath,
                _localAuthority,
                _scheme,
                command.PrimaryStreamId,
                command.ReservedStreamId,
                currentSettings,
                headerEncoder,
                endOfStream: false))
            {
                using (headerBlockFrame)
                {
#if HTTP2_DEBUG
                    logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                        "Sending outgoing HTTP/2 frame Type {0} Stream {1} Flags {2:X2} Len {3} Promised Stream ID {4}",
                        headerBlockFrame.FrameType, headerBlockFrame.StreamId, headerBlockFrame.Flags, headerBlockFrame.PayloadLength, command.ReservedStreamId);
#endif
                    await headerBlockFrame.WriteToSocket(socket, cancelToken, realTime).ConfigureAwait(false);
                }
            }
        }
    }
}
