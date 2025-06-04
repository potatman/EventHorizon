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

    public MongoCrudStore(IMongoClient client, AttributeUtil attributeUtil, string bucketId)
    {
        _client = client;
        _attributeUtil = attributeUtil;
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

        if (mongoAttr?.TimeToLiveMs != null)
            await AddIndex(CreatedDate1, Builders<T>.IndexKeys.Ascending(x => x.CreatedDate), TimeSpan.FromMilliseconds(mongoAttr.TimeToLiveMs));

        await AddIndex(UpdatedDate1, Builders<T>.IndexKeys.Ascending(x => x.UpdatedDate));
    }

    public async Task<T[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        var objs = await _collection
            .Find(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        return objs.ToArray();
    }

    public async Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        var result = await _collection.Find(x => true)
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
        await _collection.DeleteManyAsync(x => ids.Contains(x.Id), ct);
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
