using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Config
{
    /// <summary>
    /// An implementation of Configuration which is entirely volatile in-memory
    /// </summary>
    public class InMemoryConfiguration : AbstractConfiguration
    {
        
        public InMemoryConfiguration(ILogger logger) : base(logger, DefaultRealTimeProvider.Singleton)
        {
        }
        
        protected override Task CommitChanges(IRealTimeProvider realTime)
        {
            // In-memory configuration is volatile storage by definition
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
