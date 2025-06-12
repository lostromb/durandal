using System;

namespace Durandal.Common.MathExt
{
    public struct Cube3f : IEquatable<Cube3f>
    {
        private float minX;
        private float minY;
        private float minZ;
        private float maxX;
        private float maxY;
        private float maxZ;

        public Cube3f(float x, float y, float z, float width, float height, float depth)
        {
            this.minX = x;
            this.minY = y;
            this.minZ = z;
            this.maxX = x + width;
            this.maxY = y + height;
            this.maxZ = z + depth;
        }

        public float X
        {
            get
            {
                return this.minX;
            }
        }

        public float Y
        {
            get
            {
                return this.minY;
            }
        }

        public float Z
        {
            get
            {
                return this.minZ;
            }
        }

        public float MaxX
        {
            get
            {
                return this.maxX;
            }
        }

        public float MaxY
        {
            get
            {
                return this.maxY;
            }
        }

        public float MaxZ
        {
            get
            {
                return this.maxZ;
            }
        }

        public float Width
        {
            get
            {
                return this.maxX - this.minX;
            }
        }

        public float Height
        {
            get
            {
                return this.maxY - this.minY;
            }
        }

        public float Depth
        {
            get
            {
                return this.maxZ - this.minZ;
            }
        }

        public float CenterX
        {
            get
            {
                return this.minX + (this.maxX - this.minX) / 2;
            }
        }

        public float CenterY
        {
            get
            {
                return this.minY + (this.maxY - this.minY) / 2;
            }
        }

        public float CenterZ
        {
            get
            {
                return this.minZ + (this.maxZ - this.minZ) / 2;
            }
        }

        public bool Intersects(Cube3f other)
        {
            throw new NotImplementedException();
        }

        public bool Contains(Cube3f cube)
        {
            return cube.X >= this.X &&
                    cube.Y >= this.Y &&
                    cube.Z >= this.Z &&
                    cube.MaxX < this.MaxX &&
                    cube.MaxY < this.MaxY &&
                    cube.MaxZ < this.MaxZ;
        }

        public bool Contains(Vector3f point)
        {
            return point.X >= this.X &&
                    point.Y >= this.Y &&
                    point.Z >= this.Z &&
                    point.X < this.MaxX &&
                    point.Y < this.MaxY &&
                    point.Z < this.MaxZ;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Cube3f))
            {
                return false;
            }

            Cube3f other = (Cube3f)obj;
            return Equals(other);
        }

        public bool Equals(Cube3f other)
        {
            return minX == other.minX &&
                   minY == other.minY &&
                   minZ == other.minZ &&
                   maxX == other.maxX &&
                   maxY == other.maxY &&
                   maxZ == other.maxZ;
        }

        public override int GetHashCode()
        {
            int hashCode = -1025513782;
            hashCode = hashCode * -1521134295 + minX.GetHashCode();
            hashCode = hashCode * -1521134295 + minY.GetHashCode();
            hashCode = hashCode * -1521134295 + minZ.GetHashCode();
            hashCode = hashCode * -1521134295 + maxX.GetHashCode();
            hashCode = hashCode * -1521134295 + maxY.GetHashCode();
            hashCode = hashCode * -1521134295 + maxZ.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Cube3f left, Cube3f right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Cube3f left, Cube3f right)
        {
            return !(left == right);
        }
    }
}
