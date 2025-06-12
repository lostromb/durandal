using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.ServiceMgmt
{
    /// <summary>
    /// Use this as a type when an interface requires one but you don't care what it is.
    /// </summary>
    public struct StubType
    {
        public static readonly StubType Empty = new StubType();
    }
}
