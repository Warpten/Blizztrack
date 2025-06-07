using Blizztrack.Shared.Extensions;

using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Framework.TACT.Configuration
{
    using static System.MemoryExtensions;

    using Encoding = System.Text.Encoding;

    /// <summary>
    /// A strongly typed store of values deserialized from rows in a PSV file.
    /// </summary>
    /// <typeparam name="T">The type of the record.</typeparam>
    public sealed class PSV<T> : IEnumerable<T> where T : notnull
    {
        public readonly int SequenceNumber;
        private readonly T[] _values;

        public PSV(ReadOnlySpan<byte> rawData, PSV.Handler<T> recordHandler)
            => (SequenceNumber, _values) = PSV.Parse(rawData, recordHandler);

        public int Length => _values.Length;
        public T this[int index] => _values.UnsafeIndex(index);

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
    }

    /// <summary>
    /// Utility methods to parse pipe-separated values.
    /// </summary>
    public sealed class PSV
    {
        public delegate T Handler<T>(FieldInfo[] data, ReadOnlySpan<byte> fileData);
        public delegate T[] ArrayHandler<T>(FieldInfo[] data, ReadOnlySpan<byte> fileData);

        public delegate T EnumerableHandler<T>(FieldInfo[] data, ReadOnlySpan<byte> fileData);
        public delegate T[] ArrayEnumerableHandler<T>(FieldInfo[] data, ReadOnlySpan<byte> fileData);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIgnored(ReadOnlySpan<byte> line)
            => line.Length switch
            {
                0 => true,
                >= 2 => (line[0] == '#' && line[1] == '#') ? !line.Slice(3, 7).SequenceEqual("seqn = "u8) : false,
                _ => false
            };

        private static (int, List<(string Name, string Type)>) ReadHeader(ref ReadOnlySpan<byte> fileData, ref SpanSplitEnumerator<byte> lineEnumerator)
        {
            var sequenceNumber = 0;
            while (lineEnumerator.MoveNext())
            {
                var lineRange = lineEnumerator.Current;
                var line = fileData[lineRange];
                if (IsIgnored(line))
                    continue;

                if (line.StartsWith("## seqn = "u8))
                    sequenceNumber = int.Parse(line[10..]);
                else
                {
                    List<(string Name, string Type)> headerTokens = [];
                    foreach (var propRange in line.Split((byte)'|'))
                    {
                        var typeMarker = line[propRange].IndexOf((byte)'!');
                        Debug.Assert(typeMarker != -1);

                        var propertyName = Encoding.ASCII.GetString(line[propRange][..typeMarker]);
                        var propertyType = Encoding.ASCII.GetString(line[propRange][(typeMarker + 1)..]);

                        headerTokens.Add((propertyName, propertyType));
                    }

                    return (sequenceNumber, headerTokens);
                }
            }

            return (0, []);
        }

        private static async Task<(int, List<(string Name, string Type)>)> ReadHeader(IAsyncEnumerator<ArraySegment<byte>> lineEnumerator)
        {
            var sequenceNumber = 0;
            while (await lineEnumerator.MoveNextAsync())
            {
                var line = lineEnumerator.Current;
                if (IsIgnored(line))
                    continue;

                ReadOnlySpan<byte> lineSpan = line.AsSpan();

                if (lineSpan.StartsWith("## seqn = "u8))
                    sequenceNumber = int.Parse(line[10..]);
                else
                {
                    List<(string Name, string Type)> headerTokens = [];
                    foreach (var propRange in lineSpan.Split((byte)'|'))
                    {
                        var typeMarker = lineSpan[propRange].IndexOf((byte)'!');
                        Debug.Assert(typeMarker != -1);

                        var propertyName = Encoding.ASCII.GetString(line[propRange][..typeMarker]);
                        var propertyType = Encoding.ASCII.GetString(line[propRange][(typeMarker + 1)..]);

                        headerTokens.Add((propertyName, propertyType));
                    }

                    return (sequenceNumber, headerTokens);
                }
            }

            return (0, []);
        }

        private static (int SequenceNumber, Result[] Entries) ParseInternal<Result>(ReadOnlySpan<byte> fileData, Handler<Result> handler)
        {
            var lineEnumerator = fileData.Split((byte)'\n');
            var (sequenceNumber, headerTokens) = ReadHeader(ref fileData, ref lineEnumerator);

            if (headerTokens.Count == 0)
                return (sequenceNumber, []);

            var records = new List<Result>();
            while (lineEnumerator.MoveNext())
            {
                Debug.Assert(headerTokens.Count != 0);

                var lineRange = lineEnumerator.Current;
                var line = fileData[lineRange];
                if (IsIgnored(line))
                    continue;

                if (line.StartsWith("## seqn = "u8))
                    sequenceNumber = int.Parse(line[10..]);
                else
                {
                    // Preallocate ranges
                    var recordData = GC.AllocateUninitializedArray<FieldInfo>(headerTokens.Count);
                    // Traverse value ranges and store
                    var i = 0;
                    foreach (var valueRange in line.Split((byte)'|'))
                    {
                        var tokenInfo = headerTokens[i];

                        var valueStart = lineRange.Start.Value + valueRange.Start.Value;
                        var valueEnd = lineRange.Start.Value + valueRange.End.Value;

                        recordData[i] = new (tokenInfo.Name, tokenInfo.Type, i, new Range(valueStart, valueEnd));
                        ++i;
                    }

                    // Forward to the handler
                    var record = handler(recordData, fileData);
                    if (record != null)
                        records.Add(record);
                }
            }

            return (sequenceNumber, [.. records]);
        }

        private static (int SequenceNumber, Result[] Entries) ParseInternal<Result>(ReadOnlySpan<byte> fileData, ArrayHandler<Result> handler)
        {
            var lineEnumerator = fileData.Split((byte)'\n');
            var (sequenceNumber, headerTokens) = ReadHeader(ref fileData, ref lineEnumerator);

            if (headerTokens.Count == 0)
                return (sequenceNumber, []);

            var records = new List<Result>();
            while (lineEnumerator.MoveNext())
            {
                Debug.Assert(headerTokens.Count != 0);

                var lineRange = lineEnumerator.Current;
                var line = fileData[lineRange];
                if (IsIgnored(line))
                    continue;

                if (line.StartsWith("## seqn = "u8))
                    sequenceNumber = int.Parse(line[10..]);
                else
                {
                    // Preallocate ranges
                    var recordData = GC.AllocateUninitializedArray<FieldInfo>(headerTokens.Count);
                    // Traverse value ranges and store
                    var i = 0;
                    foreach (var valueRange in line.Split((byte)'|'))
                    {
                        var tokenInfo = headerTokens[i];

                        var valueStart = lineRange.Start.Value + valueRange.Start.Value;
                        var valueEnd = lineRange.Start.Value + valueRange.End.Value;

                        recordData[i] = new (tokenInfo.Name, tokenInfo.Type, i, new Range(valueStart, valueEnd));
                        ++i;
                    }

                    // Forward to the handler
                    var record = handler(recordData, fileData) ?? [];
                    records.AddRange(record);
                }
            }

            return (sequenceNumber, [.. records]);
        }

        public static (int SequenceNumber, T? Entry) ParseFirst<T>(ReadOnlySpan<byte> fileData, Handler<T?> handler)
        {
            var (sequenceNumber, entries) = ParseInternal(fileData, handler);
            return (sequenceNumber, entries.FirstOrDefault());
        }

        public static (int SequenceNumber, T[] Entries) Parse<T>(ReadOnlySpan<byte> fileData, Handler<T> handler)
            => ParseInternal(fileData, handler);
        public static (int SequenceNumber, T[] Entries) Parse<T>(ReadOnlySpan<byte> fileData, ArrayHandler<T> handler)
            => ParseInternal(fileData, handler);

        public static async Task<(int SequenceNumber, Result[] Entries)> ParseAsync<Result>(IAsyncEnumerable<ArraySegment<byte>> lines, Handler<Result> handler)
        {
            var lineEnumerator = lines.GetAsyncEnumerator();
            var (sequenceNumber, headerTokens) = await ReadHeader(lineEnumerator);

            if (headerTokens.Count == 0)
                return (sequenceNumber, []);

            var records = new List<Result>();
            while (await lineEnumerator.MoveNextAsync())
            {
                Debug.Assert(headerTokens.Count != 0);

                var line = lineEnumerator.Current;
                if (IsIgnored(line))
                    continue;

                ReadOnlySpan<byte> lineSpan = line.AsSpan();

                if (lineSpan.StartsWith("## seqn = "u8))
                    sequenceNumber = int.Parse(line[10..]);
                else
                {
                    // Preallocate ranges
                    var recordData = GC.AllocateUninitializedArray<FieldInfo>(headerTokens.Count);
                    // Traverse value ranges and store
                    var i = 0;
                    foreach (var valueRange in lineSpan.Split((byte)'|'))
                    {
                        var tokenInfo = headerTokens[i];
                        recordData[i] = new (tokenInfo.Name, tokenInfo.Type, i, valueRange);
                        ++i;
                    }

                    // Forward to the handler
                    var record = handler(recordData, line);
                    if (record != null)
                        records.Add(record);
                }
            }

            return (sequenceNumber, [.. records]);
        }

        public static async Task<(int SequenceNumber, Result[] Entries)> ParseAsync<Result>(IAsyncEnumerable<ArraySegment<byte>> lines, ArrayHandler<Result> handler)
        {
            var lineEnumerator = lines.GetAsyncEnumerator();
            var (sequenceNumber, headerTokens) = await ReadHeader(lineEnumerator);

            if (headerTokens.Count == 0)
                return (sequenceNumber, []);

            var records = new List<Result>();
            while (await lineEnumerator.MoveNextAsync())
            {
                Debug.Assert(headerTokens.Count != 0);

                var line = lineEnumerator.Current;
                if (IsIgnored(line))
                    continue;

                ReadOnlySpan<byte> lineSpan = line.AsSpan();

                if (lineSpan.StartsWith("## seqn = "u8))
                    sequenceNumber = int.Parse(line[10..]);
                else
                {
                    // Preallocate ranges
                    var recordData = GC.AllocateUninitializedArray<FieldInfo>(headerTokens.Count);
                    // Traverse value ranges and store
                    var i = 0;
                    foreach (var valueRange in lineSpan.Split((byte)'|'))
                    {
                        var tokenInfo = headerTokens[i];
                        recordData[i] = new (tokenInfo.Name, tokenInfo.Type, i, valueRange);
                        ++i;
                    }

                    // Forward to the handler
                    var record = handler(recordData, lineSpan) ?? [];
                    records.AddRange(record);
                }
            }

            return (sequenceNumber, [.. records]);
        }
    
        public readonly record struct FieldInfo(string Name, string Type, int Index, Range range);
    }
}
