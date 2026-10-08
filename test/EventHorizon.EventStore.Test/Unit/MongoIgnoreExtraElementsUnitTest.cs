using System;
using System.Collections.Generic;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.MongoDb;
using EventHorizon.EventStore.MongoDb.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace EventHorizon.EventStore.Test.Unit;

// Each test uses its own state types: the driver caches class maps (and the conventions applied to them)
// for the life of the process.
[Trait("Category", "Unit")]
public class MongoIgnoreExtraElementsUnitTest
{
    [Fact]
    public void SnapshotWithRemovedFieldsLoadsByDefault()
    {
        CreateFactory<TolerantState>(new MongoConfig { ConnectionString = "mongodb://localhost:27017" });

        var snapshot = BsonSerializer.Deserialize<Snapshot<TolerantState>>(DocumentWithExtraElements());

        Assert.Equal("abc", snapshot.Id);
        Assert.Equal("Acme", snapshot.State.Name);
        Assert.Equal(7, snapshot.State.Nested.Value);
        Assert.Equal("first", snapshot.State.Items[0].Label);
        Assert.Equal("keyed", snapshot.State.ByKey["k"].Label);
    }

    [Fact]
    public void ViewWithRemovedFieldsLoadsByDefault()
    {
        CreateFactory<TolerantViewState>(new MongoConfig { ConnectionString = "mongodb://localhost:27017" });

        var doc = DocumentWithExtraElements();
        doc.Remove("SequenceId");
        var view = BsonSerializer.Deserialize<View<TolerantViewState>>(doc);

        Assert.Equal("Acme", view.State.Name);
    }

    [Fact]
    public void OptingOutKeepsTheDriverDefault()
    {
        CreateFactory<StrictState>(new MongoConfig { ConnectionString = "mongodb://localhost:27017", IgnoreExtraElements = false });

        Assert.Throws<FormatException>(() => BsonSerializer.Deserialize<Snapshot<StrictState>>(DocumentWithExtraElements()));
    }

    private static void CreateFactory<T>(MongoConfig config) where T : class, IState
    {
        _ = new MongoStoreFactory<T>(Options.Create(config), new AttributeUtil());
    }

    // Shaped like a snapshot written by an older version whose classes had more members at every level.
    private static BsonDocument DocumentWithExtraElements()
    {
        BsonDocument Item(string label) => new() { { "Label", label }, { "RemovedItemField", 1 } };

        return new BsonDocument
        {
            { "_id", "abc" },
            { "SequenceId", 3L },
            { "CreatedDate", DateTime.UtcNow },
            { "UpdatedDate", DateTime.UtcNow },
            { "RemovedSnapshotField", "x" },
            { "State", new BsonDocument
                {
                    { "Id", "abc" },
                    { "Name", "Acme" },
                    { "Classification", "removed state field" },
                    { "Nested", new BsonDocument { { "Value", 7 }, { "RemovedNestedField", true } } },
                    { "Items", new BsonArray { Item("first") } },
                    { "ByKey", new BsonDocument { { "k", Item("keyed") } } }
                }
            }
        };
    }

    public class NestedValue
    {
        public int Value { get; set; }
    }

    public class ItemValue
    {
        public string Label { get; set; }
    }

    public class TolerantState : IState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public NestedValue Nested { get; set; }
        public List<ItemValue> Items { get; set; }
        public Dictionary<string, ItemValue> ByKey { get; set; }
    }

    public class TolerantViewState : IState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public NestedValue Nested { get; set; }
        public List<ItemValue> Items { get; set; }
        public Dictionary<string, ItemValue> ByKey { get; set; }
    }

    public class StrictState : IState
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
