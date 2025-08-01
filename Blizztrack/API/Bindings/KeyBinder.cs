using Blizztrack.Framework.TACT;
using Blizztrack.Framework.TACT.Configuration;
using Blizztrack.Framework.TACT.Implementation;
using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Persistence;
using Blizztrack.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;

using System.Reflection;

namespace Blizztrack.API.Bindings
{
    public class KeyBinder<T> : IModelBinder where T : IOwnedKey<T>
    {
        public class Mapper : ITypeMapper
        {
            public Type MappedType { get; } = typeof(T);

            public bool UseReference { get; } = false;

            public void GenerateSchema(JsonSchema schema, TypeMapperContext context)
                => schema.Type = JsonObjectType.String;
        }

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);
            var modelName = bindingContext.ModelName;
            var keyValue = bindingContext.ValueProvider.GetValue(modelName);

            if (keyValue == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(modelName, keyValue);

            if (string.IsNullOrEmpty(keyValue.FirstValue))
            {
                bindingContext.ModelState.TryAddModelError(modelName, "Key must be an hex string.");
                return Task.CompletedTask;
            }

            var value = keyValue.FirstValue.AsKey<T>();
            if (value == T.Zero)
            {
                bindingContext.ModelState.TryAddModelError(modelName, "Key must be an hex string.");
                return Task.CompletedTask;
            }

            bindingContext.Result = ModelBindingResult.Success(new Bound<T>(value));
            return Task.CompletedTask;
        }
    }

    public class Bound<T>(T storage)
    {
        private readonly T _storage = storage;

        public static implicit operator T(Bound<T> self) => self._storage;
    }

    [ModelBinder<KeyBinder<EncodingKey>>]
    public sealed class BoundEncodingKey(EncodingKey key) : Bound<EncodingKey>(key);

    [ModelBinder<KeyBinder<ContentKey>>]
    public sealed class BoundContentKey(ContentKey key) : Bound<ContentKey>(key);

}
