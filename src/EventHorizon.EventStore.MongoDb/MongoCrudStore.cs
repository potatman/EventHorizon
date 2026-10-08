using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.MongoDb.Attributes;
using EventHorizon.EventStore.MongoDb.Indexes;
using EventHorizon.EventStore.MongoDb.Models;
using EventHorizon.EventStore.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EventHorizon.EventStore.MongoDb;

public class MongoCrudStore<T> : ICrudStore<T>
    where T : ICrudEntity
{
    private const string Id = "_id";
    private const string Document = "Document";
    private const string UpdatedDate1 = "UpdatedDate_1";
    private const string CreatedDate1 = "CreatedDate_1";
    private const string Tilda1 = "`1";
    private const string ErrorPrefix = "_id: \"";
    private const string ErrorPostfix = "\" }";

    // IndexAlreadyExists, IndexOptionsConflict, IndexKeySpecsConflict
    private static readonly int[] IndexConflictCodes = { 68, 85, 86 };

    // StoreField intents become indexes only on view collections: snapshot collections are
    // write-heavy and only read by id, so secondary indexes there cost writes without serving queries.
    private static readonly bool IsViewStore = typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(View<>);

    private readonly string _bucketId;
    private readonly IMongoClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILogger<MongoCrudStore<T>> _logger;
    private readonly IMongoCollection<T> _collection;

    public MongoCrudStore(IMongoClient client, AttributeUtil attributeUtil, string bucketId)
        : this(client, attributeUtil, bucketId, NullLogger<MongoCrudStore<T>>.Instance)
    {
    }

    public MongoCrudStore(IMongoClient client, AttributeUtil attributeUtil, string bucketId, ILogger<MongoCrudStore<T>> logger)
    {
        _client = client;
        _attributeUtil = attributeUtil;
        _logger = logger ?? NullLogger<MongoCrudStore<T>>.Instance;
        _bucketId = bucketId;
        var type = typeof(T);
        var database = client.GetDatabase(bucketId);
        var typeName = type.Name.Replace(Tilda1, string.Empty);
        _collection = database.GetCollection<T>(typeName);
    }

    public async Task SetupAsync(CancellationToken ct)
    {
        var mongoAttr = _attributeUtil.GetOne<MongoCollectionAttribute>(typeof(T));
        if (mongoAttr?.ReadConcernLevel != null) _collection.WithReadConcern(new ReadConcern(mongoAttr.ReadConcernLevel));
        if (mongoAttr?.ReadPreferenceMode != null) _collection.WithReadPreference(new ReadPreference(mongoAttr.ReadPreferenceMode));
        if (mongoAttr?.WriteConcernLevel != null)
        {
            switch (mongoAttr.WriteConcernLevel)
            {
                case WriteConcernLevel.Acknowledged: _collection.WithWriteConcern(WriteConcern.Acknowledged); break;
                case WriteConcernLevel.Unacknowledged: _collection.WithWriteConcern(WriteConcern.Unacknowledged); break;
                case WriteConcernLevel.W1: _collection.WithWriteConcern(WriteConcern.W1); break;
                case WriteConcernLevel.W2: _collection.WithWriteConcern(WriteConcern.W2); break;
                case WriteConcernLevel.W3: _collection.WithWriteConcern(WriteConcern.W3); break;
                case WriteConcernLevel.Majority: _collection.WithWriteConcern(WriteConcern.WMajority); break;
            }
        }

        var existing = await (await _collection.Indexes.ListAsync(ct)).ToListAsync(ct);
        foreach (var spec in GetIndexSpecs(mongoAttr))
            await EnsureIndexAsync(spec, existing, ct);
    }

    internal IReadOnlyList<MongoIndexSpec> GetIndexSpecs(MongoCollectionAttribute mongoAttr)
    {
        var specs = new List<MongoIndexSpec>();

        if (mongoAttr?.TimeToLiveMs != null)
            specs.Add(new MongoIndexSpec(CreatedDate1,
                new BsonDocument(GetElementName(nameof(ICrudEntity.CreatedDate)), 1),
                TimeSpan.FromMilliseconds(mongoAttr.TimeToLiveMs)));

        specs.Add(new MongoIndexSpec(UpdatedDate1, new BsonDocument(GetElementName(nameof(ICrudEntity.UpdatedDate)), 1)));

        specs.AddRange(GetIntentIndexSpecs());
        return specs;
    }

    private IReadOnlyList<MongoIndexSpec> GetIntentIndexSpecs()
    {
        if (!IsViewStore) return Array.Empty<MongoIndexSpec>();

        StoreFieldSchema schema;
        try
        {
            schema = StoreSchemaFactory.GetSchema(typeof(T));
        }
        catch (StoreSchemaLimitException ex)
        {
            _logger.LogWarning(ex, "Collection {Collection}: StoreField indexes skipped: {Reason}",
                _collection.CollectionNamespace.FullName, ex.Message);
            return Array.Empty<MongoIndexSpec>();
        }

        return schema.HasExplicitIntents
            ? MongoIndexPlanner.BuildIntentIndexes(schema, typeof(T))
            : Array.Empty<MongoIndexSpec>();
    }

    private static string GetElementName(string memberName) =>
        MongoIndexPlanner.GetElementName(typeof(T), memberName) ?? memberName;

    /// <summary>
    /// Creates the index unless an index with the same key specification exists. Conflicting or
    /// drifted indexes are logged, never dropped or allowed to fail setup.
    /// </summary>
    private async Task EnsureIndexAsync(MongoIndexSpec spec, IReadOnlyCollection<BsonDocument> existing, CancellationToken ct)
    {
        var plan = MongoIndexPlanner.Plan(spec, existing);
        if (plan.Action == MongoIndexAction.Exists) return;

        if (plan.Action == MongoIndexAction.Conflict)
        {
            _logger.LogWarning("Collection {Collection}: index {Index} not created: {Reason}",
                _collection.CollectionNamespace.FullName, spec.Name, plan.Reason);
            return;
        }

        var opts = new CreateIndexOptions { Background = true, ExpireAfter = spec.ExpireAfter, Name = spec.Name };
        try
        {
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(spec.Keys, opts), cancellationToken: ct);
        }
        catch (MongoCommandException ex) when (IndexConflictCodes.Contains(ex.Code))
        {
            _logger.LogWarning(ex, "Collection {Collection}: index {Index} not created: {Reason}",
                _collection.CollectionNamespace.FullName, spec.Name, ex.Message);
        }
    }

    public async Task<T[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        var filter = Builders<T>.Filter.In(Id, ids);
        var objs = await _collection
            .Find(filter)
            .ToListAsync(ct);

        return objs.ToArray();
    }

    public async Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        var result = await _collection.Find(Builders<T>.Filter.Empty)
            .Project(x => x.UpdatedDate)
            .SortByDescending(x => x.UpdatedDate)
            .FirstOrDefaultAsync(cancellationToken: ct);

        return result;
    }

    public async Task<DbResult> InsertAsync(T[] objs, CancellationToken ct)
    {
        var result = new DbResult();
        try
        {
            await _collection.InsertManyAsync(objs, new InsertManyOptions(), ct);
            result.PassedIds = objs.Select(x => x.Id).ToArray();
            result.FailedIds = Array.Empty<string>();
        }
        catch (MongoBulkWriteException<T> ex)
        {
            var dupeKeyError = ex.WriteErrors.FirstOrDefault(x => x.Code == 11000);
            if (dupeKeyError == null) throw;

            // Note: First Id is not in UnprocessedRequests
            var firstId = dupeKeyError.Message.Split(ErrorPrefix)[1].Replace(ErrorPostfix, string.Empty);

            // Get FailedIds w/ FirstId
            var failedIds = ex.UnprocessedRequests
                .Select(x => x.ToBsonDocument()[Document][Id].AsString)
                .Concat(new[] { firstId })
                .Distinct()
                .ToArray();

            // Store passed and failed
            result.FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).Distinct().ToArray();
            result.PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).Distinct().ToArray();
        }

        return result;
    }

    public async Task<DbResult> UpsertAsync(T[] objs, CancellationToken ct)
    {
        var result = new DbResult();
        try
        {
            var ops = new List<WriteModel<T>>();
            foreach (var obj in objs)
            {
                var filter = Builders<T>.Filter.Eq(Id, obj.Id);
                ops.Add(new ReplaceOneModel<T>(filter, obj) { IsUpsert = true });
            }

            await _collection.BulkWriteAsync(ops, cancellationToken: ct);
            result.PassedIds = objs.Select(x => x.Id).ToArray();
            result.FailedIds = Array.Empty<string>();
        }
        catch (MongoBulkWriteException<T> ex)
        {
            var dupeKeyError = ex.WriteErrors.FirstOrDefault(x => x.Code == 11000);
            if (dupeKeyError == null) throw;

            // Note: First Id is not in UnprocessedRequests
            var firstId = dupeKeyError.Message.Split(ErrorPrefix)[1].Replace(ErrorPostfix, string.Empty);

            // Get FailedIds w/ FirstId
            var failedIds = ex.UnprocessedRequests
                .Select(x => x.ToBsonDocument()[Document][Id].AsString)
                .Concat(new[] { firstId })
                .Distinct()
                .ToArray();

            // Store passed and failed
            result.FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).Distinct().ToArray();
            result.PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).Distinct().ToArray();
        }

        return result;
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct)
    {
        var filter = Builders<T>.Filter.In(Id, ids);
        await _collection.DeleteManyAsync(filter, ct);
    }

    public Task DropDatabaseAsync(CancellationToken ct)
    {
        return _client.DropDatabaseAsync(_bucketId, ct);
    }
}
