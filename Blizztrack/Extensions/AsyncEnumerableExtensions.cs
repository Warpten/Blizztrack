using System.Runtime.CompilerServices;

namespace Blizztrack.Extensions
{
    public static class AsyncEnumerableExtensions
    {
        /// <summary>
        /// Merges this set of enumerables, sequencing values as they come in.
        /// <para>
        /// If multiple enumerators produce a value at the same time, the ordering
        /// of the values out of the enumerable returned by this method is unspecified.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The type of value produced by the enumerables.</typeparam>
        /// <typeparam name="U">A collection type, usually <code><typeparamref name="T"/>[]</code>.</typeparam>
        /// <param name="enumerables">An array of</param>
        /// <param name="stoppingToken"></param>
        /// <returns>An enumerable that eagerly returns elements from the individual source enumerables as they come in.</returns>
        /// <exception cref="OperationCanceledException">IIf a cancellation has been requesting with the token.</exception>
        public static async IAsyncEnumerable<T> Merge<U, T>(this U enumerables, [EnumeratorCancellation] CancellationToken stoppingToken = default)
            where U : IReadOnlyList<IAsyncEnumerable<T>>
        {
            var count = enumerables.Count;

            IAsyncEnumerator<T>?[] enumerators = [.. enumerables.Select(s => s.GetAsyncEnumerator(stoppingToken))];
            var activeTasks = enumerators.Select((e, i) => advanceEnumerator(e!, i)).ToArray();

            var activeCount = activeTasks.Length;
            while (activeCount > 0)
            {
                // Any less ugly way to do this? System.Interactive.Async/Linq uses a custom implementation of WhenAny tailored to ValueTasks...
                // Ideally we use WhenEach here but it needs to not make defensive copies, and that behavior is unspecified.
                var (completedTask, taskIndex) = await (await Task.WhenAny(activeTasks).ConfigureAwait(false)).ConfigureAwait(false);
                var enumerator = enumerators[taskIndex]!;

                if (completedTask)
                {
                    yield return enumerator.Current;
                    activeTasks[taskIndex] = advanceEnumerator(enumerator, taskIndex);
                }
                else
                {
                    --activeCount;

                    // Replace the task with one that will basically never complete.
                    activeTasks[taskIndex] = new ValueTask<(bool, int)>().AsTask();

                    await enumerator!.DisposeAsync().ConfigureAwait(false);
                    enumerators[taskIndex] = default;
                }
            }

            async Task<(bool, int)> advanceEnumerator(IAsyncEnumerator<T> enumerator, int index)
            {
                var successfullyAdvanced = await enumerator.MoveNextAsync(stoppingToken).ConfigureAwait(false);
                return (successfullyAdvanced, index);
            }
        }
    }
}
