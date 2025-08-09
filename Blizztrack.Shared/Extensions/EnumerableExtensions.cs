using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Blizztrack.Shared.Extensions
{
    public static class EnumerableExtensions
    {
        public static void Deconstruct<K, V>(this IGrouping<K, V> grouping, out K key, out IEnumerable<V> group)
        {
            key = grouping.Key;
            group = grouping;
        }
    }
}
