using Microsoft.Extensions.Caching.Memory;

using System.Runtime.CompilerServices;

namespace Blizztrack.Services.Caching
{
    public class MemoryCache<T>
        where T : struct
    {
        public readonly ref struct LeasedEntry
        {
            private readonly ICacheEntry _cacheEntry;

            public ref T Entry => ref Unsafe.Unbox<T>(_cacheEntry.Value!);
        }
    }
}
