using System;
using System.Collections.Generic;
using System.Linq;

namespace ManagedBass
{
    class ReferenceHolder
    {
        readonly Dictionary<Tuple<int, int>, object> _procedures = new Dictionary<Tuple<int, int>, object>();
        readonly SyncProcedure _freeproc;

        public ReferenceHolder()
        {
            _freeproc = Callback;
        }

        public void Add(int Handle, int SpecificHandle, object proc)
        {
            if (proc == null)
                return;

            if (proc.Equals(_freeproc))
                return;

            var key = Tuple.Create(Handle, SpecificHandle);

            var contains = _procedures.ContainsKey(key);
            
            if (_freeproc != null && _procedures.All(pair => pair.Key.Item1 != Handle))
                Bass.ChannelSetSync(Handle, SyncFlags.Free, 0, _freeproc);

            if (contains)
                _procedures[key] = proc;
            else _procedures.Add(key, proc);
        }

        public void Remove(int Handle, int SpecialHandle)
        {
            var key = Tuple.Create(Handle, SpecialHandle);
            
            if (_procedures.ContainsKey(key))
                _procedures.Remove(key);
        }

        void Callback(int Handle, int Channel, int Data, IntPtr User)
        {
            var toRemove = _procedures.Where(Pair => Pair.Key.Item1 == Channel).Select(Pair => Pair.Key).ToArray();
            
            foreach (var key in toRemove)
                _procedures.Remove(key);
        }
    }
}
