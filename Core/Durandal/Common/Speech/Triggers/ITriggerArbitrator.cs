using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public interface ITriggerArbitrator
    {
        Task<bool> ArbitrateTrigger(ILogger queryLogger, IRealTimeProvider realTime);
    }
}
