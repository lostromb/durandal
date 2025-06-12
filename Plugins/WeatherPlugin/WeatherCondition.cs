namespace Durandal.Plugins.Weather
{
    public enum WeatherCondition
    {
        Clear,
        Cloudy,
        Fog,
        Rain,
        Snow,
        MostlyCloudy,
        PartlyCloudy,
        Ice,
        Heat,
        Dust,
        Sleet,
        Thunder,
        ChanceRain,
        ChanceSleet,
        ChanceSnow,
        ChanceThunder,
        Wind,
        Unknown
    }

    public static class WeatherConditionExtensions
    {
        public static WeatherCondition Parse(string value)
        {
            if (string.Equals(value, "clear-day") ||
                string.Equals(value, "clear-night") ||
                string.Equals(value, "clear") ||
                string.Equals(value, "sun") ||
                string.Equals(value, "sunny"))
            {
                return WeatherCondition.Clear;
            }

            if (string.Equals(value, "overcast") ||
                string.Equals(value, "cloudy"))
            {
                return WeatherCondition.Cloudy;
            }

            if (string.Equals(value, "fog") ||
                string.Equals(value, "haze"))
            {
                return WeatherCondition.Fog;
            }

            if (string.Equals(value, "rain"))
            {
                return WeatherCondition.Rain;
            }

            if (string.Equals(value, "flurries") ||
                string.Equals(value, "snow"))
            {
                return WeatherCondition.Snow;
            }

            if (string.Equals(value, "mostlycloudy") ||
                string.Equals(value, "partlysunny"))
            {
                return WeatherCondition.MostlyCloudy;
            }

            if (string.Equals(value, "partly-cloudy-day") ||
                string.Equals(value, "partly-cloudy-night") || 
                string.Equals(value, "partlycloudy") ||
                string.Equals(value, "partcloud") ||
                string.Equals(value, "mostlysunny"))
            {
                return WeatherCondition.PartlyCloudy;
            }

            if (string.Equals(value, "ice"))
            {
                return WeatherCondition.Ice;
            }

            if (string.Equals(value, "wind"))
            {
                return WeatherCondition.Wind;
            }

            if (string.Equals(value, "heat"))
            {
                return WeatherCondition.Heat;
            }

            if (string.Equals(value, "dust"))
            {
                return WeatherCondition.Dust;
            }

            if (string.Equals(value, "sleet"))
            {
                return WeatherCondition.Sleet;
            }

            if (string.Equals(value, "tstorms"))
            {
                return WeatherCondition.Thunder;
            }

            if (string.Equals(value, "chancerain"))
            {
                return WeatherCondition.ChanceRain;
            }

            if (string.Equals(value, "chancesleet"))
            {
                return WeatherCondition.ChanceSleet;
            }

            if (string.Equals(value, "chancesnow") ||
                string.Equals(value, "chanceflurries"))
            {
                return WeatherCondition.ChanceSnow;
            }

            if (string.Equals(value, "chancetstorms"))
            {
                return WeatherCondition.ChanceThunder;
            }

            return WeatherCondition.Unknown;
    }
        
        public static float Difference(this WeatherCondition a, WeatherCondition other)
        {
            EditDistanceMatrix<WeatherCondition> distances = new EditDistanceMatrix<WeatherCondition>(1.0f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.PartlyCloudy, 0.2f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.MostlyCloudy, 0.3f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Cloudy, 0.6f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Rain, 0.8f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Dust, 0.7f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Fog, 0.7f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Heat, 0.1f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Ice, 0.1f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Sleet, 0.9f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Snow, 0.7f);
            distances.AddPair(WeatherCondition.Clear, WeatherCondition.Thunder, 0.9f);

            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.MostlyCloudy, 0.2f);
            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.Cloudy, 0.5f);
            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.Dust, 0.4f);
            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.Fog, 0.3f);
            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.Heat, 0.3f);
            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.Ice, 0.2f);
            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.Rain, 0.6f);
            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.Snow, 0.9f);
            distances.AddPair(WeatherCondition.PartlyCloudy, WeatherCondition.Thunder, 0.5f);

            distances.AddPair(WeatherCondition.MostlyCloudy, WeatherCondition.Cloudy, 0.1f);
            distances.AddPair(WeatherCondition.MostlyCloudy, WeatherCondition.Dust, 0.3f);
            distances.AddPair(WeatherCondition.MostlyCloudy, WeatherCondition.Fog, 0.2f);
            distances.AddPair(WeatherCondition.MostlyCloudy, WeatherCondition.Heat, 0.6f);
            distances.AddPair(WeatherCondition.MostlyCloudy, WeatherCondition.Ice, 0.4f);
            distances.AddPair(WeatherCondition.MostlyCloudy, WeatherCondition.Sleet, 0.4f);
            distances.AddPair(WeatherCondition.MostlyCloudy, WeatherCondition.Snow, 0.3f);
            distances.AddPair(WeatherCondition.MostlyCloudy, WeatherCondition.Thunder, 0.4f);

            distances.AddPair(WeatherCondition.Cloudy, WeatherCondition.Dust, 0.3f);
            distances.AddPair(WeatherCondition.Cloudy, WeatherCondition.Fog, 0.1f);
            distances.AddPair(WeatherCondition.Cloudy, WeatherCondition.Heat, 0.9f);
            distances.AddPair(WeatherCondition.Cloudy, WeatherCondition.Ice, 0.5f);
            distances.AddPair(WeatherCondition.Cloudy, WeatherCondition.Sleet, 0.3f);
            distances.AddPair(WeatherCondition.Cloudy, WeatherCondition.Snow, 0.2f);
            distances.AddPair(WeatherCondition.Cloudy, WeatherCondition.Thunder, 0.3f);

            distances.AddPair(WeatherCondition.Dust, WeatherCondition.Fog, 0.2f);

            distances.AddPair(WeatherCondition.Ice, WeatherCondition.Sleet, 0.1f);
            distances.AddPair(WeatherCondition.Ice, WeatherCondition.Snow, 0.2f);

            distances.AddPair(WeatherCondition.Snow, WeatherCondition.Sleet, 0.2f);
            distances.AddPair(WeatherCondition.Snow, WeatherCondition.Fog, 0.5f);

            return distances.GetDistance(a, other);
        }
    }
}
