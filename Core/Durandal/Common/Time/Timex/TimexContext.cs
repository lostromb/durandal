using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Durandal.Common.Time.Timex.Calendar;
using Durandal.Common.Time.Timex.Enums;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Represents a context of a text
    /// </summary>
    public class TimexContext
    {
        private readonly Dictionary<PartOfDay, PartOfDayDefaultTimes> partOfDayDefaultTimes = new Dictionary<PartOfDay, PartOfDayDefaultTimes>();

        /// <summary>
        /// Default constructor to assign default values to this object's properties.
        /// </summary>
        public TimexContext()
        {
            UseInference = true;
            TemporalType = TemporalType.All;
            Normalization = Normalization.Present;
            AmPmInferenceCutoff = 7;
            WeekdayLogicType = WeekdayLogic.SimpleOffset;
            IncludeCurrentTimeInPastOrFuture = false;
            WeekDefinition = LocalizedWeekDefinition.StandardWeekDefinition;
            DefaultValueOfVagueOffset = CreateDefaultVagueOffsetDictionary();
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        public TimexContext(TimexContext other)
        {
            if (other == null)
            {
                return;
            }
            
            ReferenceDateTime = new DateTime(other.ReferenceDateTime.Ticks);
            Normalization = other.Normalization;
            TemporalType = other.TemporalType;
            UseInference = other.UseInference;
            AmPmInferenceCutoff = other.AmPmInferenceCutoff;
            WeekdayLogicType = other.WeekdayLogicType;
            IncludeCurrentTimeInPastOrFuture = other.IncludeCurrentTimeInPastOrFuture;
            WeekDefinition = other.WeekDefinition;
            this.partOfDayDefaultTimes = other.PartOfDayDefaultTimes;
            DefaultValueOfVagueOffset = other.DefaultValueOfVagueOffset;
        }

        /// <summary>
        /// Reference DateTime according to which time expressions should be normalized 
        /// (e.g. current DateTime, DateTime of a document or DateTime of a previously extracted text)
        /// </summary>
        public DateTime ReferenceDateTime { get; set; }

        /// <summary>
        /// Normalization (Present, Future, Past) to be used
        /// (e.g. if Normalization == Present then Monday will be normalized to the Monday on the week of the ReferenceDateTime,
        /// if Normalization == Future then Monday will be normalized to the nearest Monday next to the ReferenceDateTime,
        /// if Normalization == Past then Monday will be normalized to the nearest Monday previous to the ReferenceDateTime)
        /// </summary>
        public Normalization Normalization { get; set; }

        /// <summary>
        /// Type of the rules used to extract time expressions
        /// </summary>
        public TemporalType TemporalType { get; set; }

        /// <summary>
        /// Flag indicating whether the timex code should attempt to calculate dates and times (based on the reference date) if data is ambiguous
        /// </summary>
        public bool UseInference { get; set; }

        /// <summary>
        /// In the absence of context, hours that are lower than this number are assumed to be PM. Everything else (including this value) is assumed AM.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "Pm")]
        public int AmPmInferenceCutoff { get; set; }

        /// <summary>
        /// Gets or sets the type of weekday logic to use
        /// </summary>
        public WeekdayLogic WeekdayLogicType { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether the current date or time should be factored into past/future inferences
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1702")]
        public bool IncludeCurrentTimeInPastOrFuture { get; set; }

        /// <summary>
        /// Gets or sets the current definition of "weekday" and "weekend" in the current locale
        /// </summary>
        public LocalizedWeekDefinition WeekDefinition { get; set; }

        /// <summary>
        /// Gets or sets the default value to fill in when an offset unit is given without a value, for example "in a few days", "a couple of hours", etc.
        /// </summary>
        public Dictionary<TemporalUnit, int> DefaultValueOfVagueOffset { get; set; }

        /// <summary>
        /// Provides customization of default times for different points in time of various parts of a day
        /// </summary>
        public Dictionary<PartOfDay, PartOfDayDefaultTimes> PartOfDayDefaultTimes
        {
            get
            {
                return this.partOfDayDefaultTimes;
            }
        }

        /// <summary>
        /// Provides customization of default values for vague offset keywords such as "a few" per TemporalUnit
        /// </summary>
        /// <returns></returns>
        internal Dictionary<TemporalUnit, int> CreateDefaultVagueOffsetDictionary()
        {
            var defaultVagueOffsetDictionary = new Dictionary<TemporalUnit, int>();
            foreach (TemporalUnit unit in Enum.GetValues(typeof(TemporalUnit)))
            {
                defaultVagueOffsetDictionary[unit] = 3;
            }

            return defaultVagueOffsetDictionary;
        }

        /// <summary>
        /// Writes this context's data to a binary writer
        /// </summary>
        /// <param name="writer">An opened binary writer</param>
        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "Unused internal but could be needed for public access in future")]
        internal void Serialize(BinaryWriter writer)
        {
            writer.Write(ReferenceDateTime.ToBinary());
            writer.Write((int)TemporalType);
            writer.Write((int)Normalization);
            writer.Write(UseInference);
            writer.Write(AmPmInferenceCutoff);
            writer.Write(IncludeCurrentTimeInPastOrFuture);
            writer.Write((int)WeekdayLogicType);
            writer.Write(WeekDefinition.FirstDayOfWeek);
            writer.Write(WeekDefinition.FirstDayOfWeekend);
            writer.Write(WeekDefinition.WeekendLength);
        }

        /// <summary>
        /// Creates a new context from the information in a binary reader
        /// </summary>
        /// <param name="reader">An opened binary reader</param>
        /// <returns>A newly deserialized timexcontext</returns>
        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "Unused internal but could be needed for public access in future")]
        internal static TimexContext Deserialize(BinaryReader reader)
        {
            TimexContext context = new TimexContext();
            context.ReferenceDateTime = DateTime.FromBinary(reader.ReadInt64());
            context.TemporalType = (TemporalType)reader.ReadInt32();
            context.Normalization = (Normalization)reader.ReadInt32();
            context.UseInference = reader.ReadBoolean();
            context.AmPmInferenceCutoff = reader.ReadInt32();
            context.IncludeCurrentTimeInPastOrFuture = reader.ReadBoolean();
            context.WeekdayLogicType = (WeekdayLogic)reader.ReadInt32();
            int firstDayOfWeek = reader.ReadInt32();
            int firstDayOfWeekend = reader.ReadInt32();
            int weekendLength = reader.ReadInt32();
            context.WeekDefinition = new LocalizedWeekDefinition(firstDayOfWeek, firstDayOfWeekend, weekendLength);
            return context;
        }
    }
}
