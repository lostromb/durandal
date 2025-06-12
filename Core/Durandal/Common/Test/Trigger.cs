using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Test
{
    public class Trigger
    {
        public volatile int TriggerCount;

        public void Trip()
        {
            TriggerCount++;
        }

        public int Get()
        {
            return TriggerCount;
        }

        public void Reset()
        {
            TriggerCount = 0;
        }
    }
}
