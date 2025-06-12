using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class HashTable
    {
        /*
         * HACK!!  Initial hash table size is restricted by this set of primes.  (Of course,
         * collision resolution by chaining will accommodate more entries indefinitely, but
         * efficiency will drop.)
         */
        private static readonly int[] prime = {
            101, 211, 307, 401, 503, 601, 701, 809, 907,
            1009, 1201, 1601, 2003, 2411, 3001, 4001, 5003, 6007, 7001, 8009,
            9001,
            10007, 12007, 16001, 20011, 24001, 30011, 40009, 50021, 60013,
            70001, 80021, 90001,
            100003, 120011, 160001, 200003, 240007, 300007, 400009, 500009,
            600011, 700001, 800011, 900001,
            -1
        };

        public const int HASH_CASE_YES = 0;
        public const int HASH_CASE_NO = 1;

        internal static object hash_entry_val(hash_entry_t e)
        {
            return e.val;
        }

        internal static Pointer<byte> hash_entry_key(hash_entry_t e)
        {
            return e.key;
        }

        internal static uint hash_entry_len(hash_entry_t e)
        {
            return e.len;
        }

        internal static int hash_table_inuse(hash_table_t e)
        {
            return e.inuse;
        }

        internal static int hash_table_size(hash_table_t e)
        {
            return e.size;
        }

        /*
         * This function returns a very large prime.
         */
        internal static int prime_size(int size, SphinxLogger logger)
        {
            int i;

            for (i = 0; (prime[i] > 0) && (prime[i] < size); i++) ;
            if (prime[i] <= 0)
            {
                logger.E_WARN(string.Format("Very large hash table requested ({0} entries)\n", size));
                --i;
            }
            return (prime[i]);
        }
        
        internal static hash_table_t hash_table_new(int size, int casearg, SphinxLogger logger)
        {
            hash_table_t h = new hash_table_t();
            h.size = prime_size(size + (size >> 1), logger);
            h.nocase = (casearg == HASH_CASE_NO) ? 1 : 0;
            h.table = CKDAlloc.ckd_calloc_struct<hash_entry_t>(h.size);
            /* The above calloc clears h.table[*].key and .next to NULL, i.e. an empty table */

            return h;
        }
        
        /*
         * Compute hash value for given key string.
         * Somewhat tuned for English text word strings.
         */
        internal static uint key2hash(hash_table_t h, Pointer<byte> key)
        {
            Pointer<byte> cp;

            /* This is a hack because the best way to solve it is to make sure 
               all byteacter representation is unsigned byteacter in the first place.        
               (or better unicode.) */
            byte c;
            int s;
            uint hash;

            hash = 0;
            s = 0;

            if (h.nocase != 0)
            {
                for (cp = key; cp.IsNonNull; cp++)
                {
                    c = +cp;
                    c = StringCase.UPPER_CASE(c);
                    hash += (uint)c << s;
                    s += 5;
                    if (s >= 25)
                        s -= 24;
                }
            }
            else
            {
                for (cp = key; cp.Deref != 0; cp++)
                {
                    hash += ((uint)cp.Deref) << s;
                    s += 5;
                    if (s >= 25)
                        s -= 24;
                }
            }

            return (hash % (uint)h.size);
        }


        internal static Pointer<byte> makekey(Pointer<byte> data, uint len, Pointer<byte> key)
        {
            uint i, j;

            if (key.IsNull)
                key = CKDAlloc.ckd_calloc<byte>(len * 2 + 1);

            for (i = 0, j = 0; i < len; i++, j += 2)
            {
                key[j] = (byte)('A' + (data[i] & 0x000f));
                key[j + 1] = (byte)('J' + ((data[i] >> 4) & 0x000f));
            }
            key[j] = (byte)('\0');

            return key;
        }


        internal static int keycmp_nocase(hash_entry_t entry, Pointer<byte> key)
        {
            byte c1, c2;
            int i;
            Pointer<byte> str;

            str = entry.key;
            for (i = 0; i < entry.len; i++)
            {
                str = str.Iterate(out c1);
                c1 = StringCase.UPPER_CASE(c1);
                key = key.Iterate(out c2);
                c2 = StringCase.UPPER_CASE(c2);
                if (c1 != c2)
                    return (c1 - c2);
            }

            return 0;
        }


        internal static int keycmp_case(hash_entry_t entry, Pointer<byte> key)
        {
            byte c1, c2;
            int i;
            Pointer<byte> str;
            str = entry.key;
            for (i = 0; i < entry.len; i++)
            {
                str = str.Iterate(out c1);
                key = key.Iterate(out c2);
                if (c1 != c2)
                    return (c1 - c2);
            }

            return 0;
        }


        /*
         * Lookup entry with hash-value hash in table h for given key
         * Return value: hash_entry_t for key
         */
        internal static hash_entry_t lookup(hash_table_t h, uint hash, Pointer<byte> key, uint len)
        {
            hash_entry_t entry;

            entry = h.table[hash];
            if (!entry.key.IsNonNull)
                return null;

            if (h.nocase != 0)
            {
                while (entry != null && ((entry.len != len)
                                 || (keycmp_nocase(entry, key) != 0)))
                    entry = entry.next;
            }
            else
            {
                while (entry != null && ((entry.len != len)
                                 || (keycmp_case(entry, key) != 0)))
                    entry = entry.next;
            }

            return entry;
        }


        internal static int hash_table_lookup(hash_table_t h, Pointer<byte> key, BoxedValue<object> val)
        {
            hash_entry_t entry;
            uint hash;
            uint len;

            hash = key2hash(h, key);
            len = cstring.strlen(key);

            entry = lookup(h, hash, key, len);
            if (entry != null)
            {
                if (val != null)
                    val.Val = entry.val;
                return 0;
            }
            else
                return -1;
        }

        internal static int hash_table_lookup_int32(hash_table_t h, Pointer<byte> key, BoxedValueInt val)
        {
            BoxedValue<object> vval = new BoxedValue<object>();
            int rv;

            rv = hash_table_lookup(h, key, vval);
            if (rv != 0)
                return rv;
            if (val != null)
            {
                if (vval.Val is int)
                {
                    val.Val = (int)vval.Val;
                }
                else if (vval.Val is long)
                {
                    val.Val = (int)(long)vval.Val;
                }
                else throw new Exception("what");
            }
            return 0;
        }
        
        internal static int hash_table_lookup_bkey(hash_table_t h, Pointer<byte> key, uint len, BoxedValue<object> val)
        {
            hash_entry_t entry;
            uint hash;
            Pointer<byte> str;

            str = makekey((Pointer<byte>)key, len, PointerHelpers.NULL<byte>());
            hash = key2hash(h, str);

            entry = lookup(h, hash, key, len);
            if (entry != null)
            {
                if (val != null)
                    val.Val = entry.val;
                return 0;
            }
            else
                return -1;
        }

        internal static int hash_table_lookup_bkey_int(hash_table_t h, Pointer<byte> key, uint len, BoxedValueInt val)
        {
            BoxedValue<object> vval = new BoxedValue<object>();
            int rv;

            rv = hash_table_lookup_bkey(h, key, len, vval);
            if (rv != 0)
                return rv;
            if (val != null)
                val.Val = (int)(long)vval.Val;
            return 0;
        }


        internal static T enter<T>(hash_table_t h, uint hash, Pointer<byte> key, uint len, T val, int replace)
        {
            hash_entry_t cur, _new;
            if ((cur = lookup(h, hash, key, len)) != null)
            {
                object oldval;
                /* Key already exists. */
                oldval = cur.val;
                if (replace != 0)
                {
                    /* Replace the pointer if replacement is requested,
                     * because this might be a different instance of the same
                     * string (this verges on magic, sorry) */
                    cur.key = key;
                    cur.val = val;
                }
                return (T)oldval;
            }

            cur = h.table[hash];
            if (!cur.key.IsNonNull)
            {
                /* Empty slot at hashed location; add this entry */
                cur.key = key;
                cur.len = len;
                cur.val = val;

                /* Added by ARCHAN at 20050515. This allows deletion could work. */
                cur.next = null;

            }
            else
            {
                /* Key collision; create new entry and link to hashed location */
                _new = new hash_entry_t();
                _new.key = key;
                _new.len = len;
                _new.val = val;
                _new.next = cur.next;
                cur.next = _new;
            }
            ++h.inuse;

            return val;
        }

        /* 20050523 Added by ARCHAN  to delete a key from a hash table */
        internal static object delete(hash_table_t h, uint hash, Pointer<byte> key, uint len)
        {
            hash_entry_t entry, prev;
            object val;

            prev = null;
            entry = h.table[hash];
            if (!entry.key.IsNonNull)
                return null;

            if (h.nocase != 0)
            {
                while (entry != null && ((entry.len != len)
                                 || (keycmp_nocase(entry, key) != 0)))
                {
                    prev = entry;
                    entry = entry.next;
                }
            }
            else
            {
                while (entry != null && ((entry.len != len)
                                 || (keycmp_case(entry, key) != 0)))
                {
                    prev = entry;
                    entry = entry.next;
                }
            }

            if (entry == null)
                return null;

            /* At this point, entry will be the one required to be deleted, prev
               will contain the previous entry
             */
            val = entry.val;

            if (prev == null)
            {
                /* That is to say the entry in the hash table (not the chain) matched the key. */
                /* We will then copy the things from the next entry to the hash table */
                prev = entry;
                if (entry.next != null)
                {      /* There is a next entry, great, copy it. */
                    entry = entry.next;
                    prev.key = entry.key;
                    prev.len = entry.len;
                    prev.val = entry.val;
                    prev.next = entry.next;
                }
                else
                {                  /* There is not a next entry, just set the key to null */
                    prev.key = PointerHelpers.NULL<byte>();
                    prev.len = 0;
                    prev.next = null;
                }

            }
            else
            {                      /* This case is simple */
                prev.next = entry.next;
            }

            /* Do wiring and free the entry */

            --h.inuse;

            return val;
        }

        internal static void hash_table_empty(hash_table_t h)
        {
            hash_entry_t e, e2;
            int i;

            for (i = 0; i < h.size; i++)
            {
                /* Free collision lists. */
                for (e = h.table[i].next; e != null; e = e2)
                {
                    e2 = e.next;
                }

                PointerHelpers.ZeroOutStruct(h.table + i, 1);
            }
            h.inuse = 0;
        }
        
        internal static int hash_table_enter_int32(hash_table_t h, Pointer<byte> key, int val)
        {
            return hash_table_enter(h, key, val);
        }

        internal static T hash_table_enter<T>(hash_table_t h, Pointer<byte> key, T val)
        {
            uint hash;
            uint len;

            hash = key2hash(h, key);
            len = cstring.strlen(key);
            return (enter(h, hash, key, len, val, 0));
        }

        internal static object hash_table_replace(hash_table_t h, Pointer<byte> key, object val)
        {
            uint hash;
            uint len;

            hash = key2hash(h, key);
            len = cstring.strlen(key);
            return (enter(h, hash, key, len, val, 1));
        }

        internal static object hash_table_delete(hash_table_t h, Pointer<byte> key)
        {
            uint hash;
            uint len;

            hash = key2hash(h, key);
            len = cstring.strlen(key);

            return (delete(h, hash, key, len));
        }

        internal static int hash_table_enter_bkey_int32(hash_table_t h, Pointer<byte> key, uint len, int val)
        {
            return hash_table_enter_bkey(h, key, len, val);
        }

        internal static T hash_table_enter_bkey<T>(hash_table_t h, Pointer<byte> key, uint len, T val)
        {
            uint hash;
            Pointer<byte> str;

            str = makekey(key, len, PointerHelpers.NULL<byte>());
            hash = key2hash(h, str);

            return (enter(h, hash, key, len, val, 0));
        }

        internal static object hash_table_replace_bkey(hash_table_t h, Pointer<byte> key, uint len, object val)
        {
            uint hash;
            Pointer<byte> str;

            str = makekey(key, len, PointerHelpers.NULL<byte>());
            hash = key2hash(h, str);

            return (enter(h, hash, key, len, val, 1));
        }

        internal static object hash_table_delete_bkey(hash_table_t h, Pointer<byte> key, uint len)
        {
            uint hash;
            Pointer<byte> str;

            str = makekey(key, len, PointerHelpers.NULL<byte>());
            hash = key2hash(h, str);

            return (delete(h, hash, key, len));
        }

        internal static List<hash_entry_t> hash_table_tolist(hash_table_t h, BoxedValueInt count)
        {
            List<hash_entry_t> g;
            hash_entry_t e;
            int i, j;

            g = new List<hash_entry_t>();

            j = 0;
            for (i = 0; i < h.size; i++)
            {
                e = h.table[i];

                if (e.key.IsNonNull)
                {
                    g.Add(e);
                    j++;

                    for (e = e.next; e != null; e = e.next)
                    {
                        g.Add(e);
                        j++;
                    }
                }
            }

            if (count != null)
                count.Val = j;

            return g;
        }

        internal static hash_iter_t hash_table_iter(hash_table_t h)
        {
            hash_iter_t itor = new hash_iter_t();
            itor.ht = h;
            return hash_table_iter_next(itor);
        }

        internal static hash_iter_t hash_table_iter_next(hash_iter_t itor)
        {
            /* If there is an entry, walk down its list. */
            if (itor.ent != null)
                itor.ent = itor.ent.next;
            /* If we got to the end of the chain, or we had no entry, scan
	         * forward in the table to find the next non-empty bucket. */
            if (itor.ent == null)
            {
                while (itor.idx < itor.ht.size
                       && itor.ht.table[itor.idx].key.IsNull)
                    ++itor.idx;
                /* If we did not find one then delete the iterator and
		         * return NULL. */
                if (itor.idx == itor.ht.size)
                {
                    return null;
                }
                /* Otherwise use this next entry. */
                itor.ent = itor.ht.table[itor.idx];
                /* Increase idx for the next time around. */
                ++itor.idx;
            }
            return itor;
        }
    }
}