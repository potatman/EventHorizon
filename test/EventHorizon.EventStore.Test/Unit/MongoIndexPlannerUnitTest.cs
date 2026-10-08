using System;
using System.Collections.Generic;
using System.Linq;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.MongoDb;
using EventHorizon.EventStore.MongoDb.Attributes;
using EventHorizon.EventStore.MongoDb.Indexes;
using EventHorizon.EventStore.Schema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Xunit;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class MongoIndexPlannerUnitTest
{
    private sealed class SearchNested
    {
        public string Name { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string Code { get; set; }
    }

    private sealed class SearchState : IState
    {
        public string Id { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string Sku { get; set; }

        [StoreField(FieldIntent.Sortable)]
        public decimal Price { get; set; }

        [StoreField(FieldIntent.FullText)]
        public string Description { get; set; }

        [StoreField(FieldIntent.FullText | FieldIntent.ExactMatch)]
        public string Title { get; set; }

        [StoreField(FieldIntent.ExactMatch | FieldIntent.Sortable)]
        public string Code { get; set; }

        [BsonElement("cat")]
        [StoreField(FieldIntent.ExactMatch)]
        public string Category { get; set; }

        [BsonIgnore]
        [StoreField(FieldIntent.ExactMatch)]
        public string Transient { get; set; }

        [StoreField(FieldIntent.NotQueried)]
        public SearchNested Hidden { get; set; }

        public SearchNested Child { get; set; }
        public List<SearchNested> Items { get; set; }
        public string Unannotated { get; set; }
    }

    private sealed class ConventionState : IState
    {
        public string Id { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string SkuCode { get; set; }
    }

    private sealed class ClassMapState : IState
    {
        public string Id { get; set; }

        [StoreField(FieldIntent.ExactMatch)]
        public string Sku { get; set; }
    }

    private sealed class PlainState : IState
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    static MongoIndexPlannerUnitTest()
    {
        ConventionRegistry.Register(nameof(MongoIndexPlannerUnitTest),
            new ConventionPack { new CamelCaseElementNameConvention() },
            t => t == typeof(ConventionState));

        if (!BsonClassMap.IsClassMapRegistered(typeof(ClassMapState)))
            BsonClassMap.RegisterClassMap<ClassMapState>(cm =>
            {
                cm.AutoMap();
                cm.GetMemberMap(x => x.Sku).SetElementName("s");
            });
    }

    private static IReadOnlyList<MongoIndexSpec> BuildIntentIndexes<T>() =>
        MongoIndexPlanner.BuildIntentIndexes(StoreSchemaFactory.GetSchema(typeof(T)), typeof(T));

    private static string[] AscendingPaths(IEnumerable<MongoIndexSpec> specs) =>
        specs.Where(x => !x.IsText).Select(x => x.Keys.GetElement(0).Name).ToArray();

    #region Intent to index specs

    [Fact]
    public void ExactMatchAndSortableBecomeAscendingIndexes()
    {
        var specs = BuildIntentIndexes<View<SearchState>>();

        Assert.Equal(
            new[] { "State.Sku", "State.Price", "State.Title", "State.Code", "State.cat", "State.Child.Code", "State.Items.Code" },
            AscendingPaths(specs));

        var sku = specs.Single(x => x.Name == "State.Sku_1");
        Assert.Equal(new BsonDocument("State.Sku", 1), sku.Keys);
        Assert.Null(sku.ExpireAfter);
    }

    [Fact]
    public void FullTextFieldsShareOneTextIndex()
    {
        var specs = BuildIntentIndexes<View<SearchState>>();

        var text = Assert.Single(specs, x => x.IsText);
        Assert.Equal(MongoIndexPlanner.TextIndexName, text.Name);
        Assert.Equal(new BsonDocument { { "State.Description", "text" }, { "State.Title", "text" } }, text.Keys);
    }

    [Fact]
    public void NotQueriedUnserializedAndUnannotatedFieldsGetNoIndex()
    {
        var paths = BuildIntentIndexes<View<SearchState>>().SelectMany(x => x.Keys.Names).ToArray();

        Assert.DoesNotContain(paths, x => x.StartsWith("State.Hidden", StringComparison.Ordinal));
        Assert.DoesNotContain("State.Transient", paths);
        Assert.DoesNotContain("State.Unannotated", paths);
        Assert.DoesNotContain("State.Child.Name", paths);
    }

    [Fact]
    public void ElementNamesFollowConventions()
    {
        Assert.Equal(new[] { "State.skuCode" }, AscendingPaths(BuildIntentIndexes<View<ConventionState>>()));
    }

    [Fact]
    public void ElementNamesFollowRegisteredClassMaps()
    {
        Assert.Equal(new[] { "State.s" }, AscendingPaths(BuildIntentIndexes<View<ClassMapState>>()));
    }

    [Fact]
    public void ViewStoresIncludeIntentIndexes()
    {
        using var client = new MongoClient("mongodb://localhost:27017");
        var store = new MongoCrudStore<View<SearchState>>(client, new AttributeUtil(), "test_db");

        var names = store.GetIndexSpecs(null).Select(x => x.Name).ToArray();

        Assert.Contains("UpdatedDate_1", names);
        Assert.Contains("State.Sku_1", names);
        Assert.Contains(MongoIndexPlanner.TextIndexName, names);
    }

    [Fact]
    public void SnapshotStoresIgnoreIntents()
    {
        using var client = new MongoClient("mongodb://localhost:27017");
        var store = new MongoCrudStore<Snapshot<SearchState>>(client, new AttributeUtil(), "test_db");

        var specs = store.GetIndexSpecs(new MongoCollectionAttribute { TimeToLiveMs = 60_000 });

        Assert.Equal(new[] { "CreatedDate_1", "UpdatedDate_1" }, specs.Select(x => x.Name));
        Assert.Equal(TimeSpan.FromMinutes(1), specs[0].ExpireAfter);
    }

    [Fact]
    public void StatesWithoutIntentsGetOnlyBaseIndexes()
    {
        using var client = new MongoClient("mongodb://localhost:27017");
        var store = new MongoCrudStore<View<PlainState>>(client, new AttributeUtil(), "test_db");

        Assert.Equal(new[] { "UpdatedDate_1" }, store.GetIndexSpecs(null).Select(x => x.Name));
    }

    #endregion

    #region Reconciling with existing indexes

    private static readonly MongoIndexSpec SkuIndex = new("State.Sku_1", new BsonDocument("State.Sku", 1));

    private static readonly MongoIndexSpec TextIndex = new(MongoIndexPlanner.TextIndexName,
        new BsonDocument { { "State.Description", "text" }, { "State.Title", "text" } });

    private static BsonDocument Existing(string name, BsonDocument keys) =>
        new() { { "v", 2 }, { "key", keys }, { "name", name } };

    private static BsonDocument ExistingText(string name, params string[] fields) =>
        new()
        {
            { "v", 2 },
            { "key", new BsonDocument { { "_fts", "text" }, { "_ftsx", 1 } } },
            { "name", name },
            { "weights", new BsonDocument(fields.Select(x => new BsonElement(x, 1))) },
            { "default_language", "english" }
        };

    private static readonly BsonDocument IdIndex = Existing("_id_", new BsonDocument("_id", 1));

    [Fact]
    public void MissingIndexIsCreated()
    {
        var plan = MongoIndexPlanner.Plan(SkuIndex, new[] { IdIndex });
        Assert.Equal(MongoIndexAction.Create, plan.Action);
    }

    [Fact]
    public void SameKeysUnderAnotherNameAreReused()
    {
        var existing = new[] { IdIndex, Existing("sku_lookup", new BsonDocument("State.Sku", 1.0)) };
        Assert.Equal(MongoIndexAction.Exists, MongoIndexPlanner.Plan(SkuIndex, existing).Action);
    }

    [Fact]
    public void SameNameWithDifferentKeysIsAConflict()
    {
        var existing = new[] { IdIndex, Existing("State.Sku_1", new BsonDocument("State.Sku", -1)) };

        var plan = MongoIndexPlanner.Plan(SkuIndex, existing);

        Assert.Equal(MongoIndexAction.Conflict, plan.Action);
        Assert.Contains("State.Sku_1", plan.Reason);
    }

    [Fact]
    public void MatchingTextIndexIsReusedWhateverItsName()
    {
        var existing = new[] { IdIndex, ExistingText("search", "State.Title", "State.Description") };
        Assert.Equal(MongoIndexAction.Exists, MongoIndexPlanner.Plan(TextIndex, existing).Action);
    }

    [Fact]
    public void ChangedTextFieldsAreReportedAsDrift()
    {
        var existing = new[] { IdIndex, ExistingText(MongoIndexPlanner.TextIndexName, "State.Description") };

        var plan = MongoIndexPlanner.Plan(TextIndex, existing);

        Assert.Equal(MongoIndexAction.Conflict, plan.Action);
        Assert.Contains("State.Title", plan.Reason);
    }

    [Fact]
    public void ForeignTextIndexIsAConflict()
    {
        var existing = new[] { IdIndex, ExistingText("legacy_text", "Name") };
        Assert.Equal(MongoIndexAction.Conflict, MongoIndexPlanner.Plan(TextIndex, existing).Action);
    }

    [Fact]
    public void ChangedTimeToLiveIsReportedAsDrift()
    {
        var ttl = new MongoIndexSpec("CreatedDate_1", new BsonDocument("CreatedDate", 1), TimeSpan.FromMinutes(2));
        var existing = Existing("CreatedDate_1", new BsonDocument("CreatedDate", 1));
        existing["expireAfterSeconds"] = 60;

        Assert.Equal(MongoIndexAction.Conflict, MongoIndexPlanner.Plan(ttl, new[] { existing }).Action);

        existing["expireAfterSeconds"] = 120;
        Assert.Equal(MongoIndexAction.Exists, MongoIndexPlanner.Plan(ttl, new[] { existing }).Action);
    }

    #endregion
}
