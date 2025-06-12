namespace Durandal.Plugins.Weather
{
    using System;
    using System.Collections.Generic;

    public class EditDistanceMatrix<T>
    {
        private readonly float _defaultValue;
        private IDictionary<Tuple<T, T>, float> _pairs;
        
        public EditDistanceMatrix(float defaultValue)
        {
            _defaultValue = defaultValue;
            _pairs = new Dictionary<Tuple<T, T>, float>();
        }

        public float GetDistance(T one, T two)
        {
            if (one.Equals(two))
                return 0.0f;

            Tuple<T, T> forward = new Tuple<T, T>(one, two);
            if (_pairs.ContainsKey(forward))
            {
                return _pairs[forward];
            }

            return _defaultValue;
        }

        public void AddPair(T one, T two, float weight)
        {
            Tuple<T, T> forward = new Tuple<T, T>(one, two);
            Tuple<T, T> backward = new Tuple<T, T>(two, one);
            if (_pairs.ContainsKey(forward) || _pairs.ContainsKey(backward))
                return;
            _pairs[forward] = weight;
            _pairs[backward] = weight;
        }
    }
}
