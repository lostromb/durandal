using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Net.Http;
using Durandal.Common.File;
using Durandal.API;
using Durandal.Common.IO;
using Durandal.Common.Time;

namespace Durandal.Common.Instrumentation
{
    public class RemoteInstrumentationLogger : LoggerBase
    {
        public static readonly string ENCODING_SCHEME_BINARY = "binary";
        public static readonly string ENCODING_SCHEME_BOND = "bond";

        /// <summary>
        /// Instantiates a new remote instrumentation logger, which sends all logs to an aggregator server 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="instrumentationSerializer"></param>
        /// <param name="realTime"></param>
        /// <param name="streamName"></param>
        /// <param name="tracesOnly"></param>
        /// <param name="bootstrapLogger"></param>
        /// <param name="metrics"></param>
        /// <param name="dimensions"></param>
        /// <param name="componentName"></param>
        /// <param name="validLogLevels"></param>
        /// <param name="maxLogLevels"></param>
        /// <param name="maxPrivacyClasses"></param>
        /// <param name="defaultPrivacyClass"></param>
        /// <param name="backgroundLogThreadPool"></param>
        public RemoteInstrumentationLogger(
            IHttpClient client,
            IByteConverter<InstrumentationEventList> instrumentationSerializer,
            IRealTimeProvider realTime,
            string streamName,
            bool tracesOnly = false,
            ILogger bootstrapLogger = null,
            IMetricCollector metrics = null,
            DimensionSet dimensions = null,
            string componentName = "Main",
            LogLevel validLogLevels = DEFAULT_LOG_LEVELS,
            LogLevel maxLogLevels = LogLevel.All,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata,
            IThreadPool backgroundLogThreadPool = null)
            : base(new RemoteLoggerCore(client, streamName, tracesOnly, instrumentationSerializer, realTime, bootstrapLogger, metrics, dimensions),
                      new LoggerContext()
                      {
                          ComponentName = componentName,
                          TraceId = null,
                          ValidLogLevels = validLogLevels,
                          MaxLogLevels = maxLogLevels,
                          DefaultPrivacyClass = defaultPrivacyClass,
                          ValidPrivacyClasses = DataPrivacyClassification.All,
                          MaxPrivacyClasses = maxPrivacyClasses,
                          BackgroundLoggingThreadPool = backgroundLogThreadPool
                      })
        {
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core"></param>
        /// <param name="context"></param>
        private RemoteInstrumentationLogger(ILoggerCore core, LoggerContext context)
                : base(core, context)
        {
        }

        protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext context)
        {
            return new RemoteInstrumentationLogger(core, context);
        }

        public string StreamName => ((RemoteLoggerCore)Core).StreamName;

        /// <summary>
        /// This is the context object shared between all clones of the remote logger
        /// </summary>
        private class RemoteLoggerCore : BatchedDataProcessor<PooledLogEvent>, ILoggerCore
        {
            private readonly IHttpClient _client;
            private readonly bool _tracesOnly;
            private readonly IByteConverter<InstrumentationEventList> _instrumentationSerializer;
            private readonly string _encodingScheme;
            private readonly ILogger _bootstrapLogger;

            public RemoteLoggerCore(
                IHttpClient client,
                string streamName,
                bool tracesOnly,
                IByteConverter<InstrumentationEventList> instrumentationSerializer,
                IRealTimeProvider realTime,
                ILogger bootstrapLogger,
                IMetricCollector metrics = null,
                DimensionSet dimensions = null)
                : base(
                    nameof(RemoteInstrumentationLogger),
                    new BatchedDataProcessorConfig()
                    {
                        BatchSize = 100,
                        MaxSimultaneousProcesses = 2,
                        DesiredInterval = TimeSpan.FromSeconds(5),
                        MinimumBackoffTime = TimeSpan.FromSeconds(5),
                        MaximumBackoffTime = TimeSpan.FromSeconds(60),
                        MaxBacklogSize = 10000,
                        AllowDroppedItems = true,
                    },
                    realTime,
                    bootstrapLogger,
                    metrics,
                    dimensions)
            {
                _client = client;
                _bootstrapLogger = bootstrapLogger;
                StreamName = streamName;
                _tracesOnly = tracesOnly;
                _instrumentationSerializer = instrumentationSerializer;
                if (instrumentationSerializer is InstrumentationBlobSerializer)
                {
                    _encodingScheme = ENCODING_SCHEME_BINARY;
                }
                else
                {
                    _encodingScheme = ENCODING_SCHEME_BOND;
                }
            }

            public string StreamName
            {
                get;
            }

            public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                return base.Flush(realTime, blocking ? TimeSpan.FromSeconds(5) : TimeSpan.Zero);
            }
            
            public void LoggerImplementation(PooledLogEvent value)
            {
                // If we are in traces-only mode and there is no traceid, ignore this
                if (_tracesOnly && !value.TraceId.HasValue)
                {
                    value.Dispose();
                    return;
                }
                
                Ingest(value);
            }

            /// <summary>
            /// Compresses log data and sends it along to the server
            /// </summary>
            /// <param name="logs">The logs to send</param>
            /// <param name="realTime">A definition of real time</param>
            /// <returns></returns>
            protected override async ValueTask<bool> Process(ArraySegment<PooledLogEvent> logs, IRealTimeProvider realTime)
            {
                try
                {
                    InstrumentationBlob blob = new InstrumentationBlob();
                    foreach (PooledLogEvent log in logs)
                    {
                        blob.AddEvent(log.ToLogEvent());
                    }

                    //_bootstrapLogger.Log("Processing log events");
                    using (HttpRequest request = HttpRequest.CreateOutgoing("/log", HttpConstants.HTTP_VERB_POST))
                    {
                        request.SetContent(blob.Compress(_instrumentationSerializer), HttpConstants.MIME_TYPE_OCTET_STREAM);
                        request.GetParameters.Add("stream", StreamName);
                        request.GetParameters.Add("format", _encodingScheme);

                        using (HttpResponse r = await _client.SendRequestAsync(request, CancellationToken.None, realTime).ConfigureAwait(false))
                        {
                            try
                            {
                                if (r != null && r.ResponseCode == 200)
                                {
                                    //_bootstrapLogger.Log("Sent " + logs.Length + " events");
                                    // Release all pooled log events now that we're done with them
                                    foreach (PooledLogEvent log in logs)
                                    {
                                        log.Dispose();
                                    }

                                    return true;
                                }
                                else if (r != null && r.ResponseCode != 404 && r.ResponseCode != 419)
                                {
                                    _bootstrapLogger.Log("Remote instrumentation error. Null or error response from server", LogLevel.Wrn);
                                }
                            }
                            finally
                            {
                                if (r != null)
                                {
                                    await r.FinishAsync(CancellationToken.None, realTime).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _bootstrapLogger.Log("Remote instrumentation error", LogLevel.Wrn);
                    _bootstrapLogger.Log(e, LogLevel.Wrn);
                }

                return false;
            }
        }
    }
}
