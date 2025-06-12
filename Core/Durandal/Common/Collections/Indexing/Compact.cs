namespace Durandal.Common.Collections.Indexing
{
    public struct Compact<T> : System.IEquatable<Compact<T>>
    {
        public uint Addr;
        
        public Compact(uint val)
        {
            Addr = val;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType())
                return false;
            Compact<T> o = (Compact<T>)obj;

            return Equals(o);
        }

        public override int GetHashCode()
        {
            return Addr.GetHashCode();
        }

        public static bool operator == (Compact<T> current, Compact<T> other)
        {
            return current.Addr == other.Addr;
        }

        public static bool operator !=(Compact<T> current, Compact<T> other)
        {
            return current.Addr != other.Addr;
        }

        public bool Equals(Compact<T> other)
        {
            return Addr == other.Addr;
        }
    }
}
