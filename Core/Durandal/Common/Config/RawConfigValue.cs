namespace Durandal.Common.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Durandal.Common.Config.Annotation;
    using Durandal.Common.Utils;
    using Collections;

    public class RawConfigValue
    {
        public ConfigValueType ValueType { get; private set; }
        public IList<ConfigAnnotation> Annotations { get; private set; }
        public IDictionary<VariantConfig, string> VariantValues { get; private set; }
        public string DefaultValue { get; private set; }

        public string Name { get; private set; }

        /// <summary>
        /// Create a new configuration value
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <param name="type"></param>
        public RawConfigValue(string name, string defaultValue, ConfigValueType type)
        {
            Annotations = new List<ConfigAnnotation>();
            VariantValues = new SmallDictionary<VariantConfig, string>();
            ValueType = type;
            Name = name;
            DefaultValue = defaultValue;
        }

        // <summary>
        // Create a new configuration value, storing its string-serialized form along with its type
        // </summary>
        // <param name="rawValue"></param>
        // <param name="type"></param>
        //public RawConfigValue(string nameWithVariants, string rawValue, ConfigValueType type) : base(name, null)
        //{
        //    Annotations = new List<ConfigAnnotation>();
        //    // Parse variant "key&variant=test" values
        //    int amp = name.IndexOf("&");
        //    int col = name.IndexOf(":");
        //    if (amp > 0 && col > 0 && amp < col)
        //    {
        //        _name = name.Substring(0, amp);
        //        _variantName = name.Substring(amp + 1, col - amp - 1);
        //        _variantValue = name.Substring(col + 1);
        //    }
        //    else
        //    {
        //        _name = name;
        //        _variantName = null;
        //        _variantValue = null;
        //    }

        //    _rawValue = rawValue;
        //    ValueType = type;
        //}

        // <summary>
        // Accept a raw string value and attempt to intelligently determine what type it is
        // </summary>
        // <param name="rawValue"></param>
        //public RawConfigValue(string name, string rawValue)
        //{
        //    Annotations = new List<ConfigAnnotation>();
        //    _rawValue = rawValue;
        //    // Parse variant "key&variant:test" values
        //    int amp = name.IndexOf("&");
        //    int col = name.IndexOf(":");
        //    if (amp > 0 && col > 0)
        //    {
        //        if (amp > col)
        //        {
        //            throw new FormatException("The configuration parameter \"" + rawValue + "\" is an invalid format (expected key&variantkey:variant=value)");
        //        }

        //        _name = name.Substring(0, amp);
        //        _variantName = name.Substring(amp + 1, col - amp - 1);
        //        _variantValue = name.Substring(col + 1);
        //    }
        //    else if (amp > 0 || col > 0)
        //    {
        //        throw new FormatException("The configuration parameter \"" + rawValue + "\" cannot contain a & or : character unless it specifies a variant (expected key&variantkey:variant=value)");
        //    }
        //    else
        //    {
        //        _name = name;
        //        _variantName = null;
        //        _variantValue = null;
        //    }
        //    int i;
        //    float f;
        //    bool b;
        //    if (int.TryParse(_rawValue, out i))
        //    {
        //        ValueType = ConfigValueType.Int;
        //    }
        //    else if (float.TryParse(_rawValue, out f))
        //    {
        //        ValueType = ConfigValueType.Float;
        //    }
        //    else if (bool.TryParse(_rawValue.ToLowerInvariant(), out b))
        //    {
        //        ValueType = ConfigValueType.Bool;
        //    }
        //    else if (_rawValue.Contains(","))
        //    {
        //        ValueType = ConfigValueType.StringList;
        //    }
        //    // TODO: Add other parsers here (binary, etc.)
        //    else
        //    {
        //        ValueType = ConfigValueType.String;
        //    }
        //}

        public string GetVariantValue(IDictionary<string, string> variants)
        {
            return VariantConfig.SelectByVariants<string>(VariantValues, Name, variants);
        }

        public void SetValue(string value, IDictionary<string, string> variants = null)
        {
            if (variants == null || variants.Count == 0)
            {
                DefaultValue = value;
            }
            else
            {
                // See if there's a variant config which already exists
                foreach (VariantConfig config in VariantValues.Keys)
                {
                    if (config.EqualsVariantConstraints(variants))
                    {
                        VariantValues[config] = value;
                        return;
                    }
                }

                // No value exists with these exact variants, so create a new one
                VariantConfig newVariant = new VariantConfig(Name, variants);
                VariantValues[newVariant] = value;
            }
        }

        ///// <summary>
        ///// True if this parameter instance is contingent on some variant
        ///// </summary>
        //public bool HasVariants
        //{
        //    get
        //    {
        //        return Variants.Count > 0;
        //    }
        //}

        ///// <summary>
        ///// Returns the name of this parameter including the "&variant=value" portion
        ///// </summary>
        //public string NameWithVariants
        //{
        //    get
        //    {
        //        if (HasVariants)
        //        {
        //            using (PooledStringBuilder sb = StringBuilderPool.Rent())
        //            {
        //                sb.Builder.Append(Name);
        //                foreach (KeyValuePair<string, string> kvp in Variants)
        //                {
        //                    sb.Builder.Append("&");
        //                    sb.Builder.Append(kvp.Key);
        //                    sb.Builder.Append(":");
        //                    sb.Builder.Append(kvp.Value);
        //                }

        //                return sb.Builder.ToString();
        //            }
        //        }

        //        return Name;
        //    }
        //}

        //public override string ToString()
        //{
        //    using (PooledStringBuilder sb = StringBuilderPool.Rent())
        //    {
        //        sb.Builder.Append(Name);
        //        foreach (KeyValuePair<string, string> kvp in Variants)
        //        {
        //            sb.Builder.Append("&");
        //            sb.Builder.Append(kvp.Key);
        //            sb.Builder.Append(":");
        //            sb.Builder.Append(kvp.Value);
        //        }

        //        sb.Builder.Append("=");
        //        sb.Builder.Append(_rawValue);
        //        return sb.Builder.ToString();
        //    }
        //}
    }
}
