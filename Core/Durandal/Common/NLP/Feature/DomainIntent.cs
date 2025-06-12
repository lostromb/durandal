using System;

namespace Durandal.Common.NLP.Feature
{
    public struct DomainIntent : System.IEquatable<DomainIntent>
    {
        private string _cachedValue;

        public string Domain;
        public string Intent;

        public DomainIntent(string domain, string intent)
        {
            this.Domain = domain;
            this.Intent = intent;
            _cachedValue = null;
        }

        public override int GetHashCode()
        {
            return this.Domain.GetHashCode() + this.Intent.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            DomainIntent o = (DomainIntent)obj;
            return Equals(o);
        }

        public override string ToString()
        {
            if (_cachedValue == null)
            {
                _cachedValue = this.Domain + "/" + this.Intent;
            }

            return _cachedValue;
        }

        public static bool operator ==(DomainIntent left, DomainIntent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DomainIntent left, DomainIntent right)
        {
            return !(left == right);
        }

        public bool Equals(DomainIntent other)
        {
            return string.Equals(Intent, other.Intent, StringComparison.Ordinal) &&
                string.Equals(Domain, other.Domain, StringComparison.Ordinal);
        }
    }
}
