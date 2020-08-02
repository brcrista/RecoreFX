﻿using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Recore.Text.Json.Serialization.Converters.Tests
{
    public class EitherConverterTests
    {
        class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        class Address
        {
            public string Street { get; set; }
            public string Zip { get; set; }
        }

        class PersonAddress
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public string Street { get; set; }
            public string Zip { get; set; }

            public Either<Person, Address> ToEither()
            {
                if (Name != null)
                {
                    return new Person
                    {
                        Name = Name,
                        Age = Age
                    };
                }
                else
                {
                    return new Address
                    {
                        Street = Street,
                        Zip = Zip
                    };
                }
            }
        }

        [JsonConverter(typeof(TypeWithConverterConverter))]
        class TypeWithConverter
        {
            public int Age { get; set; }

            public string Name { get; set; }

            public string GetFullName() => $"{Name} {Name}son";
        }

        // Serialize as { "fullName": GetFullName(), "age": Age }
        class TypeWithConverterConverter : JsonConverter<TypeWithConverter>
        {
            public override TypeWithConverter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                reader.Read();
                var propertyName = reader.GetString();
                if (propertyName != "fullName")
                {
                    throw new JsonException($"Unexpected JSON token: {propertyName})");
                }

                reader.Read();
                var fullName = reader.GetString();

                reader.Read();
                propertyName = reader.GetString();
                if (propertyName != "age")
                {
                    throw new JsonException($"Unexpected JSON token: {propertyName})");
                }

                reader.Read();
                var age = reader.GetInt32();

                // Invert the type's methods to get the original values
                var name = fullName.Split()[0];

                // Make sure you read to the end, or `JsonSerializer` will throw an exception
                reader.Read();
                return new TypeWithConverter
                {
                    Name = name,
                    Age = age
                };
            }

            public override void Write(Utf8JsonWriter writer, TypeWithConverter value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteString("fullName", value.GetFullName());
                writer.WriteNumber("age", value.Age);
                writer.WriteEndObject();
            }
        }

        [Fact]
        public void ToJson()
        {
            {
                Either<int, string> either;

                either = 12;
                Assert.Equal(
                    expected: "12",
                    actual: JsonSerializer.Serialize(either));

                either = "hello";
                Assert.Equal(
                    expected: "\"hello\"",
                    actual: JsonSerializer.Serialize(either));
            }
            {
                Either<string, int> either;

                either = 12;
                Assert.Equal(
                    expected: "12",
                    actual: JsonSerializer.Serialize(either));

                either = "hello";
                Assert.Equal(
                    expected: "\"hello\"",
                    actual: JsonSerializer.Serialize(either));
            }
            {
                Either<string, Person> either;

                either = new Person { Name = "Mario", Age = 42 };
                Assert.Equal(
                    expected: "{\"Name\":\"Mario\",\"Age\":42}",
                    actual: JsonSerializer.Serialize(either));
            }
            {
                Either<Person, string> either;

                either = new Person { Name = "Mario", Age = 42 };
                Assert.Equal(
                    expected: "{\"Name\":\"Mario\",\"Age\":42}",
                    actual: JsonSerializer.Serialize(either));
            }
        }

        [Fact]
        public void FromJson()
        {
            Assert.Equal(
                expected: new Either<int, string>(12),
                actual: JsonSerializer.Deserialize<Either<int, string>>("12"));

            Assert.Equal(
                expected: new Either<int, string>("hello"),
                actual: JsonSerializer.Deserialize<Either<int, string>>("\"hello\""));

            Assert.Equal(
                expected: new Either<string, int>(12),
                actual: JsonSerializer.Deserialize<Either<string, int>>("12"));

            Assert.Equal(
                expected: new Either<string, int>("hello"),
                actual: JsonSerializer.Deserialize<Either<string, int>>("\"hello\""));

            var deserializedPerson = JsonSerializer.Deserialize<Either<Person, string>>("{\"Name\":\"Mario\",\"Age\":42}");
            Assert.Equal(
                expected: "Mario",
                actual: deserializedPerson.GetLeft().First().Name);

            Assert.Equal(
                expected: 42,
                actual: deserializedPerson.GetLeft().First().Age);
        }

        [Fact]
        // https://github.com/recorefx/RecoreFX/issues/114
        // You can't deserialize JSON of type TRight when the JSON is a JSON object { ... }
        // and the left type has a converter that will partially deserialize JSON for the right type (will throw an exception)
        // or both types are using the fallback POCO deserialization (will always return TLeft).
        public void FromJsonBothRecordTypesAlwaysDeserializesLeft()
        {
            {
                var deserializedPerson = JsonSerializer.Deserialize<Either<Person, Address>>("{\"Name\":\"Mario\",\"Age\":42}");
                Assert.Equal(
                    expected: "Mario",
                    actual: deserializedPerson.GetLeft().First().Name);

                Assert.Equal(
                    expected: 42,
                    actual: deserializedPerson.GetLeft().First().Age);

                var deserializedAddress = JsonSerializer.Deserialize<Either<Person, Address>>("{\"Street\":\"123 Main St\",\"Zip\":\"12345\"}");
                //Assert.Equal(
                //    expected: "123 Main St",
                //    actual: deserializedAddress.GetRight().First().Street);

                //Assert.Equal(
                //    expected: "12345",
                //    actual: deserializedAddress.GetRight().First().Zip);

                // This is the bug. The commented-out code is the expected behavior.
                Assert.Null(deserializedAddress.GetLeft().First().Name);

                Assert.Equal(
                    expected: default,
                    actual: deserializedAddress.GetLeft().First().Age);
            }
            {
                var deserializedPerson = JsonSerializer.Deserialize<Either<Person, TypeWithConverter>>("{\"Name\":\"Mario\",\"Age\":42}");
                Assert.Equal(
                    expected: "Mario",
                    actual: deserializedPerson.GetLeft().First().Name);

                Assert.Equal(
                    expected: 42,
                    actual: deserializedPerson.GetLeft().First().Age);

                var deserializedAddress = JsonSerializer.Deserialize<Either<TypeWithConverter, Person>>("{\"fullName\":\"Brian C\",\"age\":28}");
                Assert.Equal(
                    expected: "Brian",
                    actual: deserializedAddress.GetLeft().First().Name);

                Assert.Equal(
                    expected: 28,
                    actual: deserializedAddress.GetLeft().First().Age);
            }
        }

        [Fact]
        // Here's a workaround you can apply for https://github.com/recorefx/RecoreFX/issues/114
        public void FromJsonBothRecordTypesWorkaround()
        {
            var deserializedPerson = JsonSerializer.Deserialize<PersonAddress>("{\"Name\":\"Mario\",\"Age\":42}").ToEither();
            Assert.Equal(
                expected: "Mario",
                actual: deserializedPerson.GetLeft().First().Name);

            Assert.Equal(
                expected: 42,
                actual: deserializedPerson.GetLeft().First().Age);

            var deserializedAddress = JsonSerializer.Deserialize<PersonAddress>("{\"Street\":\"123 Main St\",\"Zip\":\"12345\"}").ToEither();
            Assert.Equal(
                expected: "123 Main St",
                actual: deserializedAddress.GetRight().First().Street);

            Assert.Equal(
                expected: "12345",
                actual: deserializedAddress.GetRight().First().Zip);
        }
    }
}
