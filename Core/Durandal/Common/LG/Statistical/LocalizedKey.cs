using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.LG.Statistical
{
    public class LocalizedKey
    {
        public string Key;
        public LanguageCode Locale;

        public LocalizedKey()
        {
            Key = string.Empty;
            Locale = null;
        }

        public LocalizedKey(string key, LanguageCode locale)
        {
            Key = key;
            Locale = locale;
        }

        public bool IsEmpty()
        {
            return string.IsNullOrEmpty(Key) &&
                Locale == null;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is LocalizedKey))
            {
                return false;
            }

            LocalizedKey other = (LocalizedKey)obj;
            return string.Equals(Key, other.Key) &&
                object.Equals(Locale, other.Locale);
        }

        public override int GetHashCode()
        {
            int returnVal = 0;
            if (Key != null)
            {
                returnVal += Key.GetHashCode();
            }
            if (Locale != null)
            {
                returnVal += Locale.GetHashCode();
            }

            return returnVal;
        }

        public override string ToString()
        {
            return Key + ":" + Locale.ToBcp47Alpha2String();
        }
    }
}
