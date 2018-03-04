using System.Collections.Generic;

namespace LightGbmDotNet
{
    public static class EnumerableExtensions
    {
        public static ReadAheadEnumerable<T> StartReadingAhead<T>(this IEnumerable<T> enumerable, int maxAhead = 10000)
        {
            var rae = new ReadAheadEnumerable<T>(enumerable, maxAhead: maxAhead);
            return rae;
        }
    }
}