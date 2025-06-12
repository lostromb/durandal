using System.Collections.Generic;
using Durandal.Common.Logger;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Common.Client.Actions
{
    /// <summary>
    /// Specifies a handler object which can handle one or more Json-encoded client action objects.
    /// </summary>
    public interface IJsonClientActionHandler
    {
        /// <summary>
        /// Handles an incoming Json client action.
        /// </summary>
        /// <param name="actionName">The name of the action (given as the "Name" parameter in json)</param>
        /// <param name="action">The JSON object itself</param>
        /// <param name="queryLogger">A query logger</param>
        /// <param name="source">The client which is dispatching this action</param>
        /// <param name="cancelToken">A cancellation token which is canceled every time the user makes some tangible
        /// <param name="realTime">Wallclock time provided by the client framework</param>
        /// action on the client, typically by issuing another query, or it could be something like opening the settings menu or something.</param>
        Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Returns the set of action strings supported by this handler
        /// </summary>
        /// <returns></returns>
        ISet<string> GetSupportedClientActions();
    }
}
