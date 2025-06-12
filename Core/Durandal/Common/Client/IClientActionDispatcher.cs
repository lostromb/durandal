using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Client
{
    /// <summary>
    /// An interface for an object which can accept client action strings from a dialog response
    /// and dispatch them on the client.
    /// </summary>
    public interface IClientActionDispatcher : IDisposable
    {
        /// <summary>
        /// Interprets the client action string from the server.
        /// </summary>
        /// <param name="actionString">The raw action string that dialog sent to the client</param>
        /// <param name="source">The client currently processing actions</param>
        /// <param name="queryLogger">A query logger</param>
        /// <param name="cancelToken">A token to cancel any running client actions. This should be canceled every time the user start some new interaction with the client (and thus invalidates any currently running actions).</param>
        /// <param name="realTime">Wallclock time, provided by the client core</param>
        Task InterpretClientAction(string actionString, ClientCore source, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Gets the set of action strings supported by this handler
        /// </summary>
        /// <returns></returns>
        HashSet<string> GetSupportedClientActions();
    }
}
