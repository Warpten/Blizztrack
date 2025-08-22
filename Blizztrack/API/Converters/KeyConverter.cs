using Blizztrack.Framework.TACT;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blizztrack.API.Converters
{
    public class KeyConverter<T> : JsonConverter<T> where T : struct, IKey<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.AsHexString());
        }
    }
}
