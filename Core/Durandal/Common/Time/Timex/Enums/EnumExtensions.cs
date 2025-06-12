using System;
using System.Collections.Generic;
using System.Linq;

namespace Durandal.Common.Time.Timex.Enums
{
    public static class EnumExtensions
    {
        public static bool TryParse(string value, out DateTimeReference parsed)
        {
            return TryParse(value, DateTimeReferenceTable, out parsed);
        }

        public static bool TryParse(string value, out Holiday parsed)
        {
            return TryParse(value, HolidayTable, out parsed);
        }

        public static bool TryParse(string value, out TemporalUnit parsed)
        {
            return TryParse(value, TemporalUnitTable, out parsed);
        }

        public static bool TryParse(string value, out Modifier parsed)
        {
            return TryParse(value, ModifierTable, out parsed);
        }

        public static bool TryParse(string value, out Normalization parsed)
        {
            return TryParse(value, NormalizationTable, out parsed);
        }

        public static bool TryParse(string value, out PartOfDay parsed)
        {
            return TryParse(value, PartOfDayTable, out parsed);
        }

        public static bool TryParse(string value, out PartOfYear parsed)
        {
            return TryParse(value, PartOfYearTable, out parsed);
        }

        public static bool TryParse(string value, out Season parsed)
        {
            return TryParse(value, SeasonTable, out parsed);
        }

        public static bool TryParse(string value, out TemporalType parsed)
        {
            return TryParse(value, TemporalTypeTable, out parsed);
        }

        public static bool TryParse(string value, out WeekdayLogic parsed)
        {
            return TryParse(value, WeekdayLogicTable, out parsed);
        }

        public static string ToString(DateTimeReference value)
        {
            return ToString(value, DateTimeReferenceTable);
        }

        public static string ToString(Holiday value)
        {
            return ToString(value, HolidayTable);
        }

        public static string ToString(Modifier value)
        {
            return ToString(value, ModifierTable);
        }

        public static string ToString(Normalization value)
        {
            return ToString(value, NormalizationTable);
        }

        public static string ToString(PartOfDay value)
        {
            return ToString(value, PartOfDayTable);
        }

        public static string ToString(PartOfYear value)
        {
            return ToString(value, PartOfYearTable);
        }
        public static string ToString(Season value)
        {
            return ToString(value, SeasonTable);
        }

        public static string ToString(TemporalType value)
        {
            return ToString(value, TemporalTypeTable);
        }

        public static string ToString(TemporalUnit value)
        {
            return ToString(value, TemporalUnitTable);
        }

        public static string ToString(WeekdayLogic value)
        {
            return ToString(value, WeekdayLogicTable);
        }

        private static Tuple<string, DateTimeReference>[] DateTimeReferenceTable = new Tuple<string, DateTimeReference>[]
        {
            new Tuple<string, DateTimeReference>("PRESENT_REF", DateTimeReference.Present),
            new Tuple<string, DateTimeReference>("FUTURE_REF", DateTimeReference.Future),
            new Tuple<string, DateTimeReference>("PAST_REF", DateTimeReference.Past),
        };

        private static Tuple<string, Holiday>[] HolidayTable = new Tuple<string, Holiday>[]
        {
            new Tuple<string, Holiday>("EasterSunday", Holiday.EasterSunday),
            new Tuple<string, Holiday>("ChineseNewYear", Holiday.ChineseNewYear),
            new Tuple<string, Holiday>("Diwali", Holiday.Diwali),
            new Tuple<string, Holiday>("Passover", Holiday.Passover),
            new Tuple<string, Holiday>("Hanukkah", Holiday.Hanukkah),
            new Tuple<string, Holiday>("RoshHashanah", Holiday.RoshHashanah),
        };

        private static Tuple<string, Modifier>[] ModifierTable = new Tuple<string, Modifier>[]
        {
            //new Tuple<string, Modifier>("", Modifier.None),
            new Tuple<string, Modifier>("APPROX", Modifier.Approximately),
            new Tuple<string, Modifier>("EQUAL_OR_LESS", Modifier.EqualOrLess),
            new Tuple<string, Modifier>("EQUAL_OR_MORE", Modifier.EqualOrMore),
            new Tuple<string, Modifier>("LESS_THAN", Modifier.LessThan),
            new Tuple<string, Modifier>("MORE_THAN", Modifier.MoreThan),
            new Tuple<string, Modifier>("MID", Modifier.Mid),
            new Tuple<string, Modifier>("END", Modifier.End),
            new Tuple<string, Modifier>("START", Modifier.Start),
            new Tuple<string, Modifier>("BEFORE", Modifier.Before),
            new Tuple<string, Modifier>("AFTER", Modifier.After),
        };

        private static Tuple<string, Normalization>[] NormalizationTable = new Tuple<string, Normalization>[]
        {
            new Tuple<string, Normalization>("Present", Normalization.Present),
            new Tuple<string, Normalization>("Future", Normalization.Future),
            new Tuple<string, Normalization>("Past", Normalization.Past),
        };

        private static Tuple<string, PartOfDay>[] PartOfDayTable = new Tuple<string, PartOfDay>[]
        {
            new Tuple<string, PartOfDay>("MO", PartOfDay.Morning),
            new Tuple<string, PartOfDay>("MI", PartOfDay.MidDay),
            new Tuple<string, PartOfDay>("AF", PartOfDay.Afternoon),
            new Tuple<string, PartOfDay>("EV", PartOfDay.Evening),
            new Tuple<string, PartOfDay>("NI", PartOfDay.Night),
            new Tuple<string, PartOfDay>("PM", PartOfDay.Pm),
            new Tuple<string, PartOfDay>("DT", PartOfDay.DayTime),
            new Tuple<string, PartOfDay>("12:00:00", PartOfDay.Noon),
            new Tuple<string, PartOfDay>("12:00", PartOfDay.Noon),
            new Tuple<string, PartOfDay>("24:00:00", PartOfDay.Midnight),
            new Tuple<string, PartOfDay>("24:00", PartOfDay.Midnight),
        };

        private static Tuple<string, PartOfYear>[] PartOfYearTable = new Tuple<string, PartOfYear>[]
        {
            new Tuple<string, PartOfYear>("Q1", PartOfYear.FirstQuarter),
            new Tuple<string, PartOfYear>("Q2", PartOfYear.SecondQuarter),
            new Tuple<string, PartOfYear>("Q3", PartOfYear.ThirdQuarter),
            new Tuple<string, PartOfYear>("Q4", PartOfYear.FourthQuarter),
            new Tuple<string, PartOfYear>("H1", PartOfYear.FirstHalf),
            new Tuple<string, PartOfYear>("H2", PartOfYear.SecondHalf),
        };

        private static Tuple<string, Season>[] SeasonTable = new Tuple<string, Season>[]
        {
            new Tuple<string, Season>("WI", Season.Winter),
            new Tuple<string, Season>("SP", Season.Spring),
            new Tuple<string, Season>("SU", Season.Summer),
            new Tuple<string, Season>("FA", Season.Fall),
        };

        private static Tuple<string, TemporalType>[] TemporalTypeTable = new Tuple<string, TemporalType>[]
        {
            new Tuple<string, TemporalType>("Date", TemporalType.Date),
            new Tuple<string, TemporalType>("Time", TemporalType.Time),
            new Tuple<string, TemporalType>("Set", TemporalType.Set),
            new Tuple<string, TemporalType>("Duration", TemporalType.Duration),
        };

        private static Tuple<string, TemporalUnit>[] TemporalUnitTable = new Tuple<string, TemporalUnit>[]
        {
            new Tuple<string, TemporalUnit>("year", TemporalUnit.Year),
            new Tuple<string, TemporalUnit>("month", TemporalUnit.Month),
            new Tuple<string, TemporalUnit>("week", TemporalUnit.Week),
            new Tuple<string, TemporalUnit>("weekend", TemporalUnit.Weekend),
            new Tuple<string, TemporalUnit>("weekdays", TemporalUnit.Weekdays),
            new Tuple<string, TemporalUnit>("businessday", TemporalUnit.BusinessDay),
            new Tuple<string, TemporalUnit>("fortnight", TemporalUnit.Fortnight),
            new Tuple<string, TemporalUnit>("decade", TemporalUnit.Decade),
            new Tuple<string, TemporalUnit>("century", TemporalUnit.Century),
            new Tuple<string, TemporalUnit>("quarter", TemporalUnit.Quarter),
            new Tuple<string, TemporalUnit>("monday", TemporalUnit.Monday),
            new Tuple<string, TemporalUnit>("tuesday", TemporalUnit.Tuesday),
            new Tuple<string, TemporalUnit>("wednesday", TemporalUnit.Wednesday),
            new Tuple<string, TemporalUnit>("thursday", TemporalUnit.Thursday),
            new Tuple<string, TemporalUnit>("friday", TemporalUnit.Friday),
            new Tuple<string, TemporalUnit>("saturday", TemporalUnit.Saturday),
            new Tuple<string, TemporalUnit>("sunday", TemporalUnit.Sunday),
            new Tuple<string, TemporalUnit>("day", TemporalUnit.Day),
            new Tuple<string, TemporalUnit>("hour", TemporalUnit.Hour),
            new Tuple<string, TemporalUnit>("minute", TemporalUnit.Minute),
            new Tuple<string, TemporalUnit>("second", TemporalUnit.Second),
        };

        private static Tuple<string, WeekdayLogic>[] WeekdayLogicTable = new Tuple<string, WeekdayLogic>[]
        {
            new Tuple<string, WeekdayLogic>("Programmatic", WeekdayLogic.Programmatic),
            new Tuple<string, WeekdayLogic>("SimpleOffset", WeekdayLogic.SimpleOffset),
            new Tuple<string, WeekdayLogic>("WeekBoundary", WeekdayLogic.WeekBoundary),
        };

        private static bool TryParse<T>(string value, Tuple<string, T>[] parseTable, out T parsed) where T : struct
        {
            foreach (var entry in parseTable)
            {
                if (entry.Item1.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    parsed = entry.Item2;
                    return true;
                }
            }
            
            parsed = default(T);
            return false;

            // Fall back to raw enum parsing
            // actually NO because this will parse numbers as ordinals and we don't want that
            //return Enum.TryParse<T>(value, out parsed);
        }

        private static string ToString<T>(T value, Tuple<string, T>[] parseTable) where T : struct
        {
            foreach (var entry in parseTable)
            {
                if (entry.Item2.Equals(value))
                {
                    return entry.Item1;
                }
            }

            // Fall back to raw enum tostring
            return value.ToString();
        }
    }
}
