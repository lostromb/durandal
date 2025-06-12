using System;
using System.Xml.Linq;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Represents TIMEX3 tag from TimeML specification
    /// Specification: http://www.timeml.org/site/publications/timeMLdocs/timeml_1.2.1.html
    /// Annotation guideline: http://www.timeml.org/site/publications/timeMLdocs/annguide_1.2.1.pdf
    /// </summary>
    public class TimexTag
    {
        /// <summary>
        /// Original time expression
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Timex Id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Timex type. One of four types: DATE, TIME, DURATION, SET
        /// </summary>
        public string TimexType { get; set; }

        /// <summary>
        /// Timex value. Contains ISO 8601 formatted DateTime string
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string Mod { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string TemporalFunction { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string AnchorTimeId { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string ValueFromFunction { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string FunctionInDocument { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string BeginPoint { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string EndPoint { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string Quantity { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string Frequency { get; set; }

        /// <summary>
        /// Optional property
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Returns a string that represents the current TimexTag
        /// </summary>
        /// <returns>A string that represents the current TimexTag</returns>
        public override string ToString()
        {
            var tag = new XElement("TIMEX3");

            if (!string.IsNullOrEmpty(Id))
                tag.Add(new XAttribute("tid", Id));

            if (!string.IsNullOrEmpty(TimexType))
                tag.Add(new XAttribute("type", TimexType));

            if (!string.IsNullOrEmpty(Mod))
                tag.Add(new XAttribute("mod", Mod));

            if (!string.IsNullOrEmpty(Value))
                tag.Add(new XAttribute("value", Value));

            if (!string.IsNullOrEmpty(TemporalFunction))
                tag.Add(new XAttribute("temporalFunction", TemporalFunction));

            if (!string.IsNullOrEmpty(AnchorTimeId))
                tag.Add(new XAttribute("anchorTimeId", AnchorTimeId));

            if (!string.IsNullOrEmpty(ValueFromFunction))
                tag.Add(new XAttribute("valueFromFunction", ValueFromFunction));

            if (!string.IsNullOrEmpty(FunctionInDocument))
                tag.Add(new XAttribute("functionInDocument", FunctionInDocument));

            if (!string.IsNullOrEmpty(FunctionInDocument))
                tag.Add(new XAttribute("functionInDocument", FunctionInDocument));

            if (!string.IsNullOrEmpty(BeginPoint))
                tag.Add(new XAttribute("beginPoint", BeginPoint));

            if (!string.IsNullOrEmpty(EndPoint))
                tag.Add(new XAttribute("endPoint", EndPoint));

            if (!string.IsNullOrEmpty(Quantity))
                tag.Add(new XAttribute("quant", Quantity));

            if (!string.IsNullOrEmpty(Frequency))
                tag.Add(new XAttribute("frequency", Frequency));

            if (!string.IsNullOrEmpty(Comment))
                tag.Add(new XAttribute("comment", Comment));

            tag.Value = Text;

            return tag.ToString();
        }
    }
}
