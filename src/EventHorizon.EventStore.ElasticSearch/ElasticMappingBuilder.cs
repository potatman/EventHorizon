using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch.Mapping;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.EventStore.Schema;

namespace EventHorizon.EventStore.ElasticSearch;

/// <summary>
/// Translates the store-agnostic <see cref="StoreFieldSchema"/> into an ElasticSearch static
/// mapping. Field names follow the client's source serializer (camelCase, honoring
/// <see cref="JsonPropertyNameAttribute"/>), so the mapping lines up with indexed documents.
/// </summary>
public static class ElasticMappingBuilder
{
    /// <summary>
    /// Dynamic mapping indexes strings as text plus a keyword subfield; conventions here use a
    /// plain keyword instead. Values longer than this are not indexed (Lucene caps terms at
    /// 32766 bytes) but remain in _source.
    /// </summary>
    private const int DefaultIgnoreAbove = 8191;

    public static Properties BuildProperties(StoreFieldSchema node)
    {
        var properties = new Properties();
        foreach (var child in node.Children ?? Array.Empty<StoreFieldSchema>())
        {
            if (IsIgnored(child.Property)) continue;
            properties.Add(GetFieldName(child.Property), CreateProperty(child));
        }

        return properties;
    }

    public static ICollection<string> GetSourceExcludes(StoreFieldSchema root)
    {
        var excludes = new List<string>();
        CollectExcludes(root, string.Empty, excludes);
        return excludes;
    }

    private static IProperty CreateProperty(StoreFieldSchema node)
    {
        // Objects: map known children statically, let unknown shapes stay dynamic
        if (node.Children != null || node.IsOpaque)
        {
            if (node.Intent == FieldIntent.NotQueried)
                return new ObjectProperty { Enabled = false };

            return new ObjectProperty
            {
                Dynamic = DynamicMapping.True,
                Properties = node.Children != null ? BuildProperties(node) : null
            };
        }

        return node.Intent switch
        {
            FieldIntent.NotQueried => CreateNotQueriedLeaf(node.ClrType),
            FieldIntent.FullText when node.ClrType == typeof(string) => new TextProperty(),
            FieldIntent.ExactMatch or FieldIntent.Sortable when IsKeywordType(node.ClrType) => new KeywordProperty(),
            _ => CreateDefaultLeaf(node.ClrType)
        };
    }

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
        return new KeywordProperty { IgnoreAbove = DefaultIgnoreAbove };
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
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
            || type == typeof(sbyte) || type == typeof(ushort) || type == typeof(uint))
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

    private static void CollectExcludes(StoreFieldSchema node, string prefix, List<string> excludes)
    {
        foreach (var child in node.Children ?? Array.Empty<StoreFieldSchema>())
        {
            if (IsIgnored(child.Property)) continue;
            var path = prefix.Length == 0
                ? GetFieldName(child.Property)
                : prefix + "." + GetFieldName(child.Property);

            if (!child.Store)
            {
                excludes.Add(path);
                continue;
            }

            CollectExcludes(child, path, excludes);
        }
    }

    public static string GetFieldName(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>(true)?.Name
        ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    private static bool IsIgnored(PropertyInfo property) =>
        property.GetCustomAttribute<JsonIgnoreAttribute>(true) is { Condition: JsonIgnoreCondition.Always };
}
