using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch.Mapping;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.EventStore.Schema;

namespace EventHorizon.EventStore.ElasticSearch;

/// <summary>
/// Translates the store-agnostic <see cref="StoreFieldSchema"/> into ElasticSearch static
/// mapping properties. Field names follow the client's source serializer (camelCase, honoring
/// <see cref="JsonPropertyNameAttribute"/>), so the mapping lines up with indexed documents.
/// Fields left out of the result are mapped dynamically (or by index templates) at first use.
/// </summary>
internal static class ElasticMappingBuilder
{
    /// <summary>
    /// Matches dynamic mapping's keyword limit. Longer values are not indexed but remain in
    /// _source; without it a value over Lucene's 32766-byte term cap rejects the whole document.
    /// </summary>
    internal const int DefaultIgnoreAbove = 8191;

    /// <summary>Keyword sub-field name, matching dynamic mapping so <c>field.keyword</c> queries keep working.</summary>
    internal const string KeywordSubField = "keyword";

    /// <summary>
    /// Builds mapping properties for <paramref name="node"/>'s children.
    /// </summary>
    /// <param name="node">Schema node whose children are mapped.</param>
    /// <param name="mapUnannotated">
    /// True maps every statically knowable field by CLR-type conventions; false maps only fields
    /// annotated with <see cref="StoreFieldAttribute"/> (and the objects containing them).
    /// </param>
    public static Properties BuildProperties(StoreFieldSchema node, bool mapUnannotated)
    {
        var properties = new Properties();
        foreach (var child in node.Children ?? Array.Empty<StoreFieldSchema>())
        {
            if (IsIgnored(child.Property)) continue;

            var name = GetFieldName(child.Property);
            if (properties.TryGetProperty(name, out IProperty _)) continue;

            var property = CreateProperty(child, mapUnannotated);
            if (property is not null)
                properties.Add(name, property);
        }

        return properties;
    }

    private static IProperty CreateProperty(StoreFieldSchema node, bool mapUnannotated)
    {
        if (!mapUnannotated && !node.HasExplicitIntents)
            return null;

        var isObject = node.Children is not null || node.IsOpaque;

        // enabled:false skips parsing entirely, so it also accepts scalars written to unknown shapes
        if (node.Intent == FieldIntent.NotQueried)
            return isObject ? new ObjectProperty { Enabled = false } : CreateNotQueriedLeaf(node.ClrType);

        if (isObject)
        {
            // Unknown shapes (object, framework types, custom converters) may serialize as scalars
            if (node.IsOpaque || HasCustomConverter(node))
                return null;

            return new ObjectProperty { Properties = BuildProperties(node, mapUnannotated) };
        }

        if (node.IsAnnotated)
            return CreateIntentLeaf(node);

        // Numeric arrays are often vectors; index templates or dynamic mapping decide their type
        if (node.IsCollection && IsNumeric(node.ClrType))
            return null;

        return CreateDefaultLeaf(node.ClrType);
    }

    private static IProperty CreateIntentLeaf(StoreFieldSchema node)
    {
        var exact = (node.Intent & (FieldIntent.ExactMatch | FieldIntent.Sortable)) != 0;
        var fullText = node.Intent.HasFlag(FieldIntent.FullText);

        if (fullText && node.ClrType == typeof(string))
        {
            var text = new TextProperty();
            if (exact)
                text.Fields = new Properties { { KeywordSubField, CreateKeyword() } };
            return text;
        }

        if (exact && IsKeywordType(node.ClrType))
            return CreateKeyword();

        return CreateDefaultLeaf(node.ClrType);
    }

    private static KeywordProperty CreateKeyword() => new() { IgnoreAbove = DefaultIgnoreAbove };

    private static IProperty CreateDefaultLeaf(Type type)
    {
        if (type == typeof(bool)) return new BooleanProperty();
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly)) return new DateProperty();
        if (type == typeof(byte[])) return new BinaryProperty();
        if (type == typeof(int) || type == typeof(ushort)) return new IntegerNumberProperty();
        if (type == typeof(long) || type == typeof(uint)) return new LongNumberProperty();
        if (type == typeof(ulong)) return new UnsignedLongNumberProperty();
        if (type == typeof(short) || type == typeof(byte)) return new ShortNumberProperty();
        if (type == typeof(sbyte)) return new ByteNumberProperty();
        if (type == typeof(float)) return new FloatNumberProperty();
        if (type == typeof(double) || type == typeof(decimal)) return new DoubleNumberProperty();

        // string, Guid, char, enum, TimeSpan, TimeOnly, Uri and unknown scalars
        return CreateKeyword();
    }

    private static IProperty CreateNotQueriedLeaf(Type type)
    {
        // text with index:false builds no index structures, has no term-size limits and
        // keeps the value in _source
        if (IsKeywordType(type))
            return new TextProperty { Index = false, Norms = false };
        if (type == typeof(bool))
            return new BooleanProperty { Index = false, DocValues = false };
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly))
            return new DateProperty { Index = false, DocValues = false };
        if (type == typeof(byte[]))
            return new BinaryProperty();
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return new DoubleNumberProperty { Index = false, DocValues = false };
        if (type == typeof(ulong))
            return new UnsignedLongNumberProperty { Index = false, DocValues = false };
        if (IsNumeric(type))
            return new LongNumberProperty { Index = false, DocValues = false };

        return new TextProperty { Index = false, Norms = false };
    }

    private static bool IsKeywordType(Type type) =>
        type == typeof(string)
        || type == typeof(Guid)
        || type == typeof(char)
        || type == typeof(TimeSpan)
        || type == typeof(TimeOnly)
        || type == typeof(Uri)
        || type.IsEnum;

    private static bool IsNumeric(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(sbyte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong)
        || type == typeof(float) || type == typeof(double) || type == typeof(decimal);

    private static bool HasCustomConverter(StoreFieldSchema node) =>
        node.Property?.GetCustomAttribute<JsonConverterAttribute>(true) is not null
        || node.ClrType.GetCustomAttribute<JsonConverterAttribute>(true) is not null;

    public static string GetFieldName(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>(true)?.Name
        ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    private static bool IsIgnored(PropertyInfo property) =>
        property.GetCustomAttribute<JsonIgnoreAttribute>(true) is { Condition: JsonIgnoreCondition.Always };
}
