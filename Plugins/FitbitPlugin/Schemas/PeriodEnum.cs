using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public enum PeriodEnum
    {
        Unknown,
        OneDay,
        OneWeek,
        SevenDays,
        ThirtyDays,
        OneMonth,
        ThreeMonths,
        SixMonths,
        OneYear
    }

    public static class PeriodEnumFormatters
    {
        public static string ToIsoString(this PeriodEnum e)
        {
            switch (e)
            {
                case PeriodEnum.OneDay:
                    return "1d";
                case PeriodEnum.OneWeek:
                    return "1w";
                case PeriodEnum.SevenDays:
                    return "7d";
                case PeriodEnum.ThirtyDays:
                    return "30d";
                case PeriodEnum.OneMonth:
                    return "1m";
                case PeriodEnum.ThreeMonths:
                    return "3m";
                case PeriodEnum.SixMonths:
                    return "6m";
                case PeriodEnum.OneYear:
                    return "1y";
            }

            return string.Empty;
        }
    }
}
