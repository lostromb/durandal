using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Test.FVT
{
    public class BasicFunctionalTestIdentityStore : IFunctionalTestIdentityStore
    {
        private readonly ConcurrentQueue<FunctionalTestIdentityPair> _pooledIdentities;

        public BasicFunctionalTestIdentityStore()
        {
            _pooledIdentities = new ConcurrentQueue<FunctionalTestIdentityPair>();
        }

        public async Task<FunctionalTestIdentityPair> GetIdentities(
            FunctionalTestFeatureConstraints userConstraints,
            FunctionalTestFeatureConstraints clientConstraints,
            ILogger traceLogger)
        {
            if (!userConstraints.IsEmpty ||
                !clientConstraints.IsEmpty)
            {
                await DurandalTaskExtensions.NoOpTask;
                throw new NotImplementedException(nameof(BasicFunctionalTestIdentityStore) + " does not support identity constraints");
            }

            FunctionalTestIdentityPair rr;
            if (_pooledIdentities.TryDequeue(out rr))
            {
                return rr;
            }
            else
            {
                // If there's not enough pooled identities, generate a new one
                FunctionalTestIdentityPair returnVal = new FunctionalTestIdentityPair();
                returnVal.UserIdentity = new FunctionalTestIdentity()
                {
                    AuthScope = ClientAuthenticationScope.User,
                    UserId = "FVT_TEST_USER_" + Guid.NewGuid().ToString("N"),
                    ClientId = null,
                    Features = new HashSet<string>(),
                    Key = null
                };

                returnVal.ClientIdentity = new FunctionalTestIdentity()
                {
                    AuthScope = ClientAuthenticationScope.Client,
                    UserId = null,
                    ClientId = "FVT_TEST_CLIENT_" + Guid.NewGuid().ToString("N"),
                    Features = new HashSet<string>(),
                    Key = null,
                };

                return returnVal;
            }
        }

        public Task ReleaseIdentities(FunctionalTestIdentityPair identities, ILogger traceLogger)
        {
            _pooledIdentities.Enqueue(identities);
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
