//using Durandal.API;
//using Durandal.Common.Net;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Xml;
//using Durandal.Common.File;
//using Durandal.Common.Audio;
//using Durandal.Common.Logger;
//using Durandal.Common.MathExt;
//using Durandal.Common.Utils;
//using Durandal.Common.Tasks;
//using Durandal.Common.IO;
//using Durandal.Common.Net.WebSocket;
//using Durandal.Common.Time;

//namespace Durandal.Common.Speech.SR.Cortana
//{
//    internal class TrumanWebSocketClient : IDisposable
//    {
//        private ISocket _socket;
//        private readonly IRealTimeProvider _realTime;
//        private readonly string _authToken;
//        private readonly string _locale;
//        private readonly IRandom _random = new FastRandom();
//        private readonly string _wsConnectionId = Guid.NewGuid().ToString("N");
//        private readonly string _conversationId = Guid.NewGuid().ToString("D");
//        private readonly string _impressionId = Guid.NewGuid().ToString("N");
//        private readonly string _utteranceId = Guid.NewGuid().ToString("D");
//        private readonly string _audioRequestId = Guid.NewGuid().ToString("D");
//        private EventWaitHandle _closeWriteSignal = new EventWaitHandle(false, EventResetMode.ManualReset);
//        private EventWaitHandle _readClosedSignal = new EventWaitHandle(false, EventResetMode.ManualReset);
//        private EventWaitHandle _writeClosedSignal = new EventWaitHandle(false, EventResetMode.ManualReset);
//        private CancellationTokenSource _closeSocket = new CancellationTokenSource();
//        private readonly ConcurrentBuffer<short> _audioBuffer = new ConcurrentBuffer<short>(AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE * 5);
//        private readonly ILogger _logger;
//        private int _messageId = 0;
//        private bool _audioStarted = false;
//        private int _disposed = 0;
//        private Task _asyncReadWriteTask;

//        private static IDictionary<string, string> LANGUAGE_CODE_MAPPING = new Dictionary<string, string>
//        {
//            { "ar-sa", "1025" },
//            { "cs-cz", "1029" },
//            { "da-dk", "1030" },
//            { "de-de", "1031" },
//            { "en-au", "1033" },
//            { "en-ca", "1033" },
//            { "en-in", "1033" },
//            { "en-gb", "2057" },
//            { "en-US", "1033" },
//            { "es-es", "3082" },
//            { "es-mx", "2058" },
//            { "fi-fi", "1035" },
//            { "fr-ca", "3084" },
//            { "fr-fr", "1036" },
//            { "it-it", "1040" },
//            { "ja-jp", "1041" },
//            { "ko-kr", "1042" },
//            { "nl-nl", "1043" },
//            { "nb-no", "1044" },
//            { "pl-pl", "1045" },
//            { "pt-br", "1046" },
//            { "pt-pt", "2070" },
//            { "ru-ru", "1049" },
//            { "sv-se", "1053" },
//            { "th-th", "1054" },
//            { "zh-cn", "2052" },
//            { "zh-tw", "1028" }
//        };

//        /// <summary>
//        /// Event which is fired when a websocket message comes from the remote client.
//        /// Returns true if the channel should keep reading packets
//        /// </summary>
//        public delegate bool GotMessageCallback(WebSocketMessage message);

//        private GotMessageCallback _callback;

//        /// <summary>
//        /// Constructs a websocket client which operates over the given socket to communicate with CU service
//        /// </summary>
//        /// <param name="socket"></param>
//        /// <param name="authToken"></param>
//        /// <param name="locale"></param>
//        /// <param name="logger"></param>
//        public TrumanWebSocketClient(ISocket socket, string authToken, string locale, ILogger logger, GotMessageCallback callback, IRealTimeProvider realTime)
//        {
//            _socket = socket;
//            _authToken = authToken;
//            _locale = FormatLocale(locale);
//            _logger = logger;
//            _callback = callback;
//            _realTime = realTime;
//        }

//        ~TrumanWebSocketClient()
//        {
//            Dispose(false);
//        }

//        /// <summary>
//        /// Opens the websocket connection
//        /// </summary>
//        /// <returns>True if connection succeeded</returns>
//        public async Task<bool> OpenStream()
//        {
//            try
//            {
//                byte[] nonce = new byte[16];
//                _random.NextBytes(nonce);
//                string nonceString = Convert.ToBase64String(nonce);
//                byte[] message = Encoding.UTF8.GetBytes(
//    "GET /ws/speech/recognize HTTP/1.1\r\n" +
//    "Connection: Upgrade\r\n" +
//    "Upgrade: websocket\r\n" +
//    "Authorization: Bearer " + _authToken + "\r\n" +
//    "User-Agent: Mozilla/4.0 (Windows 8; Unknown;Unknown;ProcessName/AppName=Durandal-" + SVNVersionInfo.MajorVersion + "." + SVNVersionInfo.MinorVersion + ";DeviceType=Near;SpeechClient=1.0.160824)\r\n" +
//    //"User-Agent: Mozilla/4.0 (Windows 8; Unknown;Unknown;ProcessName/AppName=Unknown;DeviceType=Near;SpeechClient=1.0.160824)\r\n" +
//    "X-WebSocketConnectionId: " + _wsConnectionId + "\r\n" +
//    "X-Shoutouts-To: jonham, shortskirts, inque, gargaj, smash, asd, jmspeex\r\n" +
//    "X-CU-LogLevel: 0\r\n" +
//    "X-Search-AppID: Unknown\r\n" +
//    "X-Search-Market: " + _locale.ToLowerInvariant() + "\r\n" +
//    "X-Search-UILang: " + _locale.ToLowerInvariant() + "\r\n" +
//    "Sec-WebSocket-Key: " + nonceString + "\r\n" +
//    "Sec-WebSocket-Version: 13\r\n" +
//    "Host: websockets.platform.bing.com\r\n\r\n");

//                await _socket.WriteAsync(message, 0, message.Length, _closeSocket.Token).ConfigureAwait(false);
//                await _socket.FlushAsync(_closeSocket.Token).ConfigureAwait(false);

//                // Read until we get to /r/n/r/n which denotes the end of HTTP handshake

//                using (PooledBuffer<byte> pooledBuffer = BufferPool<byte>.Rent())
//                {
//                    byte[] responseBuf = pooledBuffer.Buffer;
//                    int cur = 0;
//                    bool reading = true;
//                    while (reading && cur < responseBuf.Length)
//                    {
//                        int bytesRead = await _socket.ReadAnyAsync(responseBuf, cur, responseBuf.Length - cur, CancellationToken.None, _realTime).ConfigureAwait(false);
//                        cur += bytesRead;
//                        reading = bytesRead != 0;

//                        if (cur >= 4 &&
//                            responseBuf[cur - 4] == (byte)('\r') &&
//                            responseBuf[cur - 3] == (byte)('\n') &&
//                            responseBuf[cur - 2] == (byte)('\r') &&
//                            responseBuf[cur - 1] == (byte)('\n'))
//                        {
//                            reading = false;
//                        }
//                    }

//                    string responseString = Encoding.UTF8.GetString(responseBuf, 0, cur);

//                    if (!responseString.Contains("HTTP/1.1 101"))
//                    {
//                        if (responseString.Contains("\r\n"))
//                        {
//                            string httpStatus = responseString.Substring(0, responseString.IndexOf("\r\n"));
//                            _logger.Log("SR service connection was rejected. HTTP status message: " + httpStatus, LogLevel.Err);
//                        }
//                        else
//                        {
//                            _logger.Log("SR service connection was rejected", LogLevel.Err);
//                        }

//                        return false;
//                    }

//                    // Open the WS read/write tasks
//                    _asyncReadWriteTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () => await AsyncReadWrite().ConfigureAwait(false));
//                }

//                return true;
//            }
//            catch (Exception e)
//            {
//                _logger.Log("CU socket failed to connect", LogLevel.Err);
//				_logger.Log(e, LogLevel.Err);
//            }

//            return false;
//        }

//        public void Dispose()
//        {
//            Dispose(true);
//            GC.SuppressFinalize(this);
//        }

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!AtomicOperations.ExecuteOnce(ref _disposed))
//            {
//                return;
//            }

//            if (disposing)
//            {
//                try
//                {
//                    //_logger.Log("Disposing of socket connection");
//                    _closeWriteSignal.Set();
//                    _closeSocket.Cancel();
//                    // FIXME Is there a more graceful way to do this?
//                    if (!_readClosedSignal.WaitOne(50) || !_writeClosedSignal.WaitOne(50))
//                    {
//                        _logger.Log("Pipe never closed. Forcing socket shutdown anyways", LogLevel.Wrn);
//                    }

//                    _socket.Disconnect(false);
//                    _socket = null;
//                    _closeWriteSignal.Dispose();
//                    _closeWriteSignal = null;
//                    _readClosedSignal.Dispose();
//                    _readClosedSignal = null;
//                    _closeSocket.Dispose();
//                    _closeSocket = null;
//                    _writeClosedSignal?.Dispose();
//                }
//                catch (Exception e)
//                {
//                    _logger.Log("Error while closing CU socket: " + e.Message, LogLevel.Err);
//                }
//            }
//        }

//        /// <summary>
//        /// Puts the specified audio data into the queue to be sent to SR service
//        /// </summary>
//        /// <param name="audio">The audio to send. May be null</param>
//        /// <param name="isFinal">If true, this audio will be flagged as the final audio packet for the connection</param>
//        public void SendAudio(AudioChunk audio, bool isFinal = false)
//        {
//            if (audio != null)
//            {
//                _audioBuffer.Write(audio.Data);
//            }

//            if (isFinal)
//            {
//                _closeWriteSignal.Set();
//            }
//        }

//        #region Socket Read/Write

//        private async Task AsyncReadWrite()
//        {
//            Task read = RunAsyncRead();
//            Task write = RunAsyncWrite();
//            await Task.WhenAll(read, write).ConfigureAwait(false);
//        }

//        /// <summary>
//        /// A background task which continually reads from the websocket connection and parses incoming packets
//        /// </summary>
//        /// <returns></returns>
//        private async Task RunAsyncRead()
//        {
//            IRealTimeProvider readTaskTime = _realTime.Fork("TrumanWebSocketRead");
//            try
//            {
//                //_logger.Log("Read task is started");
//                using (RecyclableMemoryStream fragmentBuffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
//                {
//                    WebSocketOpcode fragmentOpcode = WebSocketOpcode.Continuation;
//                int fragmentSize = 0;
//                bool reading = true;
//                byte[] headerBuf = new byte[10];
//                int headerCursor;
//                    while (reading)
//                    {
//                        headerCursor = 0;
//                        for (int c = 0; c < headerBuf.Length; c++)
//                        {
//                            headerBuf[c] = 0;
//                        }

//                        // Read 2 bytes from the header
//                        while (headerCursor < 2)
//                        {
//                            if (_closeSocket.Token.IsCancellationRequested)
//                            {
//                                return;
//                            }

//                            int bytesRead = await _socket.ReadAnyAsync(headerBuf, headerCursor, 2 - headerCursor, _closeSocket.Token, readTaskTime).ConfigureAwait(false);
//                            if (bytesRead == 0 || _closeSocket.Token.IsCancellationRequested)
//                            {
//                                return;
//                            }

//                            headerCursor += bytesRead;
//                        }

//                        // Parse FIN, opcode, and main length field
//                        bool packet_FIN = (0x80 & headerBuf[0]) != 0;
//                        WebSocketOpcode packet_opcode = (WebSocketOpcode)(headerBuf[0] & 0x0F);
//                        byte packet_len1 = (byte)(headerBuf[1] & 0x7F);
//                        int packet_len = packet_len1;
//                        int headerEnd = 2;
//                        if (packet_len1 == 127)
//                        {
//                            headerEnd = 10;
//                        }
//                        else if (packet_len1 == 126)
//                        {
//                            headerEnd = 4;
//                        }

//                        // Read the extended length field if applicable
//                        while (headerCursor < headerEnd)
//                        {
//                            if (_closeSocket.Token.IsCancellationRequested)
//                            {
//                                return;
//                            }
//                            int bytesRead = await _socket.ReadAnyAsync(headerBuf, headerCursor, headerEnd - headerCursor, _closeSocket.Token, readTaskTime).ConfigureAwait(false);
//                            if (bytesRead == 0 || _closeSocket.Token.IsCancellationRequested)
//                            {
//                                return;
//                            }

//                            headerCursor += bytesRead;
//                        }

//                        //string[] byteStrings = new string[headerCursor];
//                        //for (int c = 0; c < headerCursor; c++)
//                        //{
//                        //    byteStrings[c] = string.Format("{0:X2}", headerBuf[c]);
//                        //}
//                        //_logger.Log("Got WS message header { " + string.Join(",", byteStrings) + "}");

//                        if (packet_len1 == 127)
//                        {
//                            byte[] packet_len2 = new byte[8];
//                            CopyBytesReverse(headerBuf, 2, packet_len2, 0, 8);
//                            packet_len = (int)BitConverter.ToUInt64(packet_len2, 0);
//                        }
//                        else if (packet_len1 == 126)
//                        {
//                            byte[] packet_len2 = new byte[2];
//                            CopyBytesReverse(headerBuf, 2, packet_len2, 0, 2);
//                            packet_len = BitConverter.ToUInt16(packet_len2, 0);
//                        }

//                        // Now finally read the packet body
//                        byte[] payload = new byte[packet_len];
//                        int payloadCursor = 0;
//                        while (payloadCursor < packet_len)
//                        {
//                            if (_closeSocket.Token.IsCancellationRequested)
//                            {
//                                return;
//                            }
//                            int bytesRead = await _socket.ReadAnyAsync(payload, payloadCursor, packet_len - payloadCursor, _closeSocket.Token, readTaskTime).ConfigureAwait(false);
//                            if (bytesRead == 0 || _closeSocket.Token.IsCancellationRequested)
//                            {
//                                return;
//                            }

//                            payloadCursor += bytesRead;
//                        }

//                        if (packet_opcode == WebSocketOpcode.CloseConnection)
//                        {
//                            reading = false;
//                            continue;
//                        }

//                        // Is it a ping or pong? Ignore it.
//                        if (packet_opcode == WebSocketOpcode.Ping || packet_opcode == WebSocketOpcode.Pong)
//                        {
//                            continue;
//                        }

//                        // Is it a continuation?
//                        if (!packet_FIN)
//                        {
//                            // buffer data
//                            fragmentBuffer.Write(payload, 0, packet_len);
//                            fragmentSize += packet_len;
//                            // Save the original opcode if applicable
//                            if (packet_opcode != WebSocketOpcode.Continuation)
//                            {
//                                fragmentOpcode = packet_opcode;
//                            }

//                            continue;
//                        }

//                        WebSocketMessage message;

//                        if (fragmentSize > 0)
//                        {
//                            // Finishing a fragmented packet
//                            // read from the buffer
//                            byte[] recreatedPacket = new byte[fragmentSize + packet_len];
//                            fragmentBuffer.Seek(0, SeekOrigin.Begin);
//                            fragmentBuffer.Read(recreatedPacket, 0, fragmentSize);
//                            // and then concatenate it with the current packet payload
//                            ArrayExtensions.MemCopy(payload, 0, recreatedPacket, fragmentSize, packet_len);

//                            message = new WebSocketMessage()
//                            {
//                                Opcode = fragmentOpcode,
//                                Data = recreatedPacket
//                            };

//                            fragmentBuffer.Seek(0, SeekOrigin.Begin);
//                            fragmentSize = 0;
//                            fragmentOpcode = WebSocketOpcode.Continuation;
//                        }
//                        else
//                        {
//                            // Unfragmented packet
//                            message = new WebSocketMessage()
//                            {
//                                Opcode = packet_opcode,
//                                Data = payload
//                            };
//                        }

//                        // Now dispatch the packet to the client logic
//                        if (!_callback(message))
//                        {
//                            reading = false;
//                        }
//                    }
//                }
//            }
//            catch (ObjectDisposedException e)
//            {
//				_logger.Log("Websocket read error", LogLevel.Err);
//				_logger.Log(e, LogLevel.Err);
//            }
//            catch (TaskCanceledException)
//            {
//                _logger.Log("Websocket read connection timed out", LogLevel.Err);
//            }
//            catch (Exception e)
//            {
//                _logger.Log("Websocket read error", LogLevel.Err);
//				_logger.Log(e, LogLevel.Err);
//            }
//            finally
//            {
//                //_logger.Log("Read task is finished");
//                if (_readClosedSignal != null)
//                {
//                    _readClosedSignal.Set();
//                }

//                readTaskTime.Merge();
//            }
//        }

//        /// <summary>
//        /// A background task which continually monitors the audio buffer and sends incoming audio to the websocket
//        /// </summary>
//        /// <returns></returns>
//        private async Task RunAsyncWrite()
//        {
//            try
//            {
//                //_logger.Log("Write task is started");
//                await SendAudioHeaders().ConfigureAwait(false);
//                bool closeWrite = false;
//                int timeLeftToWait = 10000;
//                while (!closeWrite && !_closeSocket.Token.IsCancellationRequested && timeLeftToWait > 0)
//                {
//                    await SendAudioData().ConfigureAwait(false);
//                    closeWrite = _closeWriteSignal.WaitOne(5);
//                    timeLeftToWait -= 5;
//                }
            
//                await SendAudioFinish().ConfigureAwait(false);
//            }
//            catch (ObjectDisposedException e)
//            {
//				_logger.Log("Websocket write error", LogLevel.Err);
//				_logger.Log(e, LogLevel.Err);
//            }
//            catch (TaskCanceledException)
//            {
//                _logger.Log("Websocket write connection timed out", LogLevel.Err);
//            }
//            catch (Exception e)
//            {
//				_logger.Log("Websocket write error", LogLevel.Err);
//				_logger.Log(e, LogLevel.Err);
//            }
//            finally
//            {
//                //_logger.Log("Write task is finished");

//                if (_writeClosedSignal != null)
//                {
//                    _writeClosedSignal.Set();
//                }
//            }
//        }

//        #endregion
        
//        private static string GetLanguageCode(string locale)
//        {
//            if (string.IsNullOrEmpty(locale) || locale.Length < 2)
//            {
//                return "1033";
//            }

//            string code;
//            if (LANGUAGE_CODE_MAPPING.TryGetValue(locale.ToLowerInvariant(), out code))
//            {
//                return code;
//            }

//            return "1033";
//        }

//        private static string FormatLocale(string locale)
//        {
//            if (string.IsNullOrEmpty(locale) || locale.Length < 5)
//            {
//                return locale;
//            }

//            return locale.Substring(0, 3).ToLowerInvariant() + locale.Substring(3).ToUpperInvariant();
//        }

//        /// <summary>
//        /// Sends the audio connection info and conversation context to CU. This is done before any audio can be sent.
//        /// </summary>
//        /// <returns></returns>
//        private async Task SendAudioHeaders()
//        {
//            string requestId = Guid.NewGuid().ToString("D");
//            string currentTime = DateTimeOffset.UtcNow.AddMinutes(-420).ToString("yyyy-MM-ddTHH:mm:ss-0800");
//            string languageCode = GetLanguageCode(_locale);
//            string payloadString =
//"X-CU-ClientVersion: 4.0.150429\r\n" +
//"X-CU-ConversationId: " + _conversationId + "\r\n" +
//"X-CU-Locale: " + _locale.ToLowerInvariant() + "\r\n" +
//"X-CU-LogLevel: 1\r\n" +
//"X-CU-RequestId: " + requestId + "\r\n" +
//"X-LOBBY-MESSAGE-TYPE: connection.context\r\n" +
//"X-Search-IG: " + _impressionId + "\r\n" +
//"X-WebSocketMessageId: C#" + _messageId++ + "\r\n\r\n" +
//"{\"Groups\":{\"ConversationContext\":{\"Id\":\"ConversationContext\",\"Info\":{\"PreferClientReco\":false,\"SystemAction\":\"Unknown\",\"TurnId\":\"0\"}},\"LocalProperties\":{\"Id\":\"LocalProperties\",\"Info\":{\"AudioSourceType\":\"SpeechApp\",\"CurrentTime\":\"" + currentTime + "\",\"DrivingModeActive\":false,\"GeoLocation\":\"{\\\"Accuracy\\\":10.000000,\\\"Latitude\\\":0.000000,\\\"Longitude\\\":0.000000,\\\"Uri\\\":\\\"entity://GeoCoordinates\\\",\\\"Version\\\":\\\"1.0\\\"}\",\"InCall\":false,\"InvocationSourceType\":\"SpeechApp\",\"IsTextInput\":false,\"LockState\":\"Unlocked\",\"ModeOfTravel\":\"Undefined\",\"ProximitySensorState\":\"Uncovered\",\"SpeechAppInitiatedRequest\":false,\"SystemInfo\":\"{\\\"Branch\\\":\\\"Windows\\\",\\\"CortanaEnabled\\\":false,\\\"DefaultOperatorName\\\":\\\"\\\",\\\"DeviceMake\\\":\\\"Microsoft\\\",\\\"DeviceModel\\\":\\\"PC\\\",\\\"LanguageCode\\\":\\\"" + languageCode + "\\\",\\\"Mkt\\\":\\\"" + _locale + "\\\",\\\"OsName\\\":\\\"Windows\\\",\\\"OsVersion\\\":\\\"10\\\",\\\"RegionalFormatCode\\\":\\\"" + _locale + "\\\",\\\"TimeZone\\\":\\\"Pacific Daylight Time\\\"}\"},\"Items\":[]},\"RecoProperties\":{\"Info\":{\"OptIn\":false,\"Scenario\":\"Unknown\"}}},\"OnScreenItems\":[]}";
//            byte[] data = Encoding.UTF8.GetBytes(payloadString);
//            byte[] wsMessage = PrepareWebsocketMessage(data, WebSocketOpcode.TextFrame, _random);
//            await _socket.WriteAsync(wsMessage, 0, wsMessage.Length, _closeSocket.Token).ConfigureAwait(false);
//            //_logger.Log("Sent audio headers");
//        }

//        /// <summary>
//        /// Sends all available buffered audio data as websocket messages to the current socket
//        /// </summary>
//        /// <returns></returns>
//        private async Task SendAudioData()
//        {
//            int AUDIO_CHUNK_SIZE = 160;
//            bool dataWritten = false;
//            while (_audioBuffer.Available() > AUDIO_CHUNK_SIZE)
//            {
//                string payloadString =
//"EncodingFormat: 1\r\n" +
//(_audioStarted ? "" : "Start: True\r\n") + 
//"X-CU-ClientVersion: 4.0.150429\r\n" +
//"X-CU-ConversationId: " + _conversationId + "\r\n" +
//"X-CU-Locale: " + _locale.ToLowerInvariant() + "\r\n" +
//"X-CU-LogLevel: 1\r\n" +
//"X-CU-RequestId: " + _audioRequestId + "\r\n" +
//"X-CU-UtteranceId: " + _utteranceId + "\r\n" +
//(_audioStarted ? "X-LOBBY-MESSAGE-TYPE: audio.stream.body\r\n" : "X-LOBBY-MESSAGE-TYPE: audio.stream.start\r\nX-Search-IG: " + _impressionId + "\r\n") +
//"X-WebSocketMessageId: C#" + _messageId++ + "\r\n";

//                _audioStarted = true;
//                byte[] headerBytes = Encoding.UTF8.GetBytes(payloadString);
//                byte[] payloadBytes = new byte[2 + headerBytes.Length + (AUDIO_CHUNK_SIZE * 2)];
//                ushort headerSize = (ushort)headerBytes.Length;
//                CopyBytesReverse(BitConverter.GetBytes(headerSize), 0, payloadBytes, 0, 2);
//                ArrayExtensions.MemCopy(headerBytes, 0, payloadBytes, 2, headerSize);
//                short[] audio = _audioBuffer.Read(AUDIO_CHUNK_SIZE);
//                byte[] audioBytes = AudioMath.ShortsToBytes(audio);
//                ArrayExtensions.MemCopy(audioBytes, 0, payloadBytes, headerSize + 2, (AUDIO_CHUNK_SIZE * 2));

//                // Now make it a WS message and write it
//                byte[] wsMessage = PrepareWebsocketMessage(payloadBytes, WebSocketOpcode.BinaryFrame, _random);
//                await _socket.WriteAsync(wsMessage, 0, wsMessage.Length, _closeSocket.Token).ConfigureAwait(false);
//                //_logger.Log("Sent " + payloadBytes.Length + " bytes");
//                //_logger.Log("Sent audio frame " + (_messageId - 1));
//                dataWritten = true;
//            }

//            if (dataWritten)
//            {
//                await _socket.FlushAsync(_closeSocket.Token).ConfigureAwait(false);
//            }
//        }

//        /// <summary>
//        /// Sends the audio.stream.end message to say we are done recognizing speech.
//        /// </summary>
//        /// <returns></returns>
//        private async Task SendAudioFinish()
//        {
//            // Write the close message
//            string requestId = Guid.NewGuid().ToString("D");
//            string payloadString =
//"X-CU-ClientVersion: 4.0.150429\r\n" +
//"X-CU-Locale: " + _locale.ToLowerInvariant() + "\r\n" +
//"X-CU-LogLevel: 1\r\n" +
//"X-CU-RequestId: " + _audioRequestId + "\r\n" +
//"X-LOBBY-MESSAGE-TYPE: audio.stream.end\r\n" +
//"X-WebSocketMessageId: C#" + _messageId++ + "\r\n";

//            byte[] headerBytes = Encoding.UTF8.GetBytes(payloadString);
//            byte[] payloadBytes = new byte[2 + headerBytes.Length];
//            ushort headerSize = (ushort)headerBytes.Length;
//            CopyBytesReverse(BitConverter.GetBytes(headerSize), 0, payloadBytes, 0, 2);
//            ArrayExtensions.MemCopy(headerBytes, 0, payloadBytes, 2, headerSize);

//            // Now make it a WS message and write it
//            byte[] wsMessage = PrepareWebsocketMessage(payloadBytes, WebSocketOpcode.BinaryFrame, _random);
//            await _socket.WriteAsync(wsMessage, 0, wsMessage.Length, _closeSocket.Token).ConfigureAwait(false);

//            // Send a bunch of pongs why not
//            //for (int c = 0; c < 200; c++)
//            //{
//            //    byte[] pongData = new byte[100];
//            //    _random.NextBytes(pongData);
//            //    wsMessage = PrepareWebsocketMessage(pongData, WebSocketOpcode.Pong, _random);
//            //    await _socket.WriteAsync(wsMessage, 0, wsMessage.Length);
//            //}

//            await _socket.FlushAsync(_closeSocket.Token).ConfigureAwait(false);

//            //_logger.Log("Sent audio finish");
//        }

//        #region Static helpers

//        /// <summary>
//        /// Performs a byte array copy which reverses the order of the copied bytes. This is done to
//        /// put multi-byte values into network byte order (big-endian) where BitConverter and other
//        /// methods will always do little-endian.
//        /// </summary>
//        /// <param name="source"></param>
//        /// <param name="sIndex"></param>
//        /// <param name="dest"></param>
//        /// <param name="dIndex"></param>
//        /// <param name="count"></param>
//        private static void CopyBytesReverse(byte[] source, int sIndex, byte[] dest, int dIndex, int count)
//        {
//            for (int c = 0; c < count; c++)
//            {
//                dest[dIndex + c] = source[sIndex + count - c - 1];
//            }
//        }

//        /// <summary>
//        /// Fills the destination buffer with N non-zero pseudorandom bytes
//        /// </summary>
//        /// <param name="rand"></param>
//        /// <param name="dest"></param>
//        /// <param name="count"></param>
//        private static void GenerateNonZeroBytes(IRandom rand, byte[] dest, int count)
//        {
//            for (int c = 0; c < count; c++)
//            {
//                dest[c] = 0;
//                while (dest[c] == 0)
//                {
//                    dest[c] = (byte)(rand.NextInt() & 0xFF);
//                }
//            }
//        }

//        /// <summary>
//        /// Wraps a packet of data with a Websocket header and returns the raw data to be sent on the wire.
//        /// </summary>
//        /// <param name="payload"></param>
//        /// <param name="opcode"></param>
//        /// <param name="randomProvider"></param>
//        /// <returns></returns>
//        private static byte[] PrepareWebsocketMessage(byte[] payload, WebSocketOpcode opcode, IRandom randomProvider)
//        {
//            int payloadSize = payload.Length;
//            int headerSize = 6;
//            if (payloadSize > 65535)
//            {
//                headerSize += 8;
//            }
//            else if (payloadSize > 125)
//            {
//                headerSize += 2;
//            }
            
//            byte[] response = new byte[payloadSize + headerSize];
//            // Set the flags
//            // we never use fragmentation so FIN is always set
//            response[0] = (byte)((int)opcode | 0x80);

//            // Set the length
//            if (payloadSize > 65535)
//            {
//                response[1] = (byte)(127 | 0x80);
//                CopyBytesReverse(BitConverter.GetBytes((ulong)payload.Length), 0, response, 2, 8);
//            }
//            else if (payloadSize > 125)
//            {
//                response[1] = (byte)(126 | 0x80);
//                CopyBytesReverse(BitConverter.GetBytes((ushort)payload.Length), 0, response, 2, 2);
//            }
//            else
//            {
//                response[1] = (byte)(payload.Length | 0x80);
//            }

//            // Set the mask
//            byte[] mask = new byte[4];
//            GenerateNonZeroBytes(randomProvider, mask, 4);
//            ArrayExtensions.MemCopy(mask, 0, response, headerSize - 4, 4);

//            // Copy the payload and apply the mask to it
//            int maskIter = 0;
//            int outIter = headerSize;
//            for (int c = 0; c < payloadSize; c++)
//            {
//                response[outIter++] = (byte)(mask[maskIter++] ^ payload[c]);
//                if (maskIter >= 4)
//                {
//                    maskIter = 0;
//                }
//            }

//            return response;
//        }

//        #endregion
//    }
//}
