using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.LG
{
    public class LGSurfaceForm
    {
        public List<LGToken> Tokens { get; private set; }

        public LGSurfaceForm()
        {
            Tokens = new List<LGToken>();
        }

        public int Length
        {
            get
            {
                return Tokens.Count;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is LGSurfaceForm))
                return false;

            LGSurfaceForm other = (LGSurfaceForm)obj;
            if (Tokens.Count != other.Tokens.Count)
                return false;

            for (int c = 0; c < Tokens.Count; c++)
            {
                if (!Tokens[c].Equals(other.Tokens[c]))
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            int returnVal = 0;
            foreach (LGToken t in Tokens)
            {
                returnVal += t.GetHashCode();
            }

            return returnVal;
        }

        public override string ToString()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder returnVal = pooledSb.Builder;
                foreach (LGToken token in Tokens)
                {
                    returnVal.Append(token.ToString());
                }

                return returnVal.ToString();
            }
        }
    }
}
