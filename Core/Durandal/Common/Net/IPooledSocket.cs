using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public interface IPooledSocket : ISocket
    {
        bool Healthy { get; }
        void MakeReadyForReuse();
    }
}
