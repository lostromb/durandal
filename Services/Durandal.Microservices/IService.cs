using Durandal.Common.Config;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DurandalServices
{
    public interface IService
    {
        string ServiceName { get; }
        IConfiguration ServiceConfig { get; }
        Task Start(CancellationToken cancelToken, IRealTimeProvider realTime);
        Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime);
        bool IsRunning();
    }
}
