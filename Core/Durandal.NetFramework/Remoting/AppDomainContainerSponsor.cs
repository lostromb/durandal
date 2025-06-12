using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// ISponsor object used to maintain memory references across app domains to prevent garbage collection from interfering
    /// </summary>
    public class AppDomainContainerSponsor : MarshalByRefObject, ISponsor, IDisposable
    {
        /*
         * @CoryNelson said :
         * I've since determined that the ILease objects of my sponsors 
         * themselves are being GCed. They start out with the default 5min lease 
         * time, which explains how often my sponsors are being called. When I 
         * set my InitialLeaseTime to 1min, the ILease objects are continually        
         * renewed due to their RenewOnCallTime being the default of 2min.
         */
        private ILease _lease;

        public AppDomainContainerSponsor(MarshalByRefObject mbro)
        {
            _lease = (ILease)RemotingServices.GetLifetimeService(mbro);
            if (_lease == null)
            {
                throw new NotSupportedException("Lease instance for MarshalByRefObject is null");
            }

            _lease.Register(this);
        }

        public TimeSpan Renewal(ILease lease)
        {
            return this._lease != null ? lease.InitialLeaseTime : TimeSpan.Zero;
        }

        public void Dispose()
        {
            if (_lease != null)
            {
                _lease.Unregister(this);
                _lease = null;
            }
        }

        public override object InitializeLifetimeService()
        {
            /*
             * @MatthewLee said:
             *   It's been a long time since this question was asked, but I ran into this today and after a couple hours, I figured it out. 
             * The 5 minutes issue is because your Sponsor which has to inherit from MarshalByRefObject also has an associated lease. 
             * It's created in your Client domain and your Host domain has a proxy to the reference in your Client domain. 
             * This expires after the default 5 minutes unless you override the InitializeLifetimeService() method in your Sponsor class or this sponsor has its own sponsor keeping it from expiring.
             *   Funnily enough, I overcame this by returning Null in the sponsor's InitializeLifetimeService() override to give it an infinite timespan lease, and I created my ISponsor implementation to remove that in a Host MBRO.
             * Source: https://stackoverflow.com/questions/18680664/remoting-sponsor-stops-being-called
            */
            return null;
        }
    }
}
