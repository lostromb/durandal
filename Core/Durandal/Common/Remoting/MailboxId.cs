using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    public struct MailboxId : IEquatable<MailboxId>
    {
        public uint Id;

        public MailboxId(int id)
        {
            Id = unchecked((uint)id);
        }

        public MailboxId(uint id)
        {
            Id = id;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MailboxId))
            {
                return false;
            }

            MailboxId other = (MailboxId)obj;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return Id.ToString();
        }

        public static bool operator ==(MailboxId left, MailboxId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MailboxId left, MailboxId right)
        {
            return !(left == right);
        }

        public bool Equals(MailboxId other)
        {
            return Id == other.Id;
        }
    }
}
