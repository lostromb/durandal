using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// Defines the interface for a container guest (usually remoted across an AppDomain) 
    /// </summary>
    public interface IContainerGuest
    {
        void Initialize(string serializedJsonInitializationParameters);
        void Shutdown();
    }
}
