using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Client
{
    public class UserIdentityChangedEventArgs : EventArgs
    {
        public UserIdentityChangedEventArgs(UserIdentity newIdentity)
        {
            NewIdentity = newIdentity;
        }

        public UserIdentity NewIdentity
        {
            get;
            set;
        }
    }
}
