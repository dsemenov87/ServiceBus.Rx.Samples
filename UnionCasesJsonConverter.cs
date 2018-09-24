using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RxAsync
{
    public static class UnionCasesJson
    {
        public const string Discriminator = "__Case";
    }

    public sealed class UnionCasesJsonWriter<TBaseMessage> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsSubclassOf(typeof(TBaseMessage)) && objectType.IsSealed;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var o = JObject.FromObject(value);
            o.AddFirst(new JProperty(UnionCasesJson.Discriminator, value.GetType().FullName));
            o.WriteTo(writer);
        }

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class UnionCasesJsonReader<TBaseMessage> : JsonConverter
    {
        static HashSet<JsonToken> Primitives => new HashSet<JsonToken> { 
                JsonToken.Boolean, JsonToken.Date, JsonToken.Float, JsonToken.Integer, 
                JsonToken.Null, JsonToken.String };

        public override bool CanConvert(Type objectType)
        {
            return objectType.Equals(typeof(TBaseMessage));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var parts =
                TokenSequence(reader)
                    .TakeWhile(x => x.token != JsonToken.EndObject)
                    .Pairwise()
                    .Where((pair, id) => id % 2 == 0)
                    .ToArray();

            var values =
                parts
                    .Where(x => x.Item1.value.ToString() != UnionCasesJson.Discriminator)
                    .Select(x => x.Item2)
                    .Where(x => Primitives.Contains(x.token))
                    .Select(x => x.value)
                    .ToArray();

            var abstractType = typeof(TBaseMessage);
            var unionCases =
                abstractType
                    .Assembly
                    .ExportedTypes
                    .Where(t => t.IsSubclassOf(abstractType) && t.IsSealed)
                    .ToArray();
            
            var unionCase =
                parts
                    .Where(x => x.Item1.value.ToString() == UnionCasesJson.Discriminator)
                    .Select(x => x.Item2.value)
                    .Cast<string>()
                    .FirstOrDefault();

            Type @case = null;
            if (unionCase != null)
            {
                @case = unionCases.FirstOrDefault(x => x.FullName == unionCase);
            }
            else if (values.Length == 1 && values[0] == null)
            {
                @case = unionCases.FirstOrDefault(t => !t.GetProperties().Any());
            }
            else
            {
                @case = unionCases.FirstOrDefault(t => t.GetProperties().Any());
            }

            var instance = Activator.CreateInstance(@case);

            @case.GetProperties()
                .Zip(values, (prop, v) => new {prop, v})
                .ToList()
                .ForEach(x => x.prop.SetValue(instance, Convert.ChangeType(x.v, x.prop.PropertyType), null));

            return instance;
        }

        private static IEnumerable<(JsonToken token, object value)> TokenSequence(JsonReader reader)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                yield return (JsonToken.Undefined, new object());
                yield return (reader.TokenType, reader.Value);
            }
            else
            {
                while (reader.Read())
                {
                    yield return (reader.TokenType, reader.Value);
                }
            }
        }
    }

    internal static class EnumerableExtensions
    {
        internal static IEnumerable<(T, T)> Pairwise<T>(this IEnumerable<T> enumerable)
        {
            var previous = default(T);

            using (var e = enumerable.GetEnumerator())
            {
                if (e.MoveNext())
                    previous = e.Current;

                while (e.MoveNext())
                    yield return (previous, previous = e.Current);
            }
        }
    }
}