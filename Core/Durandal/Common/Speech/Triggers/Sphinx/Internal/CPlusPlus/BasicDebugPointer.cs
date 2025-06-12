﻿
//namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus
//{
//    using System;
//    using System.Diagnostics;
//    using System.Runtime.CompilerServices;
//    using System.Text;

//    #region Generic pointers

//    /// <summary>
//    /// This simulates a C++ style pointer as far as can be implemented in C#. It represents a handle
//    /// to an array of objects, along with a base offset that represents the address.
//    /// When you are programming in debug mode, this class also enforces memory boundaries,
//    /// tracks uninitialized values, and also records all statistics of accesses to its base array.
//    /// </summary>
//    /// <typeparam name="T"></typeparam>
//    internal struct BasicDebugPointer<T> : Pointer<T>
//    {
//        private T[] _array;
//        private int _offset;

//        /// <summary>
//        /// For internal use only!
//        /// </summary>
//        public int Offset => _offset;

//        /// <summary>
//        /// For internal use only!
//        /// </summary>
//        public T[] Data => _array;

//        private static void Assert(bool condition, string message)
//        {
//            if (!condition)
//            {
//                throw new Exception("Assertion error:" + message);
//            }
//        }

//        private bool[] _initialized;
//        private PointerStatistics _statistics;
//        private int _length;

//        public BasicDebugPointer(int capacity)
//        {
//            _array = new T[capacity];
//            _offset = 0;
//            _length = capacity;
//            _statistics = new PointerStatistics(0);
//            _initialized = new bool[capacity];
//            for (int c = 0; c < capacity; c++)
//            {
//                _initialized[c] = false;
//            }
//        }

//        public BasicDebugPointer(T[] buffer)
//        {
//            _array = buffer;
//            _offset = 0;
//            _length = buffer.Length;
//            _statistics = new PointerStatistics(0);
//            _initialized = new bool[buffer.Length];
//            for (int c = 0; c < buffer.Length; c++)
//            {
//                _initialized[c] = true;
//            }
//        }

//        public BasicDebugPointer(T[] buffer, int absoluteOffset)
//        {
//            _array = buffer;
//            _offset = absoluteOffset;
//            _length = buffer.Length - absoluteOffset;
//            Assert(_length >= 0, "Attempted to point past the end of an array");
//            _statistics = new PointerStatistics(absoluteOffset);
//            _initialized = new bool[buffer.Length];
//            for (int c = 0; c < buffer.Length; c++)
//            {
//                _initialized[c] = true;
//            }
//        }

//        public BasicDebugPointer(T[] buffer, uint absoluteOffset) : this(buffer, (int)absoluteOffset)
//        {
//        }

//        private BasicDebugPointer(T[] buffer, int absoluteOffset, PointerStatistics statistics, bool[] initializedStatus)
//        {
//            _array = buffer;
//            _offset = absoluteOffset;
//            _length = buffer.Length - absoluteOffset;
//            Assert(_length >= 0, "Attempted to point past the end of an array");
//            _statistics = statistics;
//            _initialized = initializedStatus;
//        }

//        public Tuple<int, int> ReadRange => _statistics.ReadRange;
//        public Tuple<int, int> WriteRange => _statistics.WriteRange;
//        public int Length => _length;

//        public T this[int index]
//        {
//            get
//            {
//                Assert(_initialized[index + _offset], "Attempted to read from uninitialized memory!");
//                Assert(index < _length, "Attempted to read past the end of an array!");
//                _statistics.maxReadIndex = Math.Max(_statistics.maxReadIndex, index + _offset);
//                _statistics.minReadIndex = Math.Min(_statistics.minReadIndex, index + _offset);
//                return _array[index + _offset];
//            }

//            set
//            {
//                Assert(index < _length, "Attempted to write past the end of an array!");
//                _statistics.maxWriteIndex = Math.Max(_statistics.maxWriteIndex, index + _offset);
//                _statistics.minWriteIndex = Math.Min(_statistics.minWriteIndex, index + _offset);
//                _initialized[index + _offset] = true;
//                _array[index + _offset] = value;
//            }
//        }

//        public T this[uint index]
//        {
//            get
//            {
//                return this[(int)index];
//            }

//            set
//            {
//                this[(int)index] = value;
//            }
//        }

//        public T Deref
//        {
//            get
//            {
//                return this[0];
//            }
//            set
//            {
//                this[0] = value;
//            }
//        }

//        /// <summary>
//        /// Overload operator + will dereference the pointer (I would have used * but that can't be overridden as a unary operator)
//        /// </summary>
//        /// <param name="t"></param>
//        /// <returns></returns>
//        internal static T operator +(BasicDebugPointer<T> t)
//        {
//            return t[0];
//        }

//        internal static BasicDebugPointer<T> operator ++(BasicDebugPointer<T> t)
//        {
//            return t.Point(1);
//        }

//        internal static BasicDebugPointer<T> operator --(BasicDebugPointer<T> t)
//        {
//            return t.Point(-1);
//        }

//        /// <summary>
//        /// Used when testing the pointer object as a boolean, e.g. "if (ptr) { }".
//        /// Indicates whether this pointer refers to valid data or not
//        /// </summary>
//        /// <param name="x"></param>
//        /// <returns></returns>
//        public bool IsNonNull => Data != null;
//        public bool IsNull => Data == null;

//        /// <summary>
//        /// Returns the value currently under the pointer, and returns a new pointer with +1 offset.
//        /// This method is not very efficient because it creates new pointers; this is because we must preserve
//        /// the pass-by-value nature of C++ pointers when they are used as arguments to functions
//        /// </summary>
//        /// <returns></returns>
//        public Pointer<T> Iterate(out T returnVal)
//        {
//            returnVal = _array[_offset];
//            return Point(1);
//        }

//#if DEBUG
//        public Pointer<T> Point(int relativeOffset)
//        {
//            if (relativeOffset == 0) return this;
//            return new BasicDebugPointer<T>(_array, _offset + relativeOffset, _statistics, _initialized);
//        }

//        public Pointer<T> Point(uint relativeOffset)
//        {
//            if (relativeOffset == 0) return this;
//            return new BasicDebugPointer<T>(_array, _offset + (int)relativeOffset, _statistics, _initialized);
//        }
//#else
//        public Pointer<T> Point(int relativeOffset)
//        {
//            if (relativeOffset == 0) return this;
//            return new Pointer<T>(_array, _offset + relativeOffset);
//        }

//        public Pointer<T> Point(uint relativeOffset)
//        {
//            if (relativeOffset == 0) return this;
//            return new Pointer<T>(_array, _offset + (int)relativeOffset);
//        }
//#endif

//        private static string invert_endianness(string hexstring)
//        {
//            StringBuilder b = new StringBuilder(hexstring.Length);
//            for (int c = 0; c < hexstring.Length / 2; c++)
//            {
//                b.Append(hexstring.Substring(hexstring.Length - ((c + 1) * 2), 2));
//            }
//            return b.ToString();
//        }

//        private static void PrintMemCopy<E>(E[] source, int sourceOffset, int length)
//        {
//            if (typeof(E) == typeof(int) || typeof(E) == typeof(uint))
//            {
//                Debug.WriteLine(string.Format("memcpy of {0} bytes", length * 4));
//                string buf = string.Empty;
//                for (int c = 0; c < length; c++)
//                {
//                    buf += invert_endianness(string.Format("{0:x8}", source[c + sourceOffset]));
//                }
//                Debug.WriteLine(buf);
//            }
//            else if (typeof(E) == typeof(short) || typeof(E) == typeof(ushort))
//            {
//                Debug.WriteLine(string.Format("memcpy of {0} bytes", length * 2));
//                string buf = string.Empty;
//                for (int c = 0; c < length; c++)
//                {
//                    buf += invert_endianness(string.Format("{0:x4}", source[c + sourceOffset]));
//                }
//                Debug.WriteLine(buf);
//            }
//            else if (typeof(E) == typeof(byte) || typeof(E) == typeof(sbyte))
//            {
//                Debug.WriteLine(string.Format("memcpy of {0} bytes", length));
//                string buf = string.Empty;
//                for (int c = 0; c < length; c++)
//                {
//                    buf += invert_endianness(string.Format("{0:x2}", source[c + sourceOffset]));
//                }
//                Debug.WriteLine(buf);
//            }
//        }

//        /// <summary>
//        /// Copies the contents of this pointer, starting at its current address, into the space of another pointer.
//        /// !!! IMPORTANT !!! REMEMBER THAT C++ memcpy is (DEST, SOURCE, LENGTH) !!!!
//        /// IN C# IT IS (SOURCE, DEST, LENGTH). DON'T GET SCOOPED LIKE I DID
//        /// </summary>
//        /// <param name="destination"></param>
//        /// <param name="length"></param>
//#if DEBUG
//        public void MemCopyTo(Pointer<T> destination, int length, bool debug = false)
//        {
//            Assert(length >= 0, "Cannot memcopy() with a negative length!");
//            if (debug)
//                PrintMemCopy(_array, _offset, length);

//            for (int c = 0; c < length; c++)
//            {
//                destination[c] = _array[c + _offset];
//            }
//        }
//#else
//        public void MemCopyTo(Pointer<T> destination, int length)
//        {
//            if (destination is Pointer<T>)
//            {
//                // Use the fast way if we have access to the base array
//                Array.Copy(_array, _offset, ((Pointer<T>)destination)._array, destination.Offset, length);
//            }
//            else
//            {
//                // Otherwise do it the slow way
//                for (int c = 0; c < length; c++)
//                {
//                    destination[c] = _array[c + _offset];
//                }
//            }
//        }
//#endif

//        /// <summary>
//        /// Copies the contents of this pointer, starting at its current address, into an array.
//        /// !!! IMPORTANT !!! REMEMBER THAT C++ memcpy is (DEST, SOURCE, LENGTH) !!!!
//        /// </summary>
//        /// <param name="destination"></param>
//        /// <param name="length"></param>
//#if DEBUG
//        public void MemCopyTo(T[] destination, int destOffset, int length)
//        {
//            Assert(length >= 0, "Cannot memcopy() with a negative length!");
//            //PrintMemCopy(_array, _offset, length);
//            for (int c = 0; c < length; c++)
//            {
//                destination[c + destOffset] = _array[c + _offset];
//            }
//        }
//#else
//        public void MemCopyTo(T[] destination, int offset, int length)
//        {
//            // Use the fast way if we have access to the base array
//            Array.Copy(_array, _offset, destination, offset, length);
//        }
//#endif

//        /// <summary>
//        /// Loads N values from a source array into this pointer's space
//        /// </summary>
//        /// <param name="length"></param>
//#if DEBUG
//        public void MemCopyFrom(T[] source, int sourceOffset, int length)
//        {
//            Assert(length >= 0, "Cannot memcopy() with a negative length!");
//            //PrintMemCopy(source, sourceOffset, length);
//            for (int c = 0; c < length; c++)
//            {
//                _array[c + _offset] = source[c + sourceOffset];
//                _initialized[c + _offset] = true;
//            }
//        }
//#else
//        public void MemCopyFrom(T[] source, int sourceOffset, int length)
//        {
//            Array.Copy(source, sourceOffset, _array, _offset, length);
//        }
//#endif

//        /// <summary>
//        /// Assigns a certain value to a range of spaces in this array
//        /// </summary>
//        /// <param name="value">The value to set</param>
//        /// <param name="length">The number of values to write</param>
//        public void MemSet(T value, int length)
//        {
//#if DEBUG
//            Assert(length >= 0, "Cannot memset() with a negative length!");
//#endif
//            MemSet(value, (uint)length);
//        }

//        /// <summary>
//        /// Assigns a certain value to a range of spaces in this array
//        /// </summary>
//        /// <param name="value">The value to set</param>
//        /// <param name="length">The number of values to write</param>
//        public void MemSet(T value, uint length)
//        {
//            for (int c = _offset; c < _offset + length; c++)
//            {
//                _array[c] = value;
//#if DEBUG
//                _initialized[c] = true;
//#endif
//            }
//        }

//        public void MemMoveTo(BasicDebugPointer<T> other, int length)
//        {
//            if (_array == other._array)
//            {
//                // Pointers refer to the same array, perform a move
//                //if (debug)
//                //    PrintMemCopy(_array, _offset, length);
//                MemMove(other.Offset - Offset, length);
//            }
//            else
//            {
//                // Pointers refer to different arrays (if you end up here you probably wanted to just to MemCopy())
//                // Debug.WriteLine("Unnecessary memmove detected");
//                MemCopyTo(other, length);
//            }
//        }

//        /// <summary>
//        /// Moves regions of memory within the bounds of this pointer's array.
//        /// Extra checks are done to ensure that the data is not corrupted if the copy
//        /// regions overlap
//        /// </summary>
//        /// <param name="move_dist">The offset to send this pointer's data to</param>
//        /// <param name="length">The number of values to copy</param>
//#if DEBUG
//        public void MemMove(int move_dist, int length)
//        {
//            Assert(length >= 0, "Cannot memmove() with a negative length!");
//            if (move_dist == 0 || length == 0)
//                return;

//            // Do regions overlap?
//            if ((move_dist > 0 && move_dist < length) || (move_dist < 0 && 0 - move_dist > length))
//            {
//                // Take extra precautions
//                if (move_dist < 0)
//                {
//                    // Copy forwards
//                    for (int c = 0; c < length; c++)
//                    {
//                        _array[c + _offset + move_dist] = _array[c + _offset];
//                        _initialized[c + _offset + move_dist] = true;
//                    }
//                }
//                else
//                {
//                    // Copy backwards
//                    for (int c = length - 1; c >= 0; c--)
//                    {
//                        _array[c + _offset + move_dist] = _array[c + _offset];
//                        _initialized[c + _offset + move_dist] = true;
//                    }
//                }
//            }
//            else
//            {
//                for (int c = 0; c < length; c++)
//                {
//                    _array[c + _offset + move_dist] = _array[c + _offset];
//                    _initialized[c + _offset + move_dist] = true;
//                }
//            }
//        }
//#else
//        public void MemMove(int move_dist, int length)
//        {
//            Arrays.MemMove(_array, _offset, _offset + move_dist, length);
//        }
//#endif

//        /// <summary>
//        /// Simulates pointer zooming: newPtr = &amp;ptr[offset].
//        /// Returns a pointer that is offset from this one within the same buffer.
//        /// </summary>
//        /// <param name="arg"></param>
//        /// <param name="offset"></param>
//        /// <returns></returns>
//        internal static Pointer<T> operator +(BasicDebugPointer<T> arg, int offset)
//        {
//            return new Pointer<T>(arg._array, arg._offset + offset);
//        }

//        internal static Pointer<T> operator +(BasicDebugPointer<T> arg, uint offset)
//        {
//            return new Pointer<T>(arg._array, (int)(arg._offset + offset));
//        }

//        /// <summary>
//        /// Simulates pointer zooming: newPtr = &amp;ptr[-offset].
//        /// Returns a pointer that is offset from this one within the same buffer.
//        /// </summary>
//        /// <param name="arg"></param>
//        /// <param name="offset"></param>
//        /// <returns></returns>
//        internal static Pointer<T> operator -(BasicDebugPointer<T> arg, int offset)
//        {
//            return new Pointer<T>(arg._array, arg._offset - offset);
//        }

//        /// <summary>
//        /// Indicates that this entire pointer's memory space has been freed, and this reference cannot be used again
//        /// </summary>
//        public void Free()
//        {
//#if DEBUG
//            for (int c = 0; c < _array.Length; c++)
//            {
//                _initialized[c] = false;
//            }
//#endif
//            _array = null;
//            _offset = -1;
//        }

//        public override bool Equals(object obj)
//        {
//            if (obj == null || GetType() != obj.GetType())
//            {
//                return false;
//            }

//            BasicDebugPointer<T> other = (BasicDebugPointer<T>)obj;
//            return other._offset == _offset &&
//                other._array == _array;
//        }

//        public override int GetHashCode()
//        {
//            return _array.GetHashCode() + _offset.GetHashCode();
//        }
//    }

//    #endregion
//}