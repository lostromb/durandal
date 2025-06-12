using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Client
{
    public class UserIdentity
    {
        public string Id;
        public string FullName;
        public string GivenName;
        public string Surname;
        public string Email;
        public string AuthProvider;
        public byte[] IconPng;
    }
}
