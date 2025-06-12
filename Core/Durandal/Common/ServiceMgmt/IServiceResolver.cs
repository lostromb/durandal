using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.ServiceMgmt
{
    public interface IServiceResolver<T> : IDisposable
    {
        T ResolveService();
    }
}
