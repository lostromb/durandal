using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal
{
    public static class CommonDelegates
    {
        public delegate void VoidDelegate();
        public delegate void StringDelegate(string a);
        public delegate void IntDelegate(int a);
    }
}
