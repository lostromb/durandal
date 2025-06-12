using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test
{
    /// <summary>
    /// IRandom implementation which returns a continually incrementing value
    /// </summary>
    public class FakeRandom : IRandom
    {
        private int _nextInteger = 0;
        private float _nextFloat = 0;

        public void SetNextInteger(int value)
        {
            _nextInteger = value;
            if (_nextInteger < 0)
            {
                throw new ArgumentOutOfRangeException("Value must be nonnegative");
            }
        }

        public void SetNextFloat(float value)
        {
            _nextFloat = value;
            if (_nextFloat >= 1.0f || _nextFloat < 0)
            {
                throw new ArgumentOutOfRangeException("Value must be between 0 and 1");
            }
        }

        public int NextInt()
        {
            return NextInt(0, int.MaxValue);
        }

        public int NextInt(int maxValue)
        {
            return NextInt(0, maxValue);
        }

        public int NextInt(int minValue, int maxValue)
        {
            int returnVal = (_nextInteger % (maxValue - minValue)) + minValue;
            _nextInteger++;
            if (_nextInteger < 0)
            {
                _nextInteger = 0;
            }
            
            return returnVal;
        }

        public void NextBytes(byte[] buffer)
        {
            NextBytes(buffer.AsSpan());
        }

        public void NextBytes(byte[] buffer, int offset, int count)
        {
            NextBytes(buffer.AsSpan(offset, count));
        }

        public void NextBytes(Span<byte> buffer)
        {
            for (int c = 0; c < buffer.Length; c++)
            {
                buffer[c] = (byte)(_nextInteger % 255);
                _nextInteger++;
            }
        }

        public double NextDouble()
        {
            return (double)NextFloat();
        }

        public float NextFloat()
        {
            float returnVal = _nextFloat;
            _nextFloat += 0.1f;
            if (_nextFloat >= 1.0f)
            {
                _nextFloat -= 1.0f;
            }
            
            return returnVal;
        }

        public long NextInt64()
        {
            throw new NotImplementedException();
        }

        public long NextInt64(long maxValue)
        {
            throw new NotImplementedException();
        }

        public long NextInt64(long minValue, long maxValue)
        {
            throw new NotImplementedException();
        }

        public double NextDouble(double min, double max)
        {
            throw new NotImplementedException();
        }

        public float NextFloat(float min, float max)
        {
            throw new NotImplementedException();
        }

        public bool NextBool()
        {
            throw new NotImplementedException();
        }
    }
}
