using Durandal.Common.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Durandal.Tests.Common.Parser;

namespace Durandal.Tests.Common.Parser
{
    public class PosAwareStr : IPositionAware<PosAwareStr>
    {
        public PosAwareStr SetPos(Position startPos, int length)
        {
            Pos = startPos;
            Length = length;
            return this;
        }

        public Position Pos
        {
            get;
            set;
        }

        public int Length
        {
            get;
            set;
        }

        public string Value
        {
            get;
            set;
        }
    }
}
