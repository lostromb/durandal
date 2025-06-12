﻿using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus
{
    internal class MemoryBlock<T>
    {
        public T[] Data;

        public MemoryBlock(int size)
        {
            Data = new T[size];
        }

        public MemoryBlock(T[] data)
        {
            Data = data;
        }

        public void Free()
        {
            Data = null;
        }
        
        public void Realloc(int newSize)
        {
            T[] newData = new T[newSize];
            for (int c = 0; c < FastMath.Min(Data.Length, newSize); c++)
            {
                newData[c] = Data[c];
            }

            Data = newData;
        }
    }
}
