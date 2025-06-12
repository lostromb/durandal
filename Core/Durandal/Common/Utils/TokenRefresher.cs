using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// This class manages a service token which has a fixed lifetime and which requires constant refreshing in the background to maintain.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TokenRefresher<T> : IDisposable
    {
        /// <summary>
        /// This backoff time is used when the server explicitly rejects our token. It backs off at a more aggressive rate,
        /// assuming that if this error persists there's nothing we can really do about it.
        /// </summary>
        private static readonly TimeSpan DefaultExplicitDenyBackoffTime = TimeSpan.FromMinutes(1);

        /// <summary>
        /// If the remote service has explicitly rejected our credentials multiple times in a row, this is the maximum amount of time we will backoff before retrying again.
        /// </summary>
        private static readonly TimeSpan MaxExplicitDenyBackoffTime = TimeSpan.FromHours(1);

        /// <summary>
        /// This backoff time is used when refresh fails for some unknown reason (usually network connectivity).
        /// It has a fairly quick retry time because we assume our credentials are still valid
        /// </summary>
        private static readonly TimeSpan DefaultUnknownFailureBackoffTime = TimeSpan.FromSeconds(15);
        
        private readonly CancellationTokenSource _refreshCancelizer = new CancellationTokenSource();
        private readonly ManualResetEventAsync _tokenReadySignal = new ManualResetEventAsync();
        private readonly ManualResetEventSlim _refreshTaskFinishedSignal = new ManualResetEventSlim();
        private readonly RefreshAction _refreshDelegate;
        private readonly ILogger _logger;
        private readonly Task _refreshTask;
        private readonly TimeSpan _defaultBackoffTime;
        private int _disposed = 0;

        private TokenRefreshResult<T> _lastRefreshResult;
        private DateTimeOffset _nextTokenRefresh;
        private DateTimeOffset? _tokenValidUntil;
        private DateTimeOffset? _tokenIssueTime;
        private TimeSpan _explicitDenyBackoffTime = DefaultExplicitDenyBackoffTime;

        /// <summary>
        /// Delegate that is used to implement the logic of actually acquiring a token
        /// </summary>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public delegate Task<TokenRefreshResult<T>> RefreshAction(CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Creates a token refresher that will continuously attempt to maintain a valid token in the background
        /// </summary>
        /// <param name="logger">A service logger</param>
        /// <param name="action">The delegate action which actually fetches the token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="defaultBackoffTime"></param>
        public TokenRefresher(ILogger logger, RefreshAction action, IRealTimeProvider realTime, TimeSpan? defaultBackoffTime = null)
        {
            _refreshDelegate = action;
            _logger = logger;
            _nextTokenRefresh = realTime.Time;
            _tokenValidUntil = null;
            _tokenIssueTime = null;
            _defaultBackoffTime = defaultBackoffTime.GetValueOrDefault(DefaultUnknownFailureBackoffTime);

            // Start the refresh thread right now
            IRealTimeProvider threadLocalTime = realTime.Fork("TokenRefresherThread");
            _refreshTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(() => RunRefreshTask(threadLocalTime));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        ~TokenRefresher()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the time that the token was last issued
        /// </summary>
        public DateTimeOffset? TokenIssueTime
        {
            get
            {
                return _tokenIssueTime;
            }
        }

        /// <summary>
        /// Gets the time that the current token will expire
        /// </summary>
        public DateTimeOffset? TokenExpireTime
        {
            get
            {
                return _tokenValidUntil;
            }
        }

        /// <summary>
        /// Gets the current token, if available.
        /// </summary>
        public async Task<T> GetToken(ILogger traceLogger, IRealTimeProvider realTime, TimeSpan maxSpinTime = default(TimeSpan))
        {
            TokenRefreshResult<T> result = null;

            if (maxSpinTime != default(TimeSpan))
            {
                // If the token is not currently valid, wait for its status to be updated
                // bugbug: this is not quite safe for non-realtime because we consume the full timeout even if
                // the task gets cancelled halfway through by the token being ready
                Task realTimeTask = realTime.WaitAsync(maxSpinTime, _refreshCancelizer.Token);
                Task tokenReadyTask = _tokenReadySignal.WaitAsync(_refreshCancelizer.Token);
                await Task.WhenAny(realTimeTask, tokenReadyTask).ConfigureAwait(false);
            }

            result = _lastRefreshResult;

            // Ensure that the token which we return is valid
            if (_tokenValidUntil.HasValue && _tokenValidUntil.Value < realTime.Time)
            {
                _lastRefreshResult = null;
                _tokenIssueTime = null;
                _tokenValidUntil = null;
                result = null;
                _tokenReadySignal.Reset();
            }

            if (result == null)
            {
                return default(T);
            }

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                traceLogger.Log(result.ErrorMessage, LogLevel.Err);
            }

            return result.Token;
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

            _refreshCancelizer.Cancel();
            _refreshTaskFinishedSignal.Wait(1000);
            
            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);
            
            if (disposing)
            {
                _refreshCancelizer.Dispose();
                _refreshTaskFinishedSignal.Dispose();
            }
        }

        private async Task RunRefreshTask(IRealTimeProvider threadLocalTime)
        {
            try
            {
                while (!_refreshCancelizer.Token.IsCancellationRequested)
                {
                    if (_nextTokenRefresh > threadLocalTime.Time)
                    {
                        // Wait until the next refresh time
                        TimeSpan timeToWait = _nextTokenRefresh - threadLocalTime.Time;
                        await threadLocalTime.WaitAsync(timeToWait, _refreshCancelizer.Token).ConfigureAwait(false);
                    }

                    if (_refreshCancelizer.IsCancellationRequested)
                    {
                        continue;
                    }

                    TokenRefreshResult<T> result;

                    try
                    {
                        result = await _refreshDelegate(_refreshCancelizer.Token, threadLocalTime).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // this normally happens if the program is aborting or something
                        result = new TokenRefreshResult<T>(false, "The token refresher is being shut down");
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                        result = new TokenRefreshResult<T>(false, e.GetDetailedMessage());
                    }
                    
                    if (_refreshCancelizer.IsCancellationRequested)
                    {
                        _tokenReadySignal.Set();
                        continue;
                    }

                    if (result.Success)
                    {
                        _lastRefreshResult = result;
                        _explicitDenyBackoffTime = DefaultExplicitDenyBackoffTime; // reset exponential backoff amount on success
                        _tokenIssueTime = threadLocalTime.Time;
                        _nextTokenRefresh = threadLocalTime.Time.AddTicks((long)((double)result.TokenLifetime.Value.Ticks * 0.9));
                        _tokenValidUntil = threadLocalTime.Time.Add(result.TokenLifetime.Value);
                    }
                    else if (result.SuggestedBackoffTime.HasValue)
                    {
                        // Typically in this case we've been throttled by HTTP 429 or equivalent. Cool off for the suggested amount of time
                        _nextTokenRefresh = threadLocalTime.Time.Add(result.SuggestedBackoffTime.Value);
                    }
                    else if (result.ExplicitlyDenied)
                    {
                        // If we've received an unauthorized response from the server with no suggested retry time, back off at an exponential rate
                        _nextTokenRefresh = threadLocalTime.Time.Add(_explicitDenyBackoffTime);
                        _explicitDenyBackoffTime = new TimeSpan(Math.Min(MaxExplicitDenyBackoffTime.Ticks, _explicitDenyBackoffTime.Ticks * 2));
                    }
                    else
                    {
                        _nextTokenRefresh = threadLocalTime.Time.Add(_defaultBackoffTime);
                    }

                    _tokenReadySignal.Set();

                    // Invalidate old tokens
                    if (_tokenValidUntil.HasValue && _tokenValidUntil.Value < threadLocalTime.Time)
                    {
                        _tokenReadySignal.Reset();
                        _lastRefreshResult = null;
                        _tokenIssueTime = null;
                        _tokenValidUntil = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Token refresh task has been cancelled");
            }
            finally
            {
                threadLocalTime.Merge();
            }
        }
    }
}
