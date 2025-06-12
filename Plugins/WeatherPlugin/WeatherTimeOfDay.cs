namespace Durandal.Plugins.Weather
{
    using System;

    public enum WeatherTimeOfDay
    {
        Dawn,
        Day,
        Dusk,
        Night,
        Moon
    }

    public static class WeatherTimeOfDayExtensions
    {
        public static WeatherTimeOfDay Parse(string value)
        {
            if (string.Equals(value, "dawn"))
            {
                return WeatherTimeOfDay.Dawn;
            }

            if (string.Equals(value, "day"))
            {
                return WeatherTimeOfDay.Day;
            }

            if (string.Equals(value, "dusk"))
            {
                return WeatherTimeOfDay.Dusk;
            }

            if (string.Equals(value, "night"))
            {
                return WeatherTimeOfDay.Night;
            }

            if (string.Equals(value, "moon"))
            {
                return WeatherTimeOfDay.Moon;
            }

            return Parse(DateTime.Now);
        }

        public static WeatherTimeOfDay Parse(DateTimeOffset value)
        {
            if (value.Hour > 20 || value.Hour < 5)
            {
                return WeatherTimeOfDay.Night;
            }
            else if (value.Hour > 5 && value.Hour < 8)
            {
                return WeatherTimeOfDay.Dawn;
            }
            else if (value.Hour > 8 && value.Hour < 17)
            {
                return WeatherTimeOfDay.Day;
            }
            else if (value.Hour > 17 && value.Hour < 20)
            {
                return WeatherTimeOfDay.Dusk;
            }
            return WeatherTimeOfDay.Day;
        }
        
        public static float Difference(this WeatherTimeOfDay a, WeatherTimeOfDay other)
        {
            EditDistanceMatrix<WeatherTimeOfDay> distances = new EditDistanceMatrix<WeatherTimeOfDay>(1.0f);
            distances.AddPair(WeatherTimeOfDay.Day, WeatherTimeOfDay.Dawn, 0.4f);
            distances.AddPair(WeatherTimeOfDay.Day, WeatherTimeOfDay.Dusk, 0.4f);
            distances.AddPair(WeatherTimeOfDay.Night, WeatherTimeOfDay.Moon, 0.1f);
            distances.AddPair(WeatherTimeOfDay.Night, WeatherTimeOfDay.Dawn, 0.5f);
            distances.AddPair(WeatherTimeOfDay.Night, WeatherTimeOfDay.Dusk, 0.5f);
            distances.AddPair(WeatherTimeOfDay.Moon, WeatherTimeOfDay.Dawn, 0.6f);
            distances.AddPair(WeatherTimeOfDay.Moon, WeatherTimeOfDay.Dusk, 0.6f);
            return distances.GetDistance(a, other);
        }
    }
}
