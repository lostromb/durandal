using System;

namespace Durandal.Common.Net
{
    public interface ISocketServer : IServer
    {
        /// <summary>
        /// Registers a "subclass" of this class, which contains the handler for the incoming socket event.
        /// This design is kind of an inverse of classic inheritance, because we want the subclass of this server (usually HttpServer) to be common,
        /// but the superclass (this class) to be configurable. What we basically do is manual aggregate inheritance via a delegate interface.
        /// </summary>
        /// <param name="subclass"></param>
        void RegisterSubclass(ISocketServerDelegate subclass);
    }
}