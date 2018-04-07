using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backupr
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T[]> GetPaged<T>(this IList<T> source, int pageSize)
        {
            int index = 0;
            while (index < source.Count)
            {
                yield return source.Skip(index).Take(pageSize).ToArray();
                index += pageSize;
            }
        }
    }
}
