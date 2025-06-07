using Blizztrack.Shared.Extensions;

using static System.MemoryExtensions;

namespace Blizztrack.Framework.TACT.Configuration
{
    public class ConfigurationFile
    {
        public delegate T Handler<T>(Range[] names, Range[] values, ReadOnlySpan<byte> fileData) where T : notnull;

        public static T Parse<T>(ReadOnlySpan<byte> fileData, Handler<T> handler) where T : notnull
        {
            var properties = new List<Range>();
            var values = new List<Range>();

            var lineEnumerator = fileData.Split((byte) '\n');
            while (lineEnumerator.MoveNext())
            {
                var line = fileData[lineEnumerator.Current];
                if (line.IsEmpty || line[0] == '#')
                    continue;

                var lineTokens = line.Split((byte) '=');
                if (!lineTokens.MoveNext()) // If no token, go to next line
                    continue;

                var propertyRange = lineTokens.Current;
                if (!lineTokens.MoveNext()) // If no other token, go to next line
                    continue;

                var valueRange = lineTokens.Current;
                if (lineTokens.MoveNext()) // If another token exists, go to next line
                    continue;

                // Trim the ranges and rebase them to the file.

                properties.Add(propertyRange.Trim(line, x => x == ' ').Rebase(lineEnumerator.Current));
                values.Add(valueRange.Trim(line, x => x == ' ').Rebase(lineEnumerator.Current));
            }

            return handler([.. properties], [.. values], fileData);
        }
    }
}
