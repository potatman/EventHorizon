using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch.Mapping;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.EventStore.ElasticSearch;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Schema;
using Xunit;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class ElasticMappingBuilderUnitTest
{
    private readonly StoreSchemaFactory _factory = new();

    private class Nested
    {
        public string Name { get; set; }
    }

    private class MappedState
    {
        public string Id { get; set; }

        [StoreField(FieldIntent.FullText)]
        public string Description { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string Sku { get; set; }

        [StoreField(FieldIntent.NotQueried)]
        public Nested Payload { get; set; }

        [StoreField(FieldIntent.NotQueried)]
        public string RawText { get; set; }

        [StoreField(Store = false)]
        public string Hidden { get; set; }

        [JsonPropertyName("custom_name")]
        public string Renamed { get; set; }

        [JsonIgnore]
        public string Skipped { get; set; }

        public Nested Child { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public int Count { get; set; }
        public long Big { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }
        public DateTime When { get; set; }
        public Guid Key { get; set; }
    }

    private static T GetProperty<T>(Properties properties, string name) where T : class, IProperty
    {
        Assert.True(properties.TryGetProperty(name, out IProperty property), $"missing property '{name}'");
        var typed = property as T;
        Assert.NotNull(typed);
        return typed;
    }

    private Properties BuildStateProperties()
    {
        var schema = _factory.GetSchema(typeof(Snapshot<MappedState>));
        var props = ElasticMappingBuilder.BuildProperties(schema);
        var state = GetProperty<ObjectProperty>(props, "state");
        return state.Properties;
    }

    [Fact]
    public void EnvelopeMapsToNativeTypes()
    {
        var schema = _factory.GetSchema(typeof(Snapshot<MappedState>));
        var props = ElasticMappingBuilder.BuildProperties(schema);

        Assert.IsType<KeywordProperty>(GetProperty<KeywordProperty>(props, "id"));
        Assert.IsType<LongNumberProperty>(GetProperty<LongNumberProperty>(props, "sequenceId"));
        Assert.IsType<DateProperty>(GetProperty<DateProperty>(props, "createdDate"));
        Assert.IsType<DateProperty>(GetProperty<DateProperty>(props, "updatedDate"));

        var state = GetProperty<ObjectProperty>(props, "state");
        Assert.Equal(DynamicMapping.True, state.Dynamic);
    }

    [Fact]
    public void ConventionsMapClrTypesToEfficientDefaults()
    {
        var state = BuildStateProperties();

        var id = GetProperty<KeywordProperty>(state, "id");
        Assert.Equal(8191, id.IgnoreAbove);
        Assert.IsType<IntegerNumberProperty>(GetProperty<IntegerNumberProperty>(state, "count"));
        Assert.IsType<LongNumberProperty>(GetProperty<LongNumberProperty>(state, "big"));
        Assert.IsType<DoubleNumberProperty>(GetProperty<DoubleNumberProperty>(state, "price"));
        Assert.IsType<BooleanProperty>(GetProperty<BooleanProperty>(state, "active"));
        Assert.IsType<DateProperty>(GetProperty<DateProperty>(state, "when"));
        Assert.IsType<KeywordProperty>(GetProperty<KeywordProperty>(state, "key"));
    }

    [Fact]
    public void IntentsTranslateToElasticTypes()
    {
        var state = BuildStateProperties();

        Assert.IsType<TextProperty>(GetProperty<TextProperty>(state, "description"));

        var sku = GetProperty<KeywordProperty>(state, "sku");
        Assert.Null(sku.IgnoreAbove);

        var payload = GetProperty<ObjectProperty>(state, "payload");
        Assert.False(payload.Enabled);

        var rawText = GetProperty<TextProperty>(state, "rawText");
        Assert.False(rawText.Index);
    }

    [Fact]
    public void ObjectsRecurseAndUnknownShapesStayDynamic()
    {
        var state = BuildStateProperties();

        var child = GetProperty<ObjectProperty>(state, "child");
        Assert.Equal(DynamicMapping.True, child.Dynamic);
        Assert.IsType<KeywordProperty>(GetProperty<KeywordProperty>(child.Properties, "name"));

        var tags = GetProperty<ObjectProperty>(state, "tags");
        Assert.Equal(DynamicMapping.True, tags.Dynamic);
        Assert.Null(tags.Properties);
    }

    [Fact]
    public void NamingFollowsSerializer()
    {
        var state = BuildStateProperties();

        Assert.True(state.TryGetProperty("custom_name", out IProperty _));
        Assert.False(state.TryGetProperty("renamed", out IProperty _));
        Assert.False(state.TryGetProperty("skipped", out IProperty _));
    }

    [Fact]
    public void StoreFalseBecomesSourceExclude()
    {
        var schema = _factory.GetSchema(typeof(Snapshot<MappedState>));
        var excludes = ElasticMappingBuilder.GetSourceExcludes(schema);

        Assert.Equal(new[] { "state.hidden" }, excludes);
    }
}
