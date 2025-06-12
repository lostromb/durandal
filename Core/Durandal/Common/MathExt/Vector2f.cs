namespace Durandal.Common.MathExt
{
    /// <summary>
    /// Represents a vector in 2 dimensions with 32-bit precision
    /// </summary>
    public struct Vector2f : System.IEquatable<Vector2f>
    {
        public float X;
        public float Y;

        public Vector2f(float x, float y)
        {
            X = x;
            Y = y;
        }

        public void SetLocation(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float Distance(Vector2f other)
        {
            float dx = other.X - X;
            float dy = other.Y - Y;
            return (float)System.Math.Sqrt((dx * dx) + (dy * dy));
        }

        public Vector2f Add(Vector2f other)
        {
            return Add(other.X, other.Y);
        }

        public Vector2f Add(float x, float y)
        {
            return new Vector2f(X + x, Y + y);
        }

        public Vector2f Subtract(Vector2f other)
        {
            return Subtract(other.X, other.Y);
        }

        public Vector2f Subtract(float x, float y)
        {
            return new Vector2f(X - x, Y - y);
        }

        public Vector2f Multiply(Vector2f other)
        {
            return Multiply(other.X, other.Y);
        }

        public Vector2f Multiply(float x, float y)
        {
            return new Vector2f(X * x, Y * y);
        }

        public Vector2f Multiply(float w)
        {
            return new Vector2f(X * w, Y * w);
        }

        public float Magnitude
        {
            get
            {
                return (float)System.Math.Sqrt((X * X) + (Y * Y));
            }
            set
            {
                if (X != 0 || Y != 0)
                {
                    float factor = value / Magnitude;
                    X *= factor;
                    Y *= factor;
                }
            }
        }

        public Vector2f Clone()
        {
            return new Vector2f(X, Y);
        }

        public static Vector2f Zero
        {
            get
            {
                return new Vector2f(0, 0);
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Vector2f))
            {
                return false;
            }

            Vector2f other = (Vector2f)obj;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() +
                Y.GetHashCode();
        }

        public static bool operator ==(Vector2f left, Vector2f right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2f left, Vector2f right)
        {
            return !(left == right);
        }

        public bool Equals(Vector2f other)
        {
            return X == other.X &&
                Y == other.Y;
        }
    }
}
