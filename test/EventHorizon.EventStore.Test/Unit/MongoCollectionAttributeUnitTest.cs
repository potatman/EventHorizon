using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.MongoDb;
using EventHorizon.EventStore.MongoDb.Attributes;
using EventHorizon.EventStore.MongoDb.Models;
using MongoDB.Driver;
using Xunit;
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class MongoCollectionAttributeUnitTest
{
    // Constructing a client does not connect, so no MongoDB server is needed.
    private readonly IMongoClient _client = new MongoClient("mongodb://localhost:27017");
    private readonly AttributeUtil _attributeUtil = new();

    [Fact]
    public void SnapshotAndViewStoresUseTheStateClassAttribute()
    {
        var snapshot = new MongoCrudStore<Snapshot<FullySetState>>(_client, _attributeUtil, "db").Collection.Settings;
        var view = new MongoCrudStore<View<FullySetState>>(_client, _attributeUtil, "db").Collection.Settings;

        foreach (var settings in new[] { snapshot, view })
        {
            Assert.Equal(ReadPreference.SecondaryPreferred, settings.ReadPreference);
            Assert.Equal(ReadConcern.Majority, settings.ReadConcern);
            Assert.Equal(WriteConcern.WMajority, settings.WriteConcern);
        }
    }

    [Fact]
    public void UnassignedSettingsKeepTheClientDefaults()
    {
        var defaults = new MongoCrudStore<Snapshot<UnattributedState>>(_client, _attributeUtil, "db").Collection.Settings;
        var settings = new MongoCrudStore<Snapshot<WriteConcernOnlyState>>(_client, _attributeUtil, "db").Collection.Settings;

        Assert.Equal(WriteConcern.W2, settings.WriteConcern);
        Assert.Equal(defaults.ReadPreference, settings.ReadPreference);
        Assert.Equal(defaults.ReadConcern, settings.ReadConcern);
    }

    [Fact]
    public void LockStoreIsUnaffected()
    {
        var settings = new MongoCrudStore<Lock>(_client, _attributeUtil, "db").Collection.Settings;

        Assert.Equal(_client.Settings.WriteConcern, settings.WriteConcern);
    }

    [MongoCollection(ReadPreferenceMode = ReadPreferenceMode.SecondaryPreferred,
        ReadConcernLevel = ReadConcernLevel.Majority,
        WriteConcernLevel = WriteConcernLevel.Majority)]
    public class FullySetState : IState
    {
        public string Id { get; set; }
    }

    [MongoCollection(WriteConcernLevel = WriteConcernLevel.W2)]
    public class WriteConcernOnlyState : IState
    {
        public string Id { get; set; }
    }

    public class UnattributedState : IState
    {
        public string Id { get; set; }
    }
}
