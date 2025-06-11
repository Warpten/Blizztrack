using Blizztrack.Framework.Extensions;

using System.Runtime.CompilerServices;

namespace Blizztrack.Framework.Ribbit
{
    public readonly struct MultipartCommandExecutor(ReadOnlySpan<byte> contentDisposition) : ICommandExecutor
    {
        private readonly byte[] _contentDisposition = [..contentDisposition];

        private static readonly byte[] ContentType = [.."Content-Type: multipart/alternative; boundary="u8];
        private static readonly byte[] ContentDisposition = [.."Content-Disposition: "u8];

        public async IAsyncEnumerable<ArraySegment<byte>> Apply(IAsyncEnumerable<ArraySegment<byte>> source,
            [EnumeratorCancellation] CancellationToken stoppingToken)
        {
            var enumerator = source.SplitAny("\n"u8.ToArray(), "\r\n"u8.ToArray()).GetAsyncEnumerator(stoppingToken);
            if (!await enumerator.MoveNextAsync(stoppingToken))
                yield break;

            while (true)
            {
                if (enumerator.Current.AsSpan().StartsWith(ContentType))
                    break;

                if (!await enumerator.MoveNextAsync(stoppingToken))
                    yield break;
            }

            // Determine what the boundary marker looks like, ad duplicate it
            // Duplication is needed because the segment returned by the source is getting overwritten on each iteration.
            var boundaryMarker = enumerator.Current.AsSpan()
                .Slice(ContentType.Length + 1, enumerator.Current.Count - ContentType.Length - 2)
                .ToArray();

            // Skip the initial block
            if (!await toNextBoundaryMarkerAsync(enumerator))
                yield break;

            // From here, we need to look for the Content-Disposition header as well as the end of headers delimiter.
            while (true)
            {
                // Parse headers
                if (!await MatchesContentDisposition(enumerator, stoppingToken))
                {
                    // Headers don't match, move to the next block.
                    // If we failed, either there was a network error, or we got to the end of the stream.
                    if (!await toNextBoundaryMarkerAsync(enumerator))
                        yield break;
                }

                // Now all we have to do is keep sending values back until we hit a boundary marker, or the end of the file.
                while (true)
                {
                    if (!await enumerator.MoveNextAsync(stoppingToken))
                        yield break;

                    var currentValue = enumerator.Current;
                    if (currentValue[0] == '-' && currentValue[1] == '-' && currentValue[2..].SequenceEqual(boundaryMarker))
                        yield break;

                    yield return currentValue;
                }
            }

            async ValueTask<bool> toNextBoundaryMarkerAsync(IAsyncEnumerator<ArraySegment<byte>> enumerator)
            {
                Span<byte> currentValue;
                do
                {
                    if (!await enumerator.MoveNextAsync(stoppingToken))
                        return false;

                    currentValue = enumerator.Current.AsSpan();
                } while (currentValue[0] != '-' || currentValue[1] != '-' || !currentValue[2..].SequenceEqual(boundaryMarker));

                return true;
            }
        }

        private async ValueTask<bool> MatchesContentDisposition(IAsyncEnumerator<ArraySegment<byte>> enumerator, CancellationToken stoppingToken)
        {
            Span<byte> currentValue;
            while (true)
            {
                if (!await enumerator.MoveNextAsync(stoppingToken))
                    return false;

                currentValue = enumerator.Current.AsSpan();
                if (currentValue.IsEmpty)
                    break;

                // If a Content-Disposition header is available and it does not match our expectations, this block
                // is not valid, therefore skip to the next one.
                if (currentValue.StartsWith(ContentDisposition)
                    && !currentValue[ContentDisposition.Length..].SequenceEqual(_contentDisposition))
                    return false;
            }

            return true;
        }
    }

    public readonly struct SimpleCommandExecutor : ICommandExecutor
    {
        public IAsyncEnumerable<ArraySegment<byte>> Apply(IAsyncEnumerable<ArraySegment<byte>> source, CancellationToken stoppingToken)
            => source.SplitAny("\n"u8.ToArray(), "\r\n"u8.ToArray());
    }

    file static class AsyncEnumerableExtensions
    {
        public static IAsyncEnumerable<ArraySegment<byte>> SplitAny(this IAsyncEnumerable<ArraySegment<byte>> sequence, params ReadOnlySpan<byte[]> delimiters)
            => SplitCore(sequence, static (e, d) => (e.IndexOf(d), d.Length), [.. delimiters]);

        public static IAsyncEnumerable<ArraySegment<byte>> SplitAny(this IAsyncEnumerable<ArraySegment<byte>> sequence, byte[] delimiters)
            => SplitCore(sequence, static (e, d) => (e.IndexOf(d), 1), delimiters);

        public static IAsyncEnumerable<ArraySegment<byte>> Split(this IAsyncEnumerable<ArraySegment<byte>> sequence, ArraySegment<byte> delimiter)
            => SplitCore(sequence, static (e, d) => (e.IndexOf(d), d.Count), [delimiter]);

        public static IAsyncEnumerable<ArraySegment<byte>> Split(this IAsyncEnumerable<ArraySegment<byte>> sequence, byte delimiter)
            => SplitCore(sequence, static (e, d) => (e.IndexOf(d), 1), [delimiter]);

        private delegate (int, int) SplitLocator<T, U>(ReadOnlySpan<T> element, U delimiter);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async IAsyncEnumerable<ArraySegment<byte>> SplitCore<U>(this IAsyncEnumerable<ArraySegment<byte>> sequence,
            SplitLocator<byte, U> selector, IReadOnlyList<U> selectorArg)
        {
            var dataBuffer = new List<byte>();

            await foreach (var element in sequence)
            {
                var searchOffset = 0;
            NextDelimiter:
                var (markerIndex, markerSize) = (-1, 0);
                for (var i = 0; i < selectorArg.Count && markerIndex == -1; ++i)
                    (markerIndex, markerSize) = selector(element[searchOffset..], selectorArg[i]);

                if (markerIndex == -1)
                    dataBuffer.AddRange(element[searchOffset..]);
                else
                {
                    dataBuffer.AddRange(element.AsSpan(searchOffset, markerIndex));

                    yield return dataBuffer.AsBackingArray()[..dataBuffer.Count];

                    dataBuffer.Clear();
                    searchOffset += markerIndex + markerSize;
                    goto NextDelimiter;
                }
            }

            yield return dataBuffer.AsBackingArray()[..dataBuffer.Count];
        }
    }
}
