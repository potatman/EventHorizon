using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Schema;
using MongoDB.Bson;
using Xunit;
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class StoreSchemaFactoryUnitTest
{
    private sealed class Nested
    {
        public string Name { get; set; }
        public Nested Loop { get; set; }
    }

    private sealed class MappedState
    {
        public string Id { get; set; }

        [StoreField(FieldIntent.FullText)]
        public string Description { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string Sku { get; set; }

        [StoreField(Intent = FieldIntent.Sortable)]
        public decimal Price { get; set; }

        [StoreField(FieldIntent.FullText | FieldIntent.ExactMatch)]
        public string Title { get; set; }

        [StoreField(FieldIntent.NotQueried)]
        public Nested Payload { get; set; }

        [StoreField]
        public string Pinned { get; set; }

        public Nested Child { get; set; }
        public List<Nested> Children { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public int Count { get; set; }
        public DateTime? When { get; set; }
    }

    private sealed class PlainState
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class OpaqueState
    {
        public BsonDocument Document { get; set; }
        public Type Kind { get; set; }
        public object Anything { get; set; }
        public IEnumerable Untyped { get; set; }
        public List<List<int>> Matrix { get; set; }
    }

    private class BaseState
    {
        public string Name { get; set; }
    }

    private sealed class DerivedState : BaseState
    {
        [StoreField(FieldIntent.ExactMatch)]
        public new int Name { get; set; }
    }

    private sealed class InvalidIntentState
    {
        [StoreField(FieldIntent.NotQueried | FieldIntent.FullText)]
        public string Name { get; set; }
    }

    private static StoreFieldSchema GetChild(StoreFieldSchema node, string propertyName) =>
        node.Children.Single(x => x.Property?.Name == propertyName);

    private static int CountNodes(StoreFieldSchema node) =>
        1 + (node.Children?.Sum(CountNodes) ?? 0);

    [Fact]
    public void SchemaIncludesEnvelopeAndStateSubtree()
    {
        var schema = StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>));

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
        var schema = StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>));
        var state = GetChild(schema, "State");

        Assert.Equal(FieldIntent.FullText, GetChild(state, "Description").Intent);
        Assert.Equal(FieldIntent.ExactMatch, GetChild(state, "Sku").Intent);
        Assert.Equal(FieldIntent.Sortable, GetChild(state, "Price").Intent);
        Assert.Equal(FieldIntent.FullText | FieldIntent.ExactMatch, GetChild(state, "Title").Intent);
        Assert.Equal(FieldIntent.NotQueried, GetChild(state, "Payload").Intent);
        Assert.Equal(FieldIntent.Default, GetChild(state, "Count").Intent);

        Assert.True(GetChild(state, "Pinned").IsAnnotated);
        Assert.Equal(FieldIntent.Default, GetChild(state, "Pinned").Intent);
        Assert.False(GetChild(state, "Count").IsAnnotated);
    }

    [Fact]
    public void NotQueriedCannotBeCombined()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StoreSchemaFactory.GetSchema(typeof(Snapshot<InvalidIntentState>)));
        Assert.Contains("NotQueried", ex.Message);
    }

    [Fact]
    public void HasExplicitIntentsPropagatesToRoot()
    {
        Assert.True(StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>)).HasExplicitIntents);
        Assert.False(StoreSchemaFactory.GetSchema(typeof(Snapshot<PlainState>)).HasExplicitIntents);
        Assert.False(StoreSchemaFactory.GetSchema(typeof(Lock)).HasExplicitIntents);
    }

    [Fact]
    public void CollectionsUnwrapToElementType()
    {
        var state = GetChild(StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>)), "State");
        var children = GetChild(state, "Children");

        Assert.True(children.IsCollection);
        Assert.Equal(typeof(Nested), children.ClrType);
        Assert.NotNull(children.Children);
    }

    [Fact]
    public void NullablesUnwrapToUnderlyingType()
    {
        var state = GetChild(StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>)), "State");
        Assert.Equal(typeof(DateTime), GetChild(state, "When").ClrType);
    }

    [Fact]
    public void DictionariesAndCyclesAreOpaque()
    {
        var state = GetChild(StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>)), "State");

        var tags = GetChild(state, "Tags");
        Assert.True(tags.IsOpaque);
        Assert.Null(tags.Children);

        // Nested.Loop recurses into a type already on the path
        var child = GetChild(state, "Child");
        var loop = GetChild(child, "Loop");
        Assert.True(loop.IsOpaque);
    }

    [Fact]
    public void FrameworkAndUnknownShapesAreOpaqueLeaves()
    {
        var schema = StoreSchemaFactory.GetSchema(typeof(OpaqueState));

        foreach (var name in new[] { "Document", "Kind", "Anything", "Untyped", "Matrix" })
        {
            var node = GetChild(schema, name);
            Assert.True(node.IsOpaque, name);
            Assert.Null(node.Children);
        }

        // BsonDocument and Type are not reflected into: the whole schema is the root plus five leaves
        Assert.Equal(6, CountNodes(schema));
    }

    [Fact]
    public void HiddenPropertyResolvesToMostDerivedDeclaration()
    {
        var schema = StoreSchemaFactory.GetSchema(typeof(DerivedState));

        var name = Assert.Single(schema.Children, x => x.Property.Name == "Name");
        Assert.Equal(typeof(DerivedState), name.Property.DeclaringType);
        Assert.Equal(typeof(int), name.ClrType);
        Assert.Equal(FieldIntent.ExactMatch, name.Intent);
    }

    [Fact]
    public void OversizedTypeGraphThrowsLimitException()
    {
        var ex = Assert.Throws<StoreSchemaLimitException>(() => StoreSchemaFactory.GetSchema(typeof(Fan0)));
        Assert.Contains(nameof(Fan0), ex.Message);

        // Failures are cached rather than rebuilt
        Assert.Throws<StoreSchemaLimitException>(() => StoreSchemaFactory.GetSchema(typeof(Fan0)));
    }

    [Fact]
    public void SchemasAreCachedPerType()
    {
        var first = StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>));
        var second = StoreSchemaFactory.GetSchema(typeof(Snapshot<MappedState>));
        Assert.Same(first, second);
    }

    #region Fan-out graph: 6 properties per level over 8 levels

    private sealed class Fan0 { public Fan1 A { get; set; } public Fan1 B { get; set; } public Fan1 C { get; set; } public Fan1 D { get; set; } public Fan1 E { get; set; } public Fan1 F { get; set; } }
    private sealed class Fan1 { public Fan2 A { get; set; } public Fan2 B { get; set; } public Fan2 C { get; set; } public Fan2 D { get; set; } public Fan2 E { get; set; } public Fan2 F { get; set; } }
    private sealed class Fan2 { public Fan3 A { get; set; } public Fan3 B { get; set; } public Fan3 C { get; set; } public Fan3 D { get; set; } public Fan3 E { get; set; } public Fan3 F { get; set; } }
    private sealed class Fan3 { public Fan4 A { get; set; } public Fan4 B { get; set; } public Fan4 C { get; set; } public Fan4 D { get; set; } public Fan4 E { get; set; } public Fan4 F { get; set; } }
    private sealed class Fan4 { public Fan5 A { get; set; } public Fan5 B { get; set; } public Fan5 C { get; set; } public Fan5 D { get; set; } public Fan5 E { get; set; } public Fan5 F { get; set; } }
    private sealed class Fan5 { public Fan6 A { get; set; } public Fan6 B { get; set; } public Fan6 C { get; set; } public Fan6 D { get; set; } public Fan6 E { get; set; } public Fan6 F { get; set; } }
    private sealed class Fan6 { public string A { get; set; } public string B { get; set; } public string C { get; set; } public string D { get; set; } public string E { get; set; } public string F { get; set; } }

    #endregion
}
