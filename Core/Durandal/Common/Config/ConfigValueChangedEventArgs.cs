using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Config
{
    public class ConfigValueChangedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// The configuration key that has changed.
        /// </summary>
        public string Key { get; private set; }

        //public VariantConfig KeyVariants { get; private set; }
        //public T OldValue { get; private set; }
        public T NewValue { get; private set; } // FIXME this new value is meaningless without full variants

        public ConfigValueChangedEventArgs(string key, T newValue)
        {
            Key = key;
            NewValue = newValue;
        }
    }
}
