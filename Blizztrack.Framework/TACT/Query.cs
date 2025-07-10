using Blizztrack.Framework.TACT.Implementation;

using static Blizztrack.Framework.TACT.Implementation.Encoding;

namespace Blizztrack.Framework.TACT
{
    public interface IQuery<T, out R> where R : notnull, allows ref struct
    {
        public R? SingleOrDefault(T subject);
    }

    public unsafe readonly ref struct TransformQuery<U, T, R, S>(U nestedQuery, delegate*<R?, S> transform) : IQuery<T, S>
        where U : notnull, IQuery<T, R>
        where R : notnull, allows ref struct
        where S : notnull, allows ref struct
    {
        private readonly U Query = nestedQuery;
        private readonly delegate*<R?, S> Transform = transform;

        public readonly S SingleOrDefault(T subject) => Transform(Query.SingleOrDefault(subject));
    }

    public unsafe readonly ref struct FilteredQuery<U, T, R, K>(U nestedQuery, delegate*<R, K, bool> filter, K parameter) : IQuery<T, R>
        where U : IQuery<T, R>
        where K : notnull, allows ref struct
        where R : notnull, allows ref struct
    {
        public readonly U Query = nestedQuery;
        private readonly delegate*<R, K, bool> Filter = filter;
        private readonly K Parameter = parameter;

        public readonly R? SingleOrDefault(T subject)
        {
            var result = Query.SingleOrDefault(subject);
            if (result is null || !Filter(result, Parameter))
                return default;

            return result;
        }
    }

    public unsafe readonly ref struct AdaptedQuery<U, T, R, I>(U nestedQuery, delegate*<T, I> adapter) : IQuery<T, R>
        where U : IQuery<I, R>
        where R : notnull, allows ref struct
    {
        private readonly U Query = nestedQuery;
        private readonly delegate*<T, I> Adapter = adapter;

        public readonly R? SingleOrDefault(T subject) => Query.SingleOrDefault(Adapter(subject));
    }

    public interface IEncodingQuery : IQuery<Encoding, Entry>;

    public unsafe static class EncodingQueryExtensions
    {
        public static FilteredQuery<T, Encoding, Entry, K> WithEncodingKey<T, K>(this T query, K encodingKey)
            where T : IEncodingQuery
            where K : IEncodingKey<K>, allows ref struct
            => new(query, &HasEncodingKey, encodingKey);

        private static bool HasEncodingKey<K>(Entry r, K key)
            where K : IEncodingKey<K>, allows ref struct
        {
            for (var i = 0; i < r.Count; ++i)
                if (r[i].SequenceEqual(key))
                    return true;

            return false;
        }

        public static FilteredQuery<T, Encoding, Entry, ulong> WithDecompressedSize<T>(this T query, ulong decompressedSize)
            where T : IEncodingQuery
            => new(query, &HasCompressedSize, decompressedSize);

        private static bool HasCompressedSize(Entry r, ulong sz) => r.FileSize == sz;
    }

    public interface IRootQuery : IQuery<Root, RootRecord>;

    public static class RootQueryExtensions {
        public unsafe static FilteredQuery<T, Root, RootRecord, ulong> WithNameHash<T>(this T query, ulong nameHash)
            where T : IRootQuery
            => new(query, &HasNameHash, nameHash);

        private static bool HasNameHash(RootRecord r, ulong hash) => r.NameHash == hash;

        public unsafe static FilteredQuery<T, Root, RootRecord, K> WithContentKey<T, K>(this T query, K contentKey)
            where T : IRootQuery
            where K : IContentKey<K>, IKey, allows ref struct
            => new(query, &HasContentKey, contentKey);

        private static bool HasContentKey<K>(RootRecord record, K contentKey)
            where K : IContentKey<K>, IKey, allows ref struct
            => record.ContentKey.AsSpan().SequenceEqual(contentKey.AsSpan());
    }
}
