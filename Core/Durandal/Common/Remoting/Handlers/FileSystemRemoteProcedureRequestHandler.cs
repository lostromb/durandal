using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting.Handlers
{
    /// <summary>
    /// Implements a handler that handles remote file system access on the server (host) side
    /// </summary>
    public class FileSystemRemoteProcedureRequestHandler : IRemoteProcedureRequestHandler, IDisposable
    {
        private readonly IFileSystem _targetFileSystem;
        private readonly ILogger _logger;
        private readonly FastConcurrentDictionary<string, FileStreamContainer> _openStreams = new FastConcurrentDictionary<string, FileStreamContainer>();
        private int _disposed = 0;

        public FileSystemRemoteProcedureRequestHandler(IFileSystem targetFileSystem, ILogger logger)
        {
            _targetFileSystem = targetFileSystem.AssertNonNull(nameof(targetFileSystem));
            _logger = logger.AssertNonNull(nameof(logger));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FileSystemRemoteProcedureRequestHandler()
        {
            Dispose(false);
        }
#endif

        public bool CanHandleRequestType(Type requestType)
        {
            return requestType == typeof(RemoteFileCreateDirectoryRequest) ||
                requestType == typeof(RemoteFileDeleteRequest) ||
                requestType == typeof(RemoteFileListRequest) ||
                requestType == typeof(RemoteFileMoveRequest) ||
                requestType == typeof(RemoteFileReadContentsRequest) ||
                requestType == typeof(RemoteFileStatRequest) ||
                requestType == typeof(RemoteFileWriteStatRequest) ||
                requestType == typeof(RemoteFileWriteContentsRequest) ||
                requestType == typeof(RemoteFileStreamOpenRequest) ||
                requestType == typeof(RemoteFileStreamReadRequest) ||
                requestType == typeof(RemoteFileStreamSeekRequest) ||
                requestType == typeof(RemoteFileStreamSetLengthRequest) ||
                requestType == typeof(RemoteFileStreamCloseRequest) ||
                requestType == typeof(RemoteFileStreamWriteRequest);
        }

        public Task HandleRequest(
            PostOffice postOffice,
            IRemoteDialogProtocol remoteProtocol,
            ILogger traceLogger,
            Tuple<object, Type> parsedMessage,
            MailboxMessage originalMessage,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            TaskFactory taskFactory)
        {
            if (parsedMessage.Item2 == typeof(RemoteFileCreateDirectoryRequest))
            {
                RemoteFileCreateDirectoryRequest req = parsedMessage.Item1 as RemoteFileCreateDirectoryRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");
                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<bool> successResponse;

                        try
                        {
                            VirtualPath path = new VirtualPath(req.DirectoryPath);
                            await _targetFileSystem.CreateDirectoryAsync(path).ConfigureAwait(false);
                            successResponse = new RemoteProcedureResponse<bool>(req.MethodName, true);
                        }
                        catch (Exception e)
                        {
                            successResponse = new RemoteProcedureResponse<bool>(req.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(successResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileDeleteRequest))
            {
                RemoteFileDeleteRequest req = parsedMessage.Item1 as RemoteFileDeleteRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<bool> successResponse;

                        try
                        {
                            VirtualPath path = new VirtualPath(req.TargetPath);
                            bool returnVal = await _targetFileSystem.DeleteAsync(path).ConfigureAwait(false);
                            successResponse = new RemoteProcedureResponse<bool>(req.MethodName, returnVal);
                        }
                        catch (Exception e)
                        {
                            successResponse = new RemoteProcedureResponse<bool>(req.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(successResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileListRequest))
            {
                RemoteFileListRequest req = parsedMessage.Item1 as RemoteFileListRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<List<string>> structuredResponse;

                        try
                        {
                            VirtualPath path = new VirtualPath(req.SourcePath);
                            IEnumerable<VirtualPath> list;
                            if (req.ListDirectories)
                            {
                                list = await _targetFileSystem.ListDirectoriesAsync(path).ConfigureAwait(false);
                            }
                            else
                            {
                                list = await _targetFileSystem.ListFilesAsync(path).ConfigureAwait(false);
                            }

                            List<string> returnVal = new List<string>();
                            foreach (VirtualPath foundPath in list)
                            {
                                returnVal.Add(foundPath.FullName);
                            }

                            structuredResponse = new RemoteProcedureResponse<List<string>>(req.MethodName, returnVal);
                        }
                        catch (Exception e)
                        {
                            structuredResponse = new RemoteProcedureResponse<List<string>>(req.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(structuredResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileMoveRequest))
            {
                RemoteFileMoveRequest req = parsedMessage.Item1 as RemoteFileMoveRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<bool> successResponse;

                        try
                        {
                            VirtualPath sourcePath = new VirtualPath(req.SourcePath);
                            VirtualPath targetPath = new VirtualPath(req.TargetPath);
                            await _targetFileSystem.MoveAsync(sourcePath, targetPath).ConfigureAwait(false);
                            successResponse = new RemoteProcedureResponse<bool>(req.MethodName, true);
                        }
                        catch (Exception e)
                        {
                            successResponse = new RemoteProcedureResponse<bool>(req.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(successResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileReadContentsRequest))
            {
                RemoteFileReadContentsRequest req = parsedMessage.Item1 as RemoteFileReadContentsRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<ArraySegment<byte>> structuredResponse;

                        try
                        {
                            // If the file doesn't exist, return empty array
                            VirtualPath filePath = new VirtualPath(req.FilePath);
                            if (await _targetFileSystem.ExistsAsync(filePath).ConfigureAwait(false))
                            {
                                using (MemoryStream bucket = new MemoryStream())
                                {
                                    using (Stream sourceStream = await _targetFileSystem.OpenStreamAsync(filePath, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
                                    {
                                        await sourceStream.CopyToAsync(bucket).ConfigureAwait(false);
                                    }

                                    structuredResponse = new RemoteProcedureResponse<ArraySegment<byte>>(req.MethodName, new ArraySegment<byte>(bucket.ToArray()));
                                }
                            }
                            else
                            {
                                structuredResponse = new RemoteProcedureResponse<ArraySegment<byte>>(req.MethodName, new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY));
                            }
                        }
                        catch (Exception e)
                        {
                            structuredResponse = new RemoteProcedureResponse<ArraySegment<byte>>(req.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(structuredResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileStatRequest))
            {
                RemoteFileStatRequest req = parsedMessage.Item1 as RemoteFileStatRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<RemoteFileStat> structuredResponse;

                        try
                        {
                            VirtualPath path = new VirtualPath(req.TargetPath);
                            ResourceType whatIs = await _targetFileSystem.WhatIsAsync(path).ConfigureAwait(false);
                            RemoteFileStat returnVal;
                            if (whatIs == ResourceType.Directory)
                            {
                                returnVal = new RemoteFileStat()
                                {
                                    Exists = true,
                                    IsDirectory = true
                                };
                            }
                            else if (whatIs == ResourceType.File)
                            {
                                FileStat localStat = await _targetFileSystem.StatAsync(path).ConfigureAwait(false);
                                returnVal = new RemoteFileStat()
                                {
                                    Exists = true,
                                    IsDirectory = false,
                                    CreationTime = localStat.CreationTime,
                                    LastAccessTime = localStat.LastAccessTime,
                                    LastWriteTime = localStat.LastWriteTime,
                                    Size = localStat.Size
                                };
                            }
                            else
                            {
                                returnVal = new RemoteFileStat()
                                {
                                    Exists = false
                                };
                            }
                            structuredResponse = new RemoteProcedureResponse<RemoteFileStat>(req.MethodName, returnVal);
                        }
                        catch (Exception e)
                        {
                            structuredResponse = new RemoteProcedureResponse<RemoteFileStat>(req.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(structuredResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileWriteStatRequest))
            {
                RemoteFileWriteStatRequest req = parsedMessage.Item1 as RemoteFileWriteStatRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<bool> structuredResponse;

                        try
                        {
                            VirtualPath path = new VirtualPath(req.TargetPath);
                            ResourceType whatIs = await _targetFileSystem.WhatIsAsync(path).ConfigureAwait(false);
                            bool returnVal = false;
                            if (whatIs == ResourceType.File)
                            {
                                await _targetFileSystem.WriteStatAsync(path, req.NewCreationTime, req.NewModificationTime).ConfigureAwait(false);
                                returnVal = true;
                            }

                            structuredResponse = new RemoteProcedureResponse<bool>(req.MethodName, returnVal);
                        }
                        catch (Exception e)
                        {
                            structuredResponse = new RemoteProcedureResponse<bool>(req.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(structuredResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileWriteContentsRequest))
            {
                RemoteFileWriteContentsRequest req = parsedMessage.Item1 as RemoteFileWriteContentsRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<bool> structuredResponse;

                        try
                        {
                            VirtualPath filePath = new VirtualPath(req.FilePath);
                            using (MemoryStream sourceStream = new MemoryStream(req.NewContents.Array, req.NewContents.Offset, req.NewContents.Count))
                            {
                                using (Stream targetStream = await _targetFileSystem.OpenStreamAsync(filePath, FileOpenMode.OpenOrCreate, FileAccessMode.Write).ConfigureAwait(false))
                                {
                                    await sourceStream.CopyToAsync(targetStream).ConfigureAwait(false);
                                }
                            }

                            structuredResponse = new RemoteProcedureResponse<bool>(req.MethodName, true);
                        }
                        catch (Exception e)
                        {
                            structuredResponse = new RemoteProcedureResponse<bool>(req.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(structuredResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileStreamOpenRequest))
            {
                RemoteFileStreamOpenRequest originalOpenStreamRequest = parsedMessage.Item1 as RemoteFileStreamOpenRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");

                string streamId = Guid.NewGuid().ToString("N");
                FileStreamContainer streamContainer = new FileStreamContainer()
                {
                    FilePath = null,
                    FileStream = null, // will be set later
                    LastTouched = realTime.Time,
                    StreamId = streamId,
                };

                _openStreams.Add(streamId, streamContainer);

                // Create a long-running task that will handle all requests made to this file stream.
                // This design ensures that all operations are processed serially without blocking the
                // request handler thread (the current thread)
                streamContainer.StreamProcessingTask = taskFactory.StartNew(async () =>
                {
                    // Start by opening the file stream.
                    try
                    {
                        RemoteProcedureResponse<RemoteFileStreamOpenResult> openStreamResponse;

                        try
                        {
                            RemoteFileStreamOpenResult result = new RemoteFileStreamOpenResult();
                            VirtualPath filePath = new VirtualPath(originalOpenStreamRequest.FilePath);
                            NonRealTimeStream newStream = await _targetFileSystem.OpenStreamAsync(
                                new FileStreamParams(filePath, Convert(originalOpenStreamRequest.OpenMode), Convert(originalOpenStreamRequest.AccessMode), Convert(originalOpenStreamRequest.ShareMode), null, null));
                            streamContainer.FileStream = newStream;
                            streamContainer.FilePath = filePath;
                            result.StreamId = streamId;
                            result.CanWrite = newStream.CanWrite;
                            result.CanRead = newStream.CanRead;
                            result.CanSeek = newStream.CanSeek;
                            if (newStream.CanSeek)
                            {
                                try { result.InitialFileLength = newStream.Length; } catch (Exception) { }
                            }

                            openStreamResponse = new RemoteProcedureResponse<RemoteFileStreamOpenResult>(originalOpenStreamRequest.MethodName, result);
                        }
                        catch (Exception e)
                        {
                            openStreamResponse = new RemoteProcedureResponse<RemoteFileStreamOpenResult>(originalOpenStreamRequest.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(openStreamResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);

                        // Now wait for additional serialized messages on the channel
                        while (!cancelToken.IsCancellationRequested)
                        {
                            FileStreamSerializedOperation nextOp = await streamContainer.OperationPipeline.ReceiveAsync(cancelToken, threadLocalTime);
                            if (nextOp == null)
                            {
                                // Pipeline disposed, no more work to do.
                                break;
                            }

                            //traceLogger.Log("Stream " + streamContainer.StreamId + " got message " + nextOp.MethodName);
                            streamContainer.LastTouched = threadLocalTime.Time;
                            if (string.Equals(nextOp.MethodName, RemoteFileStreamWriteRequest.METHOD_NAME, StringComparison.Ordinal))
                            {
                                // STREAM WRITE
                                RemoteProcedureResponse<bool> structuredResponse;

                                try
                                {
                                    //traceLogger.Log("Writing to file stream " + req.StreamId + " " + req.Data.Count + " bytes at position " + req.Position);

                                    // Seek if necessary (can happen if the caller wrote to one place, seeked, then starting writing elsewhere without a read in between)
                                    if (streamContainer.FileStream.Position != nextOp.Position)
                                    {
                                        if (!streamContainer.FileStream.CanSeek)
                                        {
                                            throw new InvalidOperationException("Attempted to seek on a non-seekable stream");
                                        }

                                        //traceLogger.Log("Seeking from position " + streamContainer.FileStream.Position + " to " + req.Position);
                                        streamContainer.FileStream.Position = nextOp.Position;
                                    }

                                    await streamContainer.FileStream.WriteAsync(nextOp.Data.Array, nextOp.Data.Offset, nextOp.Data.Count, cancelToken, threadLocalTime).ConfigureAwait(false);
                                    structuredResponse = new RemoteProcedureResponse<bool>(nextOp.MethodName, true);
                                }
                                catch (Exception e)
                                {
                                    structuredResponse = new RemoteProcedureResponse<bool>(nextOp.MethodName, e);
                                }

                                serializedResponse = remoteProtocol.Serialize(structuredResponse, traceLogger);
                                interstitialResponseMessage = new MailboxMessage(nextOp.ReplyMailbox, remoteProtocol.ProtocolId, serializedResponse);
                                interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                                interstitialResponseMessage.ReplyToId = nextOp.ReplyToId;
                                await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                            }
                            else if (string.Equals(nextOp.MethodName, RemoteFileStreamReadRequest.METHOD_NAME, StringComparison.Ordinal))
                            {
                                // STREAM READ
                                RemoteProcedureResponse<ArraySegment<byte>> structuredResponse;

                                try
                                {
                                    // It would be nice to not have to allocate a new byte array here, but it's going to get passed to the RPC response anyway, so we can't really help it
                                    // We rely on the caller to limit the size of its requests, because of speculative reads we have to satisfy the full
                                    // requested length if at all possible
                                    byte[] buffer = new byte[nextOp.Length];
                                    //traceLogger.Log("Reading from file stream " + req.StreamId + " " + req.Length + " bytes from position " + req.Position);

                                    // Check if this is a speculative read that has exceeded the file size. If so, just return 0 for end-of-stream
                                    if (streamContainer.FileStream.CanSeek && nextOp.Position >= streamContainer.FileStream.Length)
                                    {
                                        //traceLogger.Log("Read request at position " + req.Position + " exceeds file bounds, assumed to be speculative read");
                                        structuredResponse = new RemoteProcedureResponse<ArraySegment<byte>>(nextOp.MethodName, new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY));
                                    }
                                    else
                                    {
                                        // Seek if necessary
                                        if (streamContainer.FileStream.Position != nextOp.Position)
                                        {
                                            if (!streamContainer.FileStream.CanSeek)
                                            {
                                                throw new InvalidOperationException("Attempted to seek on a non-seekable stream");
                                            }

                                            //traceLogger.Log("Seeking from position " + streamContainer.FileStream.Position + " to " + req.Position);
                                            streamContainer.FileStream.Position = nextOp.Position;
                                        }

                                        int bytesRead = 0;
                                        if (streamContainer.FileStream.CanSeek)
                                        {
                                            // If the stream can seek, assume the caller is doing speculative reads, in which case we
                                            // are required to satisfy the entire requested read length until the end of stream is reached
                                            int maximumReadSize = (int)Math.Min(nextOp.Length, streamContainer.FileStream.Length - streamContainer.FileStream.Position);
                                            while (bytesRead < maximumReadSize)
                                            {
                                                bytesRead += await streamContainer.FileStream.ReadAsync(buffer, bytesRead, maximumReadSize - bytesRead, cancelToken, threadLocalTime).ConfigureAwait(false);
                                            }
                                        }
                                        else
                                        {
                                            bytesRead = await streamContainer.FileStream.ReadAsync(buffer, 0, nextOp.Length, cancelToken, threadLocalTime).ConfigureAwait(false);
                                        }

                                        structuredResponse = new RemoteProcedureResponse<ArraySegment<byte>>(nextOp.MethodName, new ArraySegment<byte>(buffer, 0, bytesRead));
                                    }

                                    //traceLogger.Log("Caller requested " + nextOp.Length + " bytes from position " + nextOp.Position + ", returning " + structuredResponse.ReturnVal.Count);
                                }
                                catch (Exception e)
                                {
                                    structuredResponse = new RemoteProcedureResponse<ArraySegment<byte>>(nextOp.MethodName, e);
                                }

                                serializedResponse = remoteProtocol.Serialize(structuredResponse, traceLogger);
                                interstitialResponseMessage = new MailboxMessage(nextOp.ReplyMailbox, remoteProtocol.ProtocolId, serializedResponse);
                                interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                                interstitialResponseMessage.ReplyToId = nextOp.ReplyToId;
                                await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                            }
                            else if (string.Equals(nextOp.MethodName, RemoteFileStreamCloseRequest.METHOD_NAME, StringComparison.Ordinal))
                            {
                                // STREAM CLOSE
                                RemoteProcedureResponse<bool> structuredResponse;

                                try
                                {
                                    streamContainer.FileStream.Dispose();
                                    streamContainer.OperationPipeline.Dispose();
                                    _openStreams.Remove(streamContainer.StreamId);
                                    structuredResponse = new RemoteProcedureResponse<bool>(nextOp.MethodName, true);
                                }
                                catch (Exception e)
                                {
                                    structuredResponse = new RemoteProcedureResponse<bool>(nextOp.MethodName, e);
                                }

                                serializedResponse = remoteProtocol.Serialize(structuredResponse, traceLogger);
                                interstitialResponseMessage = new MailboxMessage(nextOp.ReplyMailbox, remoteProtocol.ProtocolId, serializedResponse);
                                interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                                interstitialResponseMessage.ReplyToId = nextOp.ReplyToId;
                                await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                                //traceLogger.Log("Disposed file stream " + streamContainer.StreamId);
                                return; // Destroy this thread
                            }
                            else if (string.Equals(nextOp.MethodName, "DISPOSE", StringComparison.Ordinal))
                            {
                                // UNEXPECTED DISPOSE
                                try
                                {
                                    streamContainer.FileStream?.Dispose();
                                    streamContainer.OperationPipeline.Dispose();
                                }
                                catch (Exception) { }
                                return;
                            }
                            else
                            {
                                _logger.Log("File stream handler thread got unrecognized method name \"" + nextOp.MethodName + "\"", LogLevel.Err);
                                return;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });

                return streamContainer.StreamProcessingTask;
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileStreamWriteRequest))
            {
                RemoteFileStreamWriteRequest req = parsedMessage.Item1 as RemoteFileStreamWriteRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");
                FileStreamContainer streamContainer;
                if (!_openStreams.TryGetValue(req.StreamId, out streamContainer))
                {
                    try
                    {
                        throw new Exception("File stream not found (it may have closed from disuse): " + req.StreamId);
                    }
                    catch (Exception e)
                    {
                        RemoteProcedureResponse<bool> errorResponse = new RemoteProcedureResponse<bool>(req.MethodName, e);
                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(errorResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        return postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).AsTask();
                    }
                }

                streamContainer.OperationPipeline.Send(new FileStreamSerializedOperation()
                {
                    MethodName = req.MethodName,
                    ReplyMailbox = originalMessage.MailboxId,
                    ReplyToId = originalMessage.MessageId,
                    Position = req.Position,
                    Data = req.Data
                });

                return DurandalTaskExtensions.NoOpTask;
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileStreamReadRequest))
            {
                RemoteFileStreamReadRequest req = parsedMessage.Item1 as RemoteFileStreamReadRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");
                FileStreamContainer streamContainer;
                if (!_openStreams.TryGetValue(req.StreamId, out streamContainer))
                {
                    try
                    {
                        throw new Exception("File stream not found (it may have closed from disuse): " + req.StreamId);
                    }
                    catch (Exception e)
                    {
                        RemoteProcedureResponse<ArraySegment<byte>> errorResponse = new RemoteProcedureResponse<ArraySegment<byte>>(req.MethodName, e);
                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(errorResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        return postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).AsTask();
                    }
                }

                streamContainer.OperationPipeline.Send(new FileStreamSerializedOperation()
                {
                    MethodName = req.MethodName,
                    ReplyMailbox = originalMessage.MailboxId,
                    ReplyToId = originalMessage.MessageId,
                    Position = req.Position,
                    Length = req.Length
                });

                return DurandalTaskExtensions.NoOpTask;
            }
            else if (parsedMessage.Item2 == typeof(RemoteFileStreamCloseRequest))
            {
                RemoteFileStreamCloseRequest req = parsedMessage.Item1 as RemoteFileStreamCloseRequest;
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedFile");
                FileStreamContainer streamContainer;
                if (!_openStreams.TryGetValue(req.StreamId, out streamContainer))
                {
                    RemoteProcedureResponse<bool> streamNotFoundResponse = new RemoteProcedureResponse<bool>(req.MethodName, false);
                    PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(streamNotFoundResponse, traceLogger);
                    MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                    interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                    interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                    return postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).AsTask();
                }

                // We have to queue the work of closing the stream into the regular file operations queue,
                // otherwise we might close the stream while e.g. a speculative read is still in progress
                streamContainer.OperationPipeline.Send(new FileStreamSerializedOperation()
                {
                    MethodName = req.MethodName,
                    ReplyMailbox = originalMessage.MailboxId,
                    ReplyToId = originalMessage.MessageId,
                });

                return DurandalTaskExtensions.NoOpTask;
            }
            else
            {
                return DurandalTaskExtensions.NoOpTask;
            }
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
                foreach (var fileStream in _openStreams)
                {
                    try
                    {
                        fileStream.Value?.OperationPipeline?.Send(new FileStreamSerializedOperation()
                        {
                            MethodName = "DISPOSE"
                        });

                        fileStream.Value?.StreamProcessingTask?.AwaitWithTimeout(10000);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e);
                    }
                }
            }
        }

        private static FileOpenMode Convert(RemoteFileStreamOpenMode mode)
        {
            switch (mode)
            {
                case RemoteFileStreamOpenMode.CreateNew:
                    return FileOpenMode.CreateNew;
                case RemoteFileStreamOpenMode.Create:
                    return FileOpenMode.Create;
                case RemoteFileStreamOpenMode.Open:
                    return FileOpenMode.Open;
                case RemoteFileStreamOpenMode.OpenOrCreate:
                    return FileOpenMode.OpenOrCreate;
                default:
                    throw new Exception("Unknown FileOpenMode");
            }
        }

        private static FileAccessMode Convert(RemoteFileStreamAccessMode mode)
        {
            switch (mode)
            {
                case RemoteFileStreamAccessMode.Read:
                    return FileAccessMode.Read;
                case RemoteFileStreamAccessMode.Write:
                    return FileAccessMode.Write;
                case RemoteFileStreamAccessMode.ReadWrite:
                    return FileAccessMode.ReadWrite;
                default:
                    throw new Exception("Unknown FileAccessMode");
            }
        }

        private static FileShareMode Convert(RemoteFileStreamShareMode mode)
        {
            FileShareMode returnVal = FileShareMode.None;
            if (mode.HasFlag(RemoteFileStreamShareMode.Read))
            {
                returnVal |= FileShareMode.Read;
            }
            if (mode.HasFlag(RemoteFileStreamShareMode.Write))
            {
                returnVal |= FileShareMode.Write;
            }
            if (mode.HasFlag(RemoteFileStreamShareMode.Delete))
            {
                returnVal |= FileShareMode.Delete;
            }

            return returnVal;
        }

        /// <summary>
        /// Used to queue operations to a single file stream's processing queue
        /// </summary>
        private class FileStreamSerializedOperation
        {
            public string MethodName;
            public MailboxId ReplyMailbox;
            public uint ReplyToId;
            public long Position;
            public int Length;
            public ArraySegment<byte> Data;
        }

        /// <summary>
        /// Used to track locally opened file streams that are using the RemotedFileStream protocol.
        /// </summary>
        private class FileStreamContainer
        {
            /// <summary>
            /// The unique ID of the stream. Multiple file streams may be opened by the client at the same time.
            /// </summary>
            public string StreamId { get; set; }

            /// <summary>
            /// The local path of the file stream being opened.
            /// </summary>
            public VirtualPath FilePath { get; set; }

            /// <summary>
            /// The actual file stream.
            /// </summary>
            public NonRealTimeStream FileStream { get; set; }

            /// <summary>
            /// Marks when this stream was last touched, which allows us to enforce timeouts.
            /// </summary>
            public DateTimeOffset LastTouched { get; set; }

            /// <summary>
            /// A long-running task that processed all data request made to this stream.
            /// Finishes running after the file stream is disposed.
            /// </summary>
            public Task StreamProcessingTask { get; set; }

            /// <summary>
            /// A queue of operations to send to the file stream handler task.
            /// </summary>
            public BufferedChannel<FileStreamSerializedOperation> OperationPipeline { get; }

            public FileStreamContainer()
            {
                OperationPipeline = new BufferedChannel<FileStreamSerializedOperation>();
            }
        }
    }
}
