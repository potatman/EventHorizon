using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch.Mapping;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.EventStore.ElasticSearch;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Schema;
using MongoDB.Bson;
using Xunit;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class ElasticMappingBuilderUnitTest
{
    private sealed class Nested
    {
        public string Name { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string Code { get; set; }
    }

    private sealed class MappedState
    {
        public string Id { get; set; }

        [StoreField(FieldIntent.FullText)]
        public string Description { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string Sku { get; set; }

        [StoreField(FieldIntent.Sortable)]
        public string SortKey { get; set; }

        [StoreField(FieldIntent.FullText | FieldIntent.ExactMatch)]
        public string Title { get; set; }

        [StoreField(FieldIntent.Sortable)]
        public int Rank { get; set; }

        [StoreField(FieldIntent.NotQueried)]
        public Nested Payload { get; set; }

        [StoreField(FieldIntent.NotQueried)]
        public string RawText { get; set; }

        [StoreField(FieldIntent.NotQueried)]
        public object Blob { get; set; }

        [JsonPropertyName("custom_name")]
        [StoreField(FieldIntent.ExactMatch)]
        public string Renamed { get; set; }

        [JsonIgnore]
        [StoreField(FieldIntent.ExactMatch)]
        public string Skipped { get; set; }

        public Nested Child { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public int Count { get; set; }
        public long Big { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }
        public DateTime When { get; set; }
        public Guid Key { get; set; }

        public object Anything { get; set; }
        public IEnumerable Untyped { get; set; }
        public BsonDocument Document { get; set; }
        public Type Kind { get; set; }

        public float[] Embedding { get; set; }
        public List<double> Scores { get; set; }

        [StoreField]
        public float[] Weights { get; set; }

        public string[] Labels { get; set; }
    }

    private class BaseState
    {
        public string Name { get; set; }
    }

    private sealed class HidingState : BaseState
    {
        [StoreField(FieldIntent.Sortable)]
        public new int Name { get; set; }
    }

    private static T GetProperty<T>(Properties properties, string name) where T : class, IProperty
    {
        Assert.True(properties.TryGetProperty(name, out IProperty property), $"missing property '{name}'");
        var typed = property as T;
        Assert.NotNull(typed);
        return typed;
    }

    private static void AssertAbsent(Properties properties, params string[] names)
    {
        foreach (var name in names)
            Assert.False(properties.TryGetProperty(name, out IProperty _), $"unexpected property '{name}'");
    }

    private static Properties BuildStateProperties(bool mapUnannotated)
    {
        var schema = StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>));
        var props = ElasticMappingBuilder.BuildProperties(schema, mapUnannotated);
        return GetProperty<ObjectProperty>(props, "state").Properties;
    }

    [Fact]
    public void AnnotatedOnlyMapsJustAnnotatedFields()
    {
        var schema = StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>));
        var props = ElasticMappingBuilder.BuildProperties(schema, mapUnannotated: false);

        // Envelope fields are unannotated and stay dynamic
        AssertAbsent(props, "id", "sequenceId", "createdDate", "updatedDate");

        var state = GetProperty<ObjectProperty>(props, "state").Properties;
        GetProperty<TextProperty>(state, "description");
        GetProperty<KeywordProperty>(state, "sku");
        AssertAbsent(state, "id", "count", "big", "price", "active", "when", "key", "tags", "labels", "embedding");

        // Objects appear only for annotated descendants
        var child = GetProperty<ObjectProperty>(state, "child");
        GetProperty<KeywordProperty>(child.Properties, "code");
        AssertAbsent(child.Properties, "name");
    }

    [Fact]
    public void StaticMapsEnvelopeToNativeTypes()
    {
        var schema = StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>));
        var props = ElasticMappingBuilder.BuildProperties(schema, mapUnannotated: true);

        GetProperty<KeywordProperty>(props, "id");
        GetProperty<LongNumberProperty>(props, "sequenceId");
        GetProperty<DateProperty>(props, "createdDate");
        GetProperty<DateProperty>(props, "updatedDate");

        // Nested objects inherit the root's dynamic setting
        var state = GetProperty<ObjectProperty>(props, "state");
        Assert.Null(state.Dynamic);
    }

    [Fact]
    public void StaticConventionsMapClrTypesToEfficientDefaults()
    {
        var state = BuildStateProperties(mapUnannotated: true);

        var id = GetProperty<KeywordProperty>(state, "id");
        Assert.Equal(ElasticMappingBuilder.DefaultIgnoreAbove, id.IgnoreAbove);
        GetProperty<IntegerNumberProperty>(state, "count");
        GetProperty<LongNumberProperty>(state, "big");
        GetProperty<DoubleNumberProperty>(state, "price");
        GetProperty<BooleanProperty>(state, "active");
        GetProperty<DateProperty>(state, "when");
        GetProperty<KeywordProperty>(state, "key");
        GetProperty<KeywordProperty>(state, "labels");

        var child = GetProperty<ObjectProperty>(state, "child");
        GetProperty<KeywordProperty>(child.Properties, "name");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IntentsTranslateToElasticTypes(bool mapUnannotated)
    {
        var state = BuildStateProperties(mapUnannotated);

        var description = GetProperty<TextProperty>(state, "description");
        Assert.Null(description.Fields);

        var sku = GetProperty<KeywordProperty>(state, "sku");
        Assert.Equal(ElasticMappingBuilder.DefaultIgnoreAbove, sku.IgnoreAbove);

        var sortKey = GetProperty<KeywordProperty>(state, "sortKey");
        Assert.Equal(ElasticMappingBuilder.DefaultIgnoreAbove, sortKey.IgnoreAbove);

        // Sortable on a number keeps its native type
        GetProperty<IntegerNumberProperty>(state, "rank");

        var payload = GetProperty<ObjectProperty>(state, "payload");
        Assert.False(payload.Enabled);

        var rawText = GetProperty<TextProperty>(state, "rawText");
        Assert.False(rawText.Index);

        var blob = GetProperty<ObjectProperty>(state, "blob");
        Assert.False(blob.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedFullTextAndExactMatchAddsKeywordSubField(bool mapUnannotated)
    {
        var state = BuildStateProperties(mapUnannotated);

        var title = GetProperty<TextProperty>(state, "title");
        var keyword = GetProperty<KeywordProperty>(title.Fields, ElasticMappingBuilder.KeywordSubField);
        Assert.Equal("keyword", ElasticMappingBuilder.KeywordSubField);
        Assert.Equal(ElasticMappingBuilder.DefaultIgnoreAbove, keyword.IgnoreAbove);
    }

    [Fact]
    public void UnknownShapesStayDynamic()
    {
        var state = BuildStateProperties(mapUnannotated: true);

        // object, non-generic IEnumerable, driver and framework types may serialize as scalars
        AssertAbsent(state, "anything", "untyped", "document", "kind", "tags");
    }

    [Fact]
    public void NumericArraysStayDynamicUnlessAnnotated()
    {
        var state = BuildStateProperties(mapUnannotated: true);

        AssertAbsent(state, "embedding", "scores");
        GetProperty<FloatNumberProperty>(state, "weights");
    }

    [Fact]
    public void NamingFollowsSerializer()
    {
        var state = BuildStateProperties(mapUnannotated: true);

        GetProperty<KeywordProperty>(state, "custom_name");
        AssertAbsent(state, "renamed", "skipped");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenPropertyMapsMostDerivedDeclaration(bool mapUnannotated)
    {
        var schema = StoreSchemaFactory.GetSchema(typeof(HidingState));
        var props = ElasticMappingBuilder.BuildProperties(schema, mapUnannotated);

        GetProperty<IntegerNumberProperty>(props, "name");
    }
}
