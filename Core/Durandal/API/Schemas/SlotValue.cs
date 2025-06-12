
namespace Durandal.API
{
    using Durandal.Common.Time.Timex;
    using Durandal.Common.Time.Timex.Enums;
    using Durandal.Common.Ontology;
    using Durandal.Common.Statistics;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Durandal.Common.Dialog;
    using Durandal.Common.Utils;

    public class SlotValue
    {
        private static readonly Regex SLOT_NAME_VALIDATOR = new Regex("^[a-zA-Z0-9_\\.]+$");

        /// <summary>
        /// The name of this slot. Slot names must contain only letters, numbers, underscore, and the period character. By convention, slot names are lower case.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The string value of this slot, usually the actual value that was matched from an utterance
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// The format of this slot, which gives a hint at how it was generated
        /// </summary>
        public SlotValueFormat Format { get; set; }

        /// <summary>
        /// Annotations, which are generic key-value pairs of data associated with this slot
        /// </summary>
        public Dictionary<string, string> Annotations { get; set; }

        /// <summary>
        /// A potential list of homophones that may also represent this slot's value, based on speech recognition hypotheses
        /// </summary>
        public List<string> Alternates { get; set; }

        /// <summary>
        /// The lexical form of this slot as it was spoken by a user.
        /// The format MUST be an IPA syllable string. If the query was not spoken, this value will be null.
        /// </summary>
        public string LexicalForm { get; set; }
        
        public SlotValue()
        {
            Name = string.Empty;
            Value = string.Empty;
            Format = SlotValueFormat.TypedText;
            Annotations = new Dictionary<string, string>();
            LexicalForm = string.Empty;
        }

        public SlotValue(string name, string value, SlotValueFormat type, string lexicalForm = null)
        {
            if (!SLOT_NAME_VALIDATOR.IsMatch(name))
            {
                throw new FormatException("Slot names must contain only letters, numbers, underscore, and the period character. Input was \"" + name + "\"");
            }

            Alternates = new List<string>();
            Annotations = new Dictionary<string, string>();
            Name = name;
            Value = value;
            Format = type;
            LexicalForm = lexicalForm ?? string.Empty;
        }

        public bool HasProperty(string key)
        {
            return Annotations.ContainsKey(key);
        }

        public void SetProperty(string key, string value)
        {
            Annotations[key] = value;
        }

        public string GetProperty(string key)
        {
            if (Annotations.ContainsKey(key))
            {
                return Annotations[key];
            }

            return null;
        }

        [JsonIgnore]
        public IEnumerable<string> PropertyNames
        {
            get
            {
                return Annotations.Keys;
            }
        }

        public void AddTimexMatch(TimexMatch input)
        {
            TimexEntity newEntity = new TimexEntity();
            newEntity.Index = input.Index;
            newEntity.Id = input.Id;
            newEntity.Type = input.ExtendedDateTime.FormatType();
            newEntity.Value = input.ExtendedDateTime.FormatValue();
            newEntity.TimexDictionary = input.ExtendedDateTime.OriginalTimexDictionary;
            newEntity.Comment = input.ExtendedDateTime.FormatComment();
            newEntity.Frequency = input.ExtendedDateTime.FormatFrequency();
            newEntity.Modifier = input.ExtendedDateTime.FormatMod();
            newEntity.Quantity = input.ExtendedDateTime.FormatQuantity();

            string timexStringValue = newEntity.ToJson();

            string nextTimexKey = GetNextTimexIndex();
            if (!string.IsNullOrEmpty(nextTimexKey) && !string.IsNullOrEmpty(nextTimexKey))
            {
                SetProperty(nextTimexKey, timexStringValue);
            }
        }

        public void AddEntity(Hypothesis<Entity> input)
        {
            string stringValue = input.Value.EntityId + "|" + input.Value.EntityTypeName + "|" + input.Conf.ToString();
            string nextKey = GetNextEntityIndex();
            if (!string.IsNullOrEmpty(stringValue) && !string.IsNullOrEmpty(nextKey))
            {
                SetProperty(nextKey, stringValue);
            }
        }

        /// <summary>
        /// Finds an empty annotation slot to add an extra timex annotation to this slot,
        /// up to a max of 10 matches
        /// </summary>
        /// <returns>The name of the slot property you can use</returns>
        private string GetNextTimexIndex()
        {
            for (int index = 0; index < 10; index++)
            {
                string testValue = SlotPropertyName.TimexMatch + index.ToString();
                if (!Annotations.ContainsKey(testValue))
                {
                    return testValue;
                }
            }

            return string.Empty;
        }

        private string GetNextEntityIndex()
        {
            for (int index = 0; index < 20; index++)
            {
                string testValue = SlotPropertyName.Entity + index.ToString();
                if (!Annotations.ContainsKey(testValue))
                {
                    return testValue;
                }
            }

            return string.Empty;
        }

        public bool HasTimexMatches()
        {
            foreach (string property in Annotations.Keys)
            {
                if (property.StartsWith(SlotPropertyName.TimexMatch))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieves the list of all time matches that occurred within this tag
        /// </summary>
        /// <param name="typesToMatch"></param>
        /// <param name="timeContext"></param>
        /// <returns></returns>
        public IList<TimexMatch> GetTimeMatches(TemporalType typesToMatch, TimexContext timeContext)
        {
            IList<TimexMatch> returnVal = new List<TimexMatch>();
            char[] equalsSign = { '=' };
            bool hasMore = true;
            int currentIndex = 0;

            while (hasMore)
            {
                if (Annotations.ContainsKey(SlotPropertyName.TimexMatch + currentIndex))
                {
                    TimexEntity parsedEntity = TimexEntity.ParseFromJson(Annotations[SlotPropertyName.TimexMatch + currentIndex]);
                    if (parsedEntity == null)
                        continue;

                    TemporalType newType;
                    if (!EnumExtensions.TryParse(parsedEntity.Type, out newType))
                    {
                        newType = TemporalType.None;
                    }

                    ExtendedDateTime reifiedTime = ExtendedDateTime.Create(newType, parsedEntity.TimexDictionary, timeContext);

                    if (reifiedTime != null && typesToMatch.HasFlag(reifiedTime.TemporalType))
                    {
                        returnVal.Add(new TimexMatch()
                        {
                            ExtendedDateTime = reifiedTime,
                            Id = parsedEntity.Id,
                            Value = parsedEntity.Value,
                            Index = parsedEntity.Index
                        });
                    }

                    currentIndex++;
                }
                else
                {
                    hasMore = false;
                }
            }
            return returnVal;
        }

        public IList<ContextualEntity> GetEntities(KnowledgeContext context)
        {
            List<ContextualEntity> returnVal = new List<ContextualEntity>();
            bool hasMore = true;
            int currentIndex = 0;
            while (hasMore)
            {
                if (Annotations.ContainsKey(SlotPropertyName.Entity + currentIndex))
                {
                    string value = Annotations[SlotPropertyName.Entity + currentIndex];
                    string[] parts = value.Split('|');
                    float conf = 1.0f;
                    string entityId = parts[0];
                    Entity entity = context.GetEntityInMemory(entityId);
                    if (parts.Length == 3)
                    {
                        if (!float.TryParse(parts[2], out conf))
                        {
                            conf = 1.0f;
                        }
                    }

                    returnVal.Add(new ContextualEntity(entity, ContextualEntitySource.LanguageUnderstanding, conf));
                    currentIndex++;
                }
                else
                {
                    hasMore = false;
                }
            }
            
            returnVal.Sort(new ContextualEntity.DescendingComparator());

            return returnVal;
        }

        /// <summary>
        /// Returns the ordinal associated with this slot, or NULL if none exists
        /// </summary>
        /// <returns></returns>
        public Ordinal GetOrdinal()
        {
            if (!Annotations.ContainsKey(SlotPropertyName.Ordinal))
            {
                return null;
            }

            return Ordinal.Parse(Annotations[SlotPropertyName.Ordinal]);
        }

        /// <summary>
        /// Returns the numerical value parsed from this slot, or NULL if none exists
        /// </summary>
        /// <returns></returns>
        public decimal? GetNumber()
        {
            if (!Annotations.ContainsKey(SlotPropertyName.Number))
            {
                return null;
            }

            string annotation = Annotations[SlotPropertyName.Number];

            // Parse fractions
            if (annotation.Contains("/"))
            {
                int slashIndex = annotation.IndexOf('/');
                string part1 = annotation.Substring(0, slashIndex).Trim();
                string part2 = annotation.Substring(slashIndex + 1).Trim();
                decimal numerator;
                decimal denominator;

                if (!decimal.TryParse(part1, out numerator) ||
                    !decimal.TryParse(part2, out denominator) ||
                    denominator == 0)
                {
                    return null;
                }

                return numerator / denominator;
            }
            else
            {
                decimal returnVal;
                if (!decimal.TryParse(annotation, out returnVal))
                {
                    return null;
                }

                return returnVal;
            }
        }

        public IList<string> GetSpellSuggestions()
        {
            IList<string> returnVal = new List<string>();
            if (Annotations.ContainsKey(SlotPropertyName.SpellSuggestions))
            {
                string[] suggestions = Annotations[SlotPropertyName.SpellSuggestions].Split('\n');
                foreach (string x in suggestions)
                {
                    if (!string.IsNullOrEmpty(x))
                    {
                        returnVal.Add(x);
                    }
                }
            }

            return returnVal;
        }

        public SlotValue Clone()
        {
            SlotValue clone = new SlotValue();
            clone.Name = this.Name;
            clone.Value = this.Value;
            clone.LexicalForm = this.LexicalForm;
            clone.Format = this.Format;
            if (this.Alternates != null)
            {
                clone.Alternates = new List<string>();
                foreach (string alt in this.Alternates)
                {
                    clone.Alternates.Add(alt);
                }
            }
            foreach (var anno in this.Annotations)
            {
                clone.Annotations.Add(anno.Key, anno.Value);
            }

            return clone;
        }

        public override string ToString()
        {
            return string.Format("{0} = {1}", Name, Value);
        }
    }
} // Durandal.API
