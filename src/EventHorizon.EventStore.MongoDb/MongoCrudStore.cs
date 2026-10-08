using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.MongoDb.Attributes;
using EventHorizon.EventStore.MongoDb.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EventHorizon.EventStore.MongoDb;

public class MongoCrudStore<T> : ICrudStore<T>
    where T : ICrudEntity
{
    private const string Id = "_id";
    private const string Name = "name";
    private const string Document = "Document";
    private const string UpdatedDate1 = "UpdatedDate_1";
    private const string CreatedDate1 = "CreatedDate_1";
    private const string Tilda1 = "`1";
    private const string ErrorPrefix = "_id: \"";
    private const string ErrorPostfix = "\" }";
    private readonly string _bucketId;
    private readonly IMongoClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly IMongoCollection<T> _collection;
    private readonly MongoCollectionAttribute _collectionAttribute;

    internal IMongoCollection<T> Collection => _collection;

    public MongoCrudStore(IMongoClient client, AttributeUtil attributeUtil, string bucketId)
    {
        _client = client;
        _attributeUtil = attributeUtil;
        _bucketId = bucketId;
        var type = typeof(T);
        var database = client.GetDatabase(bucketId);
        var typeName = type.Name.Replace(Tilda1, string.Empty);
        _collectionAttribute = GetCollectionAttribute();
        _collection = ApplyCollectionSettings(database.GetCollection<T>(typeName), _collectionAttribute);
    }

    // The attribute sits on the state class, so for Snapshot<TState>/View<TState> look at TState.
    private MongoCollectionAttribute GetCollectionAttribute()
    {
        var type = typeof(T);
        var stateType = type.IsGenericType ? type.GetGenericArguments()[0] : type;
        return _attributeUtil.GetOne<MongoCollectionAttribute>(stateType);
    }

    // Applied per instance in the constructor: SetupAsync runs once per process on a single instance.
    // The With* methods return a new collection object rather than modifying the receiver.
    private static IMongoCollection<T> ApplyCollectionSettings(IMongoCollection<T> collection, MongoCollectionAttribute attr)
    {
        if (attr is null)
            return collection;

        if (attr.HasReadConcernLevel)
            collection = collection.WithReadConcern(new ReadConcern(attr.ReadConcernLevel));
        if (attr.HasReadPreferenceMode)
            collection = collection.WithReadPreference(new ReadPreference(attr.ReadPreferenceMode));
        if (attr.HasWriteConcernLevel)
            collection = collection.WithWriteConcern(attr.WriteConcernLevel switch
            {
                WriteConcernLevel.Acknowledged => WriteConcern.Acknowledged,
                WriteConcernLevel.Unacknowledged => WriteConcern.Unacknowledged,
                WriteConcernLevel.W1 => WriteConcern.W1,
                WriteConcernLevel.W2 => WriteConcern.W2,
                WriteConcernLevel.W3 => WriteConcern.W3,
                WriteConcernLevel.Majority => WriteConcern.WMajority,
                _ => throw new ArgumentOutOfRangeException(nameof(attr), attr.WriteConcernLevel, "Unknown write concern level")
            });

        return collection;
    }

    public async Task SetupAsync(CancellationToken ct)
    {
        if (_collectionAttribute?.TimeToLiveMs > 0)
            await AddIndex(CreatedDate1, Builders<T>.IndexKeys.Ascending(x => x.CreatedDate), TimeSpan.FromMilliseconds(_collectionAttribute.TimeToLiveMs));

        await AddIndex(UpdatedDate1, Builders<T>.IndexKeys.Ascending(x => x.UpdatedDate));
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

    private async Task AddIndex(string name, IndexKeysDefinition<T> definition, TimeSpan? timeSpan = null)
    {
        var opts = new CreateIndexOptions { Background = true, ExpireAfter = timeSpan };
        var names = (await _collection.Indexes.ListAsync()).ToList().Select(x => x[Name.ToLower(CultureInfo.InvariantCulture)]).ToArray();
        if (!names.Contains(name))
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(definition,opts));
    }
}
