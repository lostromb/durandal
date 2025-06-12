using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.MySql
{
    public class MySqlConversationStateCache : MySqlDataSource, IConversationStateCache
    {
        private readonly TimeSpan MAX_TIME_TO_GET_CONNECTION = TimeSpan.FromMilliseconds(5000);
        private const string TABLE_NAME = "sessions";
        private const int DEFAULT_FETCH_TIMEOUT = 5000;
        private readonly WeakPointer<MySqlConnectionPool> _connectionPool;
        private readonly bool _useCoproc;
        private readonly ILogger _logger;
        private readonly IByteConverter<SerializedConversationStateStack> _byteConverter;
        private int _disposed = 0;

        public MySqlConversationStateCache(
            MySqlConnectionPool connectionPool,
            IByteConverter<SerializedConversationStateStack> serializer,
            ILogger logger,
            bool useCoproc = true)
                : base(connectionPool, logger)
        {
            _logger = logger;
            _connectionPool = new WeakPointer<MySqlConnectionPool>(connectionPool);
            _byteConverter = serializer;
            _useCoproc = useCoproc;

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MySqlConversationStateCache()
        {
            Dispose(false);
        }
#endif

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
            }
        }

        /// <summary>
        /// Sets a new conversation state
        /// </summary>
        /// <param name="userId">The User ID (not the client ID) for the user making the current request</param>
        /// <param name="clientId">The client id of the _device_ making the request. This should only be used for client-specific states! Otherwise it should be null</param>
        /// <param name="newState">The updated conversation state</param>
        /// <param name="queryLogger">A logger for this particular query</param>
        /// <param name="fireAndForget">If true, return immediately</param>
        /// <returns>True</returns>
        public async Task<bool> SetClientSpecificState(string userId, string clientId, Stack<ConversationState> newState, ILogger queryLogger, bool fireAndForget)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("Client ID");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("User ID");
            }

            SerializedConversationStateStack convertedStack = new SerializedConversationStateStack()
            {
                Stack = ConversationState.StackToList(ConversationState.ConvertStack(newState))
            };

            queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "SQL session store is writing client-specific state for {0}:{1}", userId, clientId);
            
            if (fireAndForget)
            {
                SetClientSpecificStateInternal(userId, clientId, convertedStack, queryLogger, true).Forget(queryLogger);
                return true;
            }
            else
            {
                return await SetClientSpecificStateInternal(userId, clientId, convertedStack, queryLogger, false).ConfigureAwait(false);
            }
        }

        private async Task<bool> SetClientSpecificStateInternal(string userId, string clientId, SerializedConversationStateStack newState, ILogger queryLogger, bool fireAndForget)
        {
            if (fireAndForget)
            {
                // Tell the caller thread to go do something else important and let this queue to the thread pool in the background
                await Task.Yield();
            }

            ValueStopwatch sessionStoreTimer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    byte[] blob = _byteConverter.Encode(newState);
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("INSERT INTO sessions (UserId, ClientId, Value, LastUpdateTime) VALUES(\'{0}\', \'{1}\', @BLOBDATA, @TIME) " +
                        "ON DUPLICATE KEY UPDATE Value=VALUES(Value),LastUpdateTime=VALUES(LastUpdateTime);",
                        userId,
                        string.IsNullOrEmpty(clientId) ? string.Empty : clientId);
                    command.Parameters.Add("BLOBDATA", MySqlDbType.MediumBlob).Value = blob;
                    command.Parameters.Add("TIME", MySqlDbType.DateTime).Value = DateTimeOffset.UtcNow.UtcDateTime;
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    sessionStoreTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_SessionWriteClientState, ref sessionStoreTimer), LogLevel.Ins);
                    queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "Updated conversation state {0}:{1}", userId, clientId);
                }
                catch (Exception e)
                {
                    queryLogger.Log("Caught an exception while updating SQL client-specific session state", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }

                return true;
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to write client session", LogLevel.Err);
                return false;
            }
        }

        /// <summary>
        /// Sets a new conversation state
        /// </summary>
        /// <param name="userId">The User ID (not the client ID) for the user making the current request</param>
        /// <param name="newState">The updated conversation state</param>
        /// <param name="queryLogger">A logger for this particular query</param>
        /// <param name="fireAndForget">If true, return immediately</param>
        /// <returns>True</returns>
        public async Task<bool> SetRoamingState(string userId, Stack<ConversationState> newState, ILogger queryLogger, bool fireAndForget)
        {
            SerializedConversationStateStack convertedStack = new SerializedConversationStateStack()
            {
                Stack = ConversationState.StackToList(ConversationState.ConvertStack(newState))
            };

            queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "SQL session store is writing roaming state for {0}", userId);

            if (fireAndForget)
            {
                SetRoamingStateInternal(userId, convertedStack, queryLogger, true).Forget(queryLogger);
                return true;
            }
            else
            {
                return await SetRoamingStateInternal(userId, convertedStack, queryLogger, false);
            }
        }

        private async Task<bool> SetRoamingStateInternal(string userId, SerializedConversationStateStack newState, ILogger queryLogger, bool fireAndForget)
        {
            if (fireAndForget)
            {
                // Tell the caller thread to go do something else important and let this queue to the thread pool in the background
                await Task.Yield();
            }

            ValueStopwatch sessionStoreTimer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    byte[] blob = _byteConverter.Encode(newState);
                    if (blob.Length == 0)
                    {
                        queryLogger.Log("Serialized conversation state has zero length; this probably means it's missing required values", LogLevel.Wrn);
                    }
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("INSERT INTO sessions (UserId, ClientId, Value, LastUpdateTime) VALUES(\'{0}\', \'\', @BLOBDATA, @TIME) " +
                        "ON DUPLICATE KEY UPDATE Value=VALUES(Value),LastUpdateTime=VALUES(LastUpdateTime);",
                        userId);
                    command.Parameters.Add("BLOBDATA", MySqlDbType.MediumBlob).Value = blob;
                    command.Parameters.Add("TIME", MySqlDbType.DateTime).Value = DateTimeOffset.UtcNow.UtcDateTime;
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    sessionStoreTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_SessionWriteRoamingState, ref sessionStoreTimer), LogLevel.Ins);
                    queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "Updated conversation state {0}", userId);
                }
                catch (Exception e)
                {
                    queryLogger.Log("Caught an exception while updating SQL session state", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to write roaming session", LogLevel.Err);
            }

            return true;
        }

        /// <summary>
        /// Attempt to retrieve the conversation state associated with the given user ID. If it is not found, this method will STORE a newly created
        /// conversation state and then return it along with FALSE.
        /// </summary>
        /// <param name="userId">The User ID (not the client ID) for the user making the current request</param>
        /// <param name="clientId">The client id of the _device_ making the request. If a client-specific state exists it will be retrieved, otherwise the cross-client state is used</param>
        /// <param name="queryLogger">A logger for this particular query</param>
        /// <param name="realTime">Real time</param>
        /// <returns>True if an existing state was found in the store, false if a new one was created</returns>
        public Task<RetrieveResult<Stack<ConversationState>>> TryRetrieveState(string userId, string clientId, ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (_useCoproc)
            {
                return TryRetrieveStateWithCoproc(userId, clientId, queryLogger);
            }
            else
            {
                return TryRetrieveStateWithoutCoproc(userId, clientId, queryLogger);
            }
        }
        
        public async Task<bool> ClearRoamingState(string userId, ILogger queryLogger, bool fireAndForget)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("User ID");
            }

            queryLogger.Log("SQL session store is clearing state for " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);

            if (fireAndForget)
            {
                ClearRoamingStateInternal(userId, queryLogger, true).Forget(queryLogger);
                return true;
            }
            else
            {
                return await ClearRoamingStateInternal(userId, queryLogger, false).ConfigureAwait(false);
            }
        }

        private async Task<bool> ClearRoamingStateInternal(string userId, ILogger queryLogger, bool fireAndForget)
        {
            if (fireAndForget)
            {
                // Tell the caller thread to go do something else important and let this queue to the thread pool in the background
                await Task.Yield();
            }

            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("DELETE FROM sessions WHERE UserId = \'{0}\' AND ClientId = \'\';", userId);
                    queryLogger.Log("Clearing roaming conversation state for user " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);

                    int rows = await command.ExecuteNonQueryAsync();
                    return rows > 0;
                }
                catch (Exception e)
                {
                    queryLogger.Log("Caught an exception while clearing SQL session state", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to clear roaming session", LogLevel.Err);
            }

            return false;
        }

        public async Task<bool> ClearClientSpecificState(string userId, string clientId, ILogger queryLogger, bool fireAndForget)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("User ID");
            }
            else if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("Client ID");
            }

            queryLogger.Log("SQL session store is clearing state for " + userId + ":" + clientId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);

            if (fireAndForget)
            {
                ClearClientSpecificStateInternal(userId, clientId, queryLogger, true).Forget(queryLogger);
                return true;
            }
            else
            {
                return await ClearClientSpecificStateInternal(userId, clientId, queryLogger, false).ConfigureAwait(false);
            }
        }

        private async Task<bool> ClearClientSpecificStateInternal(string userId, string clientId, ILogger queryLogger, bool fireAndForget)
        {
            if (fireAndForget)
            {
                // Tell the caller thread to go do something else important and let this queue to the thread pool in the background
                await Task.Yield();
            }

            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("DELETE FROM sessions WHERE UserId = \'{0}\' AND ClientId = \'{1}\';", userId, clientId);
                    queryLogger.Log("Clearing client-specific conversation state " + userId + ":" + clientId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);

                    int rows = await command.ExecuteNonQueryAsync();
                    return rows > 0;
                }
                catch (Exception e)
                {
                    queryLogger.Log("Caught an exception while clearing SQL session state", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to clear client session", LogLevel.Err);
            }

            return false;
        }

        public async Task<bool> ClearBothStates(string userId, string clientId, ILogger queryLogger, bool fireAndForget)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("User ID");
            }
            else if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("Client ID");
            }

            queryLogger.Log("SQL session store is clearing state for " + userId + ":" + clientId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);

            if (fireAndForget)
            {
                ClearBothStatesInternal(userId, clientId, queryLogger, true).Forget(queryLogger);
                return true;
            }
            else
            {
                return await ClearBothStatesInternal(userId, clientId, queryLogger, false).ConfigureAwait(false);
            }
        }

        private async Task<bool> ClearBothStatesInternal(string userId, string clientId, ILogger queryLogger, bool fireAndForget)
        {
            if (fireAndForget)
            {
                // Tell the caller thread to go do something else important and let this queue to the thread pool in the background
                await Task.Yield();
            }

            ValueStopwatch sessionClearTimer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = string.Format("DELETE FROM sessions WHERE UserId = \'{0}\' AND (ClientId = \'{1}\' OR ClientId = \'\');", userId, clientId);
                    queryLogger.Log("Clearing both conversation states " + userId + ":" + clientId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                    int rows = await command.ExecuteNonQueryAsync();
                    sessionClearTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_SessionClear, ref sessionClearTimer), LogLevel.Ins);

                    return rows > 0;
                }
                catch (Exception e)
                {
                    queryLogger.Log("Caught an exception while clearing SQL session state", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to clear sessions", LogLevel.Err);
            }

            return false;
        }

        private async Task<RetrieveResult<Stack<ConversationState>>> TryRetrieveStateWithCoproc(string userId, string clientId, ILogger queryLogger)
        {
            queryLogger.Log("SQL session store is reading state for " + userId + ":" + clientId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);

            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "get_session";
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = DEFAULT_FETCH_TIMEOUT;
                    MySqlParameter userIdParam = new MySqlParameter("@p_userId", MySqlDbType.VarChar, 48)
                    {
                        Direction = ParameterDirection.Input,
                        Value = userId
                    };
                    MySqlParameter clientIdParam = new MySqlParameter("@p_clientId", MySqlDbType.VarChar, 48)
                    {
                        Direction = ParameterDirection.Input,
                        Value = string.IsNullOrEmpty(clientId) ? string.Empty : clientId
                    };
                    MySqlParameter returnValParam = new MySqlParameter("@p_session", MySqlDbType.MediumBlob)
                    {
                        Direction = ParameterDirection.Output
                    };
                    MySqlParameter isRoamingParam = new MySqlParameter("@p_isRoaming", MySqlDbType.Byte)
                    {
                        Direction = ParameterDirection.Output
                    };
                    command.Parameters.Add(userIdParam);
                    command.Parameters.Add(clientIdParam);
                    command.Parameters.Add(returnValParam);
                    command.Parameters.Add(isRoamingParam);
                    await command.ExecuteScalarAsync();

                    if (returnValParam.Value != null && returnValParam.Value is byte[])
                    {
                        if (((sbyte)isRoamingParam.Value) != 0)
                        {
                            queryLogger.Log("Using roaming conversation state " + userId, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                        }
                        else
                        {
                            queryLogger.Log("Using client-specific state " + userId + ":" + clientId, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                        }

                        byte[] readBlob = (byte[])returnValParam.Value;
                        SerializedConversationStateStack state = _byteConverter.Decode(readBlob, 0, readBlob.Length);
                        if (state != null)
                        {
                            Stack<SerializedConversationState> existingStack = ConversationState.ListToStack(state.Stack);

                            while (existingStack.Count != 0)
                            {
                                SerializedConversationState topState = existingStack.Peek();
                                DateTimeOffset expireTime = new DateTimeOffset(topState.ConversationExpireTime, TimeSpan.Zero);
                                DateTimeOffset currentTime = DateTimeOffset.UtcNow;
                                if (currentTime > expireTime)
                                {
                                    queryLogger.Log("State for " + topState.CurrentPluginDomain + " expired, "
                                        + currentTime.ToString() + " > " + expireTime.ToString(), LogLevel.Vrb);
                                    existingStack.Pop();
                                }
                                else
                                {
                                    // A state already exists and has not entirely expired, so use it
                                    queryLogger.Log("Got conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                                    // Instrument its size
                                    queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Store_SessionRead, readBlob.Length), LogLevel.Ins);
                                    Stack<ConversationState> returnVal = ConversationState.ConvertStack(existingStack);
                                    timer.Stop();
                                    return new RetrieveResult<Stack<ConversationState>>(returnVal, timer.ElapsedMillisecondsPrecise(), true);
                                }
                            }

                            // State exists but has expired, so create a new one and remove the one in the store
                            //queryLogger.Log("Client " + userId + " attempted to retrieve an expired conversation state", LogLevel.Wrn);

                            //_conversationStateLock.EnterWriteLock();
                            //if (_roamingStates.ContainsKey(userId))
                            //{
                            //    _roamingStates.Remove(userId);
                            //}
                            //if (_clientSpecificStates.ContainsKey(clientSpecificKey))
                            //{
                            //    _clientSpecificStates.Remove(clientSpecificKey);
                            //}
                            //_conversationStateLock.ExitWriteLock();

                            queryLogger.Log("Building new conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                            //returnVal.Result = new Stack<ConversationState>();
                            //returnVal.Success = false;
                        }
                    }
                    else
                    {
                        // No state at all; create empty
                        queryLogger.Log("Building new conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                        //returnVal.Result = new Stack<ConversationState>();
                        //returnVal.Success = false;
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log("Caught an exception while reading SQL session state", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to read sessions", LogLevel.Err);
            }

            timer.Stop();
            return new RetrieveResult<Stack<ConversationState>>(null, timer.ElapsedMillisecondsPrecise(), false);
        }

        /// <summary>
        /// Experimental code that ignores the server-side coproc, instead opting to dump both sessions at once and then sort them out on client
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="clientId"></param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        private async Task<RetrieveResult<Stack<ConversationState>>> TryRetrieveStateWithoutCoproc(string userId, string clientId, ILogger queryLogger)
        {
            queryLogger.Log("SQL session store is reading state for " + userId + ":" + clientId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);

            ValueStopwatch timer = ValueStopwatch.StartNew();
            RetrieveResult<PooledResource<MySqlConnection>> pooledConnection = await _connectionPool.Value.TryGetConnectionAsync(MAX_TIME_TO_GET_CONNECTION).ConfigureAwait(false);
            if (pooledConnection.Success)
            {
                PooledResource<MySqlConnection> connection = pooledConnection.Result;
                try
                {
                    MySqlCommand command = connection.Value.CreateCommand();
                    command.CommandText = "SELECT ClientId, Value FROM sessions WHERE UserId = @USERID AND (ClientId = @CLIENTID OR ClientId = \'\');";
                    command.CommandTimeout = DEFAULT_FETCH_TIMEOUT;
                    command.Parameters.Add("USERID", MySqlDbType.VarChar).Value = userId;
                    command.Parameters.Add("CLIENTID", MySqlDbType.VarChar).Value = clientId;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        byte[] clientSpecificState = null;
                        byte[] roamingState = null;

                        while (await reader.ReadAsync())
                        {
                            if (string.IsNullOrWhiteSpace(reader.GetString(0)))
                            {
                                roamingState = (byte[])reader.GetValue(1);
                            }
                            else
                            {
                                clientSpecificState = (byte[])reader.GetValue(1);
                            }
                        }

                        byte[] readBlob = clientSpecificState ?? roamingState;

                        if (readBlob != null && readBlob.Length > 0)
                        {
                            SerializedConversationStateStack state = _byteConverter.Decode(readBlob, 0, readBlob.Length);
                            Stack<SerializedConversationState> existingStack = ConversationState.ListToStack(state.Stack);

                            while (existingStack.Count != 0)
                            {
                                SerializedConversationState topState = existingStack.Peek();
                                DateTimeOffset expireTime = new DateTimeOffset(topState.ConversationExpireTime, TimeSpan.Zero);
                                DateTimeOffset currentTime = DateTimeOffset.UtcNow;
                                if (currentTime > expireTime)
                                {
                                    queryLogger.Log("State for " + topState.CurrentPluginDomain + " expired, "
                                        + currentTime.ToString() + " > " + expireTime.ToString(), LogLevel.Vrb);
                                    existingStack.Pop();
                                }
                                else
                                {
                                    // A state already exists and has not entirely expired, so use it
                                    queryLogger.Log("Got conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                                    // Instrument its size
                                    queryLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Store_SessionRead, readBlob.Length), LogLevel.Ins);
                                    Stack<ConversationState> returnVal = ConversationState.ConvertStack(existingStack);
                                    timer.Stop();
                                    return new RetrieveResult<Stack<ConversationState>>(returnVal, timer.ElapsedMillisecondsPrecise(), true);
                                }
                            }

                            // State exists but has expired, so create a new one
                            queryLogger.Log("Building new conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                        }
                        else
                        {
                            // No state at all; create empty
                            queryLogger.Log("Building new conversation state for client id " + userId, LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                        }
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log("Caught an exception while reading SQL session state", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _connectionPool.Value.ReleaseConnectionAsync(connection).Forget(queryLogger);
                }
            }
            else
            {
                _logger.Log("FAILED to get MySql connection to read sessions", LogLevel.Err);
            }

            timer.Stop();
            return new RetrieveResult<Stack<ConversationState>>(new Stack<ConversationState>(), timer.ElapsedMillisecondsPrecise(), false);
        }

        protected override IEnumerable<MySqlTableDefinition> Tables =>
            new List<MySqlTableDefinition>()
            {
                new MySqlTableDefinition()
                {
                    TableName = TABLE_NAME,
                    CreateStatement = string.Format(
                        "CREATE TABLE {0} (\r\n" +
                        "  `UserId` varchar(255) NOT NULL,\r\n" +
                        "  `ClientId` varchar(255) NOT NULL,\r\n" +
                        "  `Value` mediumblob,\r\n" +
                        "  `LastUpdateTime` datetime DEFAULT NULL,\r\n" +
                        "  UNIQUE KEY `UserId` (`UserId`,`ClientId`),\r\n" +
                        "  KEY `ageIdx` (`LastUpdateTime`)\r\n" +
                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8;\r\n",
                        TABLE_NAME)
                }
            };

        protected override IEnumerable<MySqlProcedureDefinition> Procedures => 
            new List<MySqlProcedureDefinition>()
            {
                new MySqlProcedureDefinition()
                {
                    ProcedureName = "get_session",
                    CreateStatement = string.Format(
                        "CREATE PROCEDURE `get_session`( \r\n" +
                        "IN p_userId VARCHAR(255), \r\n" +
                        "IN p_clientId VARCHAR(255), \r\n" +
                        "OUT p_session MEDIUMBLOB, \r\n" +
                        "OUT p_isRoaming BOOLEAN) \r\n" +
                        "BEGIN \r\n" +
                        "	# First, see what rows exist \r\n" +
                        "    DECLARE v_clientSpecificExists BOOLEAN; \r\n" +
                        "    DECLARE v_roamingExists BOOLEAN; \r\n" +
                        "    SET p_isRoaming = 0; \r\n" +
                        "    SELECT EXISTS (SELECT * FROM sessions WHERE UserId = p_userId AND ClientId = p_clientId AND ClientId != '') INTO v_clientSpecificExists; \r\n" +
                        "    SELECT EXISTS (SELECT * FROM sessions WHERE UserId = p_userId AND ClientId = '') INTO v_roamingExists; \r\n" +
                        "    # Then select the appropriate one \r\n" +
                        "    IF v_clientSpecificExists THEN \r\n" +
                        "		SELECT Value INTO p_session FROM sessions WHERE UserId = p_userId AND ClientId = p_clientId; \r\n" +
                        "	ELSEIF v_roamingExists THEN \r\n" +
                        "		SELECT Value INTO p_session FROM sessions WHERE UserId = p_userId AND ClientId = ''; \r\n" +
                        "        SET p_isRoaming = 1; \r\n" +
                        "	ELSE \r\n" +
                        "		SET p_session = NULL; \r\n" +
                        "    END IF; \r\n" +
                        "END"
                        )
                }
            };

        protected override IEnumerable<MySqlEventDefinition> Events =>
            new List<MySqlEventDefinition>()
            {
                new MySqlEventDefinition()
                {
                    EventName = "session_cleanup",
                    CreateStatement = string.Format(
                        "CREATE EVENT `session_cleanup`\r\n" +
                        "ON SCHEDULE EVERY 1 HOUR DO\r\n" +
                        "DELETE FROM {0} WHERE LastUpdateTime < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 7 DAY)",
                        TABLE_NAME)
                }
            };
    }
}
