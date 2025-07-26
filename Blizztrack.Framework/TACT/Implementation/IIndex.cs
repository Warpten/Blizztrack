namespace Blizztrack.Framework.TACT.Implementation
{
    public interface IIndex
    {
        /// <summary>
        /// A record within an archive index.
        /// </summary>
        public readonly ref struct Entry
        {
            internal Entry(EncodingKeyRef key, int offset, long length, EncodingKey archive)
            {
                EncodingKey = key;
                Offset = offset;
                Length = length;
                Archive = archive;
            }

            /// <summary>
            /// The content key for this record.
            /// </summary>
            public readonly EncodingKeyRef EncodingKey;

            /// <summary>
            /// The offset, in bytes, of the resource within its archive.
            /// </summary>
            public readonly int Offset;

            /// <summary>
            /// The length, in bytes, of the resource within its archive.
            /// </summary>
            public readonly long Length;

            /// <summary>
            /// The encoding key that describes the archive holding the resource described by this object.
            /// </summary>
            public readonly EncodingKey Archive;

            public static implicit operator bool(Entry entry) => entry.EncodingKey != default!;
        }

        /// <summary>
        /// Resolves an encoding key within this index.
        /// </summary>
        /// <typeparam name="T">A type that implements <see cref="IEncodingKey{T}"/>.</typeparam>
        /// <param name="encodingKey">The encoding key to search for.</param>
        /// <returns></returns>
        public Entry FindEncodingKey<T>(in T encodingKey)
            where T : notnull, IEncodingKey<T>, allows ref struct;

        public Entry this[int index] { get; }
        public Entry this[System.Index index] { get; }

        /// <summary>
        /// The amount of entries within this index.
        /// </summary>
        public int Count { get; }
    }

    // Decorator interface to constraint types. FOrces proper duck-typing for enumerator types.
    public interface IIndexEnumerator<T> where T : allows ref struct
    {
        public T GetEnumerator();

        public bool MoveNext();

        public IIndex.Entry Current { get; }
    }

    public interface IIndex<E> : IIndex where E : notnull, IIndexEnumerator<E>, allows ref struct
    {
        public E Entries { get; }
    }
}
