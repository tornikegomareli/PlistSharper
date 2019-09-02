using System;
using System.Collections.Generic;

namespace PlistSharper.src.Models
{
    public static class SharedDataSet
    {
        public static List<int> OffsetTable = new List<int>();
        public static List<byte> ObjectTable = new List<byte>();
        public static int ReferenceCount;
        public static int ObjectReferenceSize;
        public static int OffsetByteSize;
        public static long OffSetTableSize;
       
    }
}
