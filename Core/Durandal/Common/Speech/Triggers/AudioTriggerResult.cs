using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public class AudioTriggerResult
    {
        public bool Triggered;
        public bool WasPrimaryKeyword;
        public string TriggeredKeyword;
    }
}
