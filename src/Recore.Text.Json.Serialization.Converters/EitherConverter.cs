﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recore.Text.Json.Serialization.Converters
{
    /// <summary>
    /// JSON converter factory for the open generic type <seealso cref="Either{TLeft, TRight}"/>.
    /// </summary>
    internal sealed class EitherConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(Either<,>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var leftType = typeToConvert.GetGenericArguments()[0];
            var rightType = typeToConvert.GetGenericArguments()[1];

            var eitherConverter = Activator.CreateInstance(
                type: typeof(EitherConverter<,>).MakeGenericType(new[] { leftType, rightType }));

            if (eitherConverter is null)
            {
                // According to the docs, `CreateInstance` should return `null`
                // only if the type is some `Nullable<T>`
                throw new InvalidOperationException();
            }

            return (JsonConverter)eitherConverter;
        }
    }

    /// <summary>
    /// JSON converter for the generic type <seealso cref="Either{TLeft, TRight}"/>
    /// for a given <typeparamref name="TLeft"/> and <typeparamref name="TRight"/>.
    /// </summary>
    internal sealed class EitherConverter<TLeft, TRight> : JsonConverter<Either<TLeft, TRight>>
    {
        public override Either<TLeft, TRight> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Read the whole string in as JSON to avoid the case where the first converter partially succeeds
            // and then the reader is stuck in the middle of the JSON.
            var jsonDocument = JsonDocument.ParseValue(ref reader);
            var json = jsonDocument.RootElement.ToString()!;

            // `ToString()` for a string type will return the string *without* quotes,
            // which won't deserialize correctly.
            if (jsonDocument.RootElement.ValueKind == JsonValueKind.String)
            {
                json = $"\"{json}\"";
            }

            // Using try-catch for control flow is an antipattern,
            // but it seems to be the only way in this case.
            try
            {
                return JsonSerializer.Deserialize<TLeft>(json, options)!;
            }
            catch (JsonException)
            {
                return JsonSerializer.Deserialize<TRight>(json, options)!;
            }
        }

        public override void Write(Utf8JsonWriter writer, Either<TLeft, TRight> value, JsonSerializerOptions options)
            => value.Switch(
                l => JsonSerializer.Serialize(writer, l, options),
                r => JsonSerializer.Serialize(writer, r, options));
    }

    /// <summary>
    /// Converts <seealso cref="Either{TLeft, TRight}"/> to and from JSON.
    /// Register this converter to override the default deserialization behavior.
    /// </summary>
    /// <remarks>
    /// This converter is made to be used with a closed type and registered through <seealso cref="JsonSerializerOptions.Converters"/>.
    /// It is not returned by <seealso cref="EitherConverter.CreateConverter(Type, JsonSerializerOptions)"/>.
    /// </remarks>
    public sealed class OverrideEitherConverter<TLeft, TRight> : JsonConverter<Either<TLeft, TRight>>
    {
        private readonly Func<JsonElement, bool> deserializeAsLeft;

        /// <summary>
        /// Initializes an instance of <see cref="OverrideEitherConverter{TLeft, TRight}"/>.
        /// </summary>
        /// <param name="deserializeAsLeft">
        /// A delegate that takes a <seealso cref="JsonElement"/> representing some JSON and returns
        /// whether it should be deserialized as <typeparamref name="TLeft"/> or <typeparamref name="TRight"/>.
        /// It should return <c>true</c> for <typeparamref name="TLeft"/> and <c>false</c> for <typeparamref name="TRight"/>.
        /// </param>
        public OverrideEitherConverter(
            Func<JsonElement, bool> deserializeAsLeft)
        {
            this.deserializeAsLeft = deserializeAsLeft;
        }

        /// <summary>
        /// Deserializes JSON into <seealso cref="Either{TLeft, TRight}"/>.
        /// This method will call the delegate passed to the constructor to determine how to deserialize the JSON.
        /// </summary>
        public override Either<TLeft, TRight> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jsonDocument = JsonDocument.ParseValue(ref reader);
            var json = jsonDocument.RootElement.ToString()!;

            // `ToString()` for a string type will return the string *without* quotes,
            // which won't deserialize correctly.
            if (jsonDocument.RootElement.ValueKind == JsonValueKind.String)
            {
                json = $"\"{json}\"";
            }

            if (deserializeAsLeft(jsonDocument.RootElement))
            {
                return JsonSerializer.Deserialize<TLeft>(json, options)!;
            }
            else
            {
                return JsonSerializer.Deserialize<TRight>(json, options)!;
            }
        }

        /// <summary>
        /// Serializes <seealso cref="Either{TLeft, TRight}"/> as JSON.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, Either<TLeft, TRight> value, JsonSerializerOptions options)
        {
            var eitherConverter = new EitherConverter<TLeft, TRight>();
            eitherConverter.Write(writer, value, options);
        }
    }
}
