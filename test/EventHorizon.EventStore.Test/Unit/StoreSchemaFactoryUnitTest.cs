using System;
using System.Collections.Generic;
using System.Linq;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Schema;
using Xunit;
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class StoreSchemaFactoryUnitTest
{
    private readonly StoreSchemaFactory _factory = new();

    private class Nested
    {
        public string Name { get; set; }
        public Nested Loop { get; set; }
    }

    private class MappedState
    {
        public string Id { get; set; }

        [StoreField(FieldIntent.FullText)]
        public string Description { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string Sku { get; set; }

        [StoreField(Intent = FieldIntent.Sortable)]
        public decimal Price { get; set; }

        [StoreField(FieldIntent.NotQueried)]
        public Nested Payload { get; set; }

        [StoreField(Store = false)]
        public string Hidden { get; set; }

        public Nested Child { get; set; }
        public List<Nested> Children { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public int Count { get; set; }
        public DateTime? When { get; set; }
    }

    private class PlainState
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    private static StoreFieldSchema GetChild(StoreFieldSchema node, string propertyName) =>
        node.Children.Single(x => x.Property?.Name == propertyName);

    [Fact]
    public void SchemaIncludesEnvelopeAndStateSubtree()
    {
        var schema = _factory.GetSchema(typeof(Snapshot<MappedState>));

        var names = schema.Children.Select(x => x.Property.Name).ToArray();
        Assert.Contains("Id", names);
        Assert.Contains("SequenceId", names);
        Assert.Contains("State", names);
        Assert.Contains("CreatedDate", names);
        Assert.Contains("UpdatedDate", names);

        var state = GetChild(schema, "State");
        Assert.NotNull(state.Children);
        Assert.Contains("Description", state.Children.Select(x => x.Property.Name));
    }

    [Fact]
    public void IntentsAreReadFromAttributes()
    {
        var schema = _factory.GetSchema(typeof(Snapshot<MappedState>));
        var state = GetChild(schema, "State");

        Assert.Equal(FieldIntent.FullText, GetChild(state, "Description").Intent);
        Assert.Equal(FieldIntent.ExactMatch, GetChild(state, "Sku").Intent);
        Assert.Equal(FieldIntent.Sortable, GetChild(state, "Price").Intent);
        Assert.Equal(FieldIntent.NotQueried, GetChild(state, "Payload").Intent);
        Assert.False(GetChild(state, "Hidden").Store);
        Assert.Equal(FieldIntent.Default, GetChild(state, "Count").Intent);
    }

    [Fact]
    public void HasExplicitIntentsPropagatesToRoot()
    {
        Assert.True(_factory.GetSchema(typeof(Snapshot<MappedState>)).HasExplicitIntents);
        Assert.False(_factory.GetSchema(typeof(Snapshot<PlainState>)).HasExplicitIntents);
        Assert.False(_factory.GetSchema(typeof(Lock)).HasExplicitIntents);
    }

    [Fact]
    public void CollectionsUnwrapToElementType()
    {
        var state = GetChild(_factory.GetSchema(typeof(Snapshot<MappedState>)), "State");
        var children = GetChild(state, "Children");

        Assert.True(children.IsCollection);
        Assert.Equal(typeof(Nested), children.ClrType);
        Assert.NotNull(children.Children);
    }

    [Fact]
    public void NullablesUnwrapToUnderlyingType()
    {
        var state = GetChild(_factory.GetSchema(typeof(Snapshot<MappedState>)), "State");
        Assert.Equal(typeof(DateTime), GetChild(state, "When").ClrType);
    }

    [Fact]
    public void DictionariesAndCyclesAreOpaque()
    {
        var state = GetChild(_factory.GetSchema(typeof(Snapshot<MappedState>)), "State");

        var tags = GetChild(state, "Tags");
        Assert.True(tags.IsOpaque);
        Assert.Null(tags.Children);

        // Nested.Loop recurses into a type already on the path
        var child = GetChild(state, "Child");
        var loop = GetChild(child, "Loop");
        Assert.True(loop.IsOpaque);
    }

    [Fact]
    public void SchemasAreCachedPerType()
    {
        var first = _factory.GetSchema(typeof(Snapshot<MappedState>));
        var second = _factory.GetSchema(typeof(Snapshot<MappedState>));
        Assert.Same(first, second);
    }
}
