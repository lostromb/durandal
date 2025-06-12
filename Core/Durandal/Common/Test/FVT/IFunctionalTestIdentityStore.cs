using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Test.FVT
{
    public interface IFunctionalTestIdentityStore
    {
        Task<FunctionalTestIdentityPair> GetIdentities(FunctionalTestFeatureConstraints userConstraints, FunctionalTestFeatureConstraints clientConstraints, ILogger traceLogger);

        Task ReleaseIdentities(FunctionalTestIdentityPair identities, ILogger traceLogger);
    }
}
