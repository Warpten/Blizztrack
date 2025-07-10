using Blizztrack.Shared.Extensions;

using static Blizztrack.Framework.TACT.Implementation.IIndex;

namespace Blizztrack.Framework.TACT.Implementation
{
    public sealed class CompoundingIndex(Index[] indices) : IIndex
    {
        private readonly Index[] _indices = indices;

        private readonly EncodingKey[] _keys = [.. indices.Select(i => i.Key)];

        public EncodingKey Key { get; } = default;
        public int Count => _indices.Sum(i => i.Count);

        // Actually not trivial to implement; we should have some sort of switching sub-implementation
        // - but the design of value-typed enumerators forbids interfaces, and typing IIndex on its enumerator is....
        // icky.
        public Enumerator Entries => throw new NotImplementedException();

        public Entry FindEncodingKey<T>(T encodingKey) where T : notnull, IEncodingKey<T>, allows ref struct
        {
            var archiveIndex = encodingKey[0];
            for (var i = 1; i < encodingKey.Length / 2 + 1; ++i)
                archiveIndex ^= encodingKey[i];

            archiveIndex = (byte)((archiveIndex & 0xF) | (archiveIndex >> 4));
            if (archiveIndex > _indices.Length)
                return default;

            var rawEntry = _indices.UnsafeIndex(archiveIndex).FindEncodingKey(encodingKey);
            return new(rawEntry.EncodingKey, rawEntry.Offset, rawEntry.Length, _indices[archiveIndex].Key);
        }
    }
}
