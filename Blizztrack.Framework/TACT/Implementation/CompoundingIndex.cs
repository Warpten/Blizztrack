using Blizztrack.Shared.Extensions;

using System.Runtime.CompilerServices;

using static Blizztrack.Framework.TACT.Implementation.IIndex;

namespace Blizztrack.Framework.TACT.Implementation
{
    /// <summary>
    /// An index that aggregates multiple indices. This is <b>not</b> an implementation of
    /// <c>group-index</c>.
    /// </summary>
    /// <param name="indices"></param>
    public sealed class CompoundingIndex(IIndex[] indices) : IIndex
    {
        private readonly IIndex[] _indices = indices;

        public Entry this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var indiceIndex = 0;
                while (indiceIndex < _indices.Length)
                {
                    ref var currentIndex = ref _indices.UnsafeIndex(indiceIndex);
                    var indexSize = currentIndex.Count;
                    if (index < indexSize)
                        return currentIndex[index];

                    index -= indexSize;
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public Entry this[System.Index index] => this[index.GetOffset(Count)];

        public int Count => _indices.Sum(i => i.Count);

        public Entry FindEncodingKey<T>(in T encodingKey) where T : notnull, IEncodingKey<T>, allows ref struct
        {
            // Not valid for TACT ... Or is it?
            // var archiveIndex = encodingKey[0];
            // for (var i = 1; i < encodingKey.Length / 2 + 1; ++i)
            //     archiveIndex ^= encodingKey[i];
            // 
            // archiveIndex = (byte)((archiveIndex & 0xF) ^ (archiveIndex >> 4));
            // if (archiveIndex > _indices.Length)
            //     return default;
            // 
            // ref var containingIndex = ref _indices.UnsafeIndex(archiveIndex);
            // return containingIndex.FindEncodingKey(encodingKey);

            for (var i = 0; i < _indices.Length; ++i)
            {
                ref var containingIndex = ref _indices.UnsafeIndex(i);

                var entry = containingIndex.FindEncodingKey(encodingKey);
                if (entry != default)
                    return entry;
            }

            return default;
        }
    }
}
