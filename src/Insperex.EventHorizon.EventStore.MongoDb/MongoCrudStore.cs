using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Insperex.EventHorizon.EventStore.MongoDb;

public class MongoCrudStore<T> : ICrudStore<T>
    where T : ICrudEntity
{
    private const string Id = "_id";
    private const string Name = "name";
    private const string Document = "Document";
    private const string UpdatedDate1 = "UpdatedDate_1";
    private const string Tilda1 = "`1";
    private const string ErrorPrefix = "_id: \"";
    private const string ErrorPostfix = "\" }";
    private readonly string _bucketId;
    private readonly IMongoClient _client;
    private readonly IMongoCollection<T> _collection;

    public MongoCrudStore(IMongoClient client, string bucketId)
    {
        _client = client;
        _bucketId = bucketId;
        var database = client.GetDatabase(bucketId);
        var typeName = typeof(T).Name.Replace(Tilda1, string.Empty);
        _collection = database.GetCollection<T>(typeName);
        SetupAsync().Wait();
    }

    public async Task<T[]> GetAsync(string[] ids, CancellationToken ct)
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

    private async Task SetupAsync()
    {
        await AddIndex(UpdatedDate1, Builders<T>.IndexKeys.Ascending(x => x.UpdatedDate));
    }

    private async Task AddIndex(string name, IndexKeysDefinition<T> definition)
    {
        var names = (await _collection.Indexes.ListAsync()).ToList().Select(x => x[Name.ToLower()]).ToArray();
        if (!names.Contains(name))
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(definition));
    }
}