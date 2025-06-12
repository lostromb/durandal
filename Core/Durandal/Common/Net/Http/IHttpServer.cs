namespace Durandal.Common.Net.Http
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// An interface that defines a server which can accept HTTP requests
    /// </summary>
    public interface IHttpServer : IServer
    {
        /// <summary>
        /// Registers a "subclass" of this class, which contains the handler for the incoming socket event.
        /// This design is kind of an inverse of classic inheritance, because we want the subclass of this server (something like DialogHTTP) to be common,
        /// but the superclass (this class) to be configurable. What we basically do is manual aggregate inheritance via a delegate interface.
        /// </summary>
        /// <param name="subclass"></param>
        void RegisterSubclass(IHttpServerDelegate subclass);

        /// <summary>
        /// Gets a uri which can be used to access the service that is running on the local machine (i.e. the loopback path)
        /// </summary>
        Uri LocalAccessUri { get; }
    }
}
