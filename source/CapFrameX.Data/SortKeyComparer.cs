using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Data
{
    public class SortKeyComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xParts = x.Split('_').Select(int.Parse).ToArray();
            var yParts = y.Split('_').Select(int.Parse).ToArray();

            int length = Math.Max(xParts.Length, yParts.Length);

            for (int i = 0; i < length; i++)
            {
                int xVal = i < xParts.Length ? xParts[i] : 0;
                int yVal = i < yParts.Length ? yParts[i] : 0;

                int result = xVal.CompareTo(yVal);
                if (result != 0)
                    return result;
            }

            return 0;
        }
    }
}
