using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Nest;

namespace Insperex.EventHorizon.EventStore.ElasticSearch;

public class ElasticCrudStore<T> : ICrudStore<T>
    where T : class, ICrudEntity
{
    private readonly IElasticClient _client;
    private readonly string _dbName;

    public ElasticCrudStore(IElasticClient client, string bucketId)
    {
        _client = client;
        _dbName = $"{bucketId}_{typeof(T).Name.ToLower().Replace("`1", string.Empty)}";
        Setup().Wait();
    }

    public async Task<T[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        if (ids?.Any() != true)
            return Array.Empty<T>();

        ids = ids.Distinct().ToArray();

        var res = await _client.MultiGetAsync(m => m
            .Index(_dbName)
            .GetMany<T>(ids, (g, id) => g.Index(_dbName)), ct);

        return res.Hits.Select(x => x.Source as T).Where(x => x != null).ToArray();
    }

    public async Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        var res = await _client.SearchAsync<Snapshot<T>>(x =>
            x.Index(_dbName)
                .Size(1)
                .Source(s =>
                    s.Includes(i =>
                        i.Fields(f => f.UpdatedDate)
                    )
                )
                .Query(q =>
                    q.Bool(b =>
                        b.Filter(f => f.MatchAll())
                    )
                )
                .Sort(s => s.Descending(f => f.UpdatedDate)), ct);

        return res.Documents.FirstOrDefault()?.UpdatedDate ?? DateTime.MinValue;
    }

    public async Task<DbResult> InsertAsync(T[] objs, CancellationToken ct)
    {
        var res = await _client.BulkAsync(
            b => b.Index(_dbName)
                .CreateMany(objs), ct);

        var result = new DbResult { PassedIds = objs.Select(x => x.Id).ToArray() };
        if (res.Errors)
        {
            var failedIds = res.ItemsWithErrors.Select(x => x.Id).ToArray();
            result.FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
            result.PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
        }

        return result;
    }

    public async Task<DbResult> UpsertAsync(T[] objs, CancellationToken ct)
    {
        var res = await _client.BulkAsync(
            b => b.Index(_dbName)
                .IndexMany(objs), ct);

        var result = new DbResult { PassedIds = objs.Select(x => x.Id).ToArray(), FailedIds = Array.Empty<string>() };
        if (res.Errors)
        {
            var failedIds = res.ItemsWithErrors.Select(x => x.Id).ToArray();
            result.FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
            result.PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
        }

        return result;
    }

    public Task DeleteAsync(string[] ids, CancellationToken ct)
    {
        var objs = ids.Select(x => new { Id = x }).ToArray();
        return _client.DeleteManyAsync(objs, _dbName, ct);
    }

    public Task DropDatabaseAsync(CancellationToken ct)
    {
        return _client.Indices.DeleteAsync(_dbName, ct: ct);
    }

    public async Task Setup()
    {
        var res = await _client.Indices.CreateAsync(_dbName, cfg =>
            cfg.Map<T>(map => map.AutoMap()));
    }
}