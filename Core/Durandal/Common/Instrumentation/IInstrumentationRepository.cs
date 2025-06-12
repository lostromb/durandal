using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Durandal.Common.Instrumentation;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Represents a repository which manages data across two data tables: LOGS and INSTRUMENTATION.
    /// LOGS contains the set of raw logs coming through aggregation from clients + services. There are multiple log entries for each trace ID
    /// INSTRUMENTATION contains a set of unified traces for which there is at most 1 entry for each trace ID. Instrumentation events also intrinsically contain all of the log events for that trace.
    /// The primary purpose of the instrumentation functions is to aggregate data from the LOGS table and put them into INSTRUMENTATION events so that they can be used for debugging and aggregate metrics down the pipe.
    /// TODO: In the future we want to hide the actual intrinsics of this method so that it only exposes GetUnifiedTrace(), WriteUnifiedTrace(), AggregateLogs(), and MarkAsImported()
    /// </summary>
    public interface IInstrumentationRepository
    {
        /// <summary>
        /// Deletes all logs from the logs table that have the specified trace ID
        /// </summary>
        /// <param name="traceId"></param>
        Task DeleteLogs(Guid traceId);

        /// <summary>
        /// Gets trace IDs for events that have data present inside the instrumentation table and where TraceEnd falls within the specified time range
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        Task<ISet<Guid>> GetProcessedTraceIds(DateTimeOffset startTime, DateTimeOffset endTime);

        /// <summary>
        /// Pulls a complete trace for a single trace ID using only information from the instrumentation table
        /// </summary>
        /// <param name="traceId"></param>
        /// <param name="piiDecrypter"></param>
        /// <returns></returns>
        Task<UnifiedTrace> GetTraceData(Guid traceId, IStringDecrypterPii piiDecrypter);

        /// <summary>
        /// Pulls complete traces for a set of trace IDs using only information from the instrumentation table
        /// </summary>
        /// <param name="traceIds"></param>
        /// <param name="piiDecrypter"></param>
        /// <returns></returns>
        Task<IList<UnifiedTrace>> GetTraceData(IEnumerable<Guid> traceIds, IStringDecrypterPii piiDecrypter);

        /// <summary>
        /// Gets a set of trace IDs in the instrumentation table which have not been imported to AppInsights instrumentation.
        /// Typical implementations will also impose a time delay - specifically, to make sure we have all of the relevant logs aggregated
        /// before we upload the final "baked" instrumentation event, this method will only return trace IDs whose timestamp is over 10 minutes old
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        Task<ISet<Guid>> GetTraceIdsNotImportedToAppInsights(int limit = 100);

        /// <summary>
        /// Gets a set of trace IDs for events that have data inside the logs table. The order is arbitrary, but the set of returned IDs is guaranteed to be unique
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        Task<ISet<Guid>> GetUnprocessedTraceIds(int limit = 100);

        /// <summary>
        /// Marks the set of given instrumentation entries (keyed by trace ID) as having been successfully uploaded to app insights
        /// </summary>
        /// <param name="traceIds"></param>
        Task MarkTraceIdsAsImportedToAppInsights(IEnumerable<Guid> traceIds);

        /// <summary>
        /// Writes a single unified trace to the instrumentation table, overwriting any existing record
        /// </summary>
        /// <param name="traceInfo"></param>
        /// <returns></returns>
        Task<bool> WriteTraceData(UnifiedTrace traceInfo);
    }
}