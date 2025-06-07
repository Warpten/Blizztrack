namespace Blizztrack.Linq
{
    public static class AsyncEnumerable
    {
        /// <summary>
        /// Creates an <see cref="IAsyncEnumerable{T}"/> which yields no results, similar to <see cref="Enumerable.Empty{TResult}"/>.
        /// </summary>
        public static IAsyncEnumerable<T?> Empty<T>() where T : notnull => EmptyAsyncEnumerator<T>.Instance;

        private class EmptyAsyncEnumerator<T> : IAsyncEnumerator<T?>, IAsyncEnumerable<T?> where T : notnull
        {
            public static readonly EmptyAsyncEnumerator<T> Instance = new();

            public T? Current => default;

            public ValueTask DisposeAsync() => default;

            public IAsyncEnumerator<T?> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return this;
            }

            public ValueTask<bool> MoveNextAsync() => new(false);
        }
    }
}
