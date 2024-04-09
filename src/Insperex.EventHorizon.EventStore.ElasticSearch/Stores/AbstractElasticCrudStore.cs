using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStore.ElasticSearch.Stores;

public abstract class AbstractElasticCrudStore<TE> : ICrudStore<TE>
    where TE : class, ICrudEntity
{
    private readonly ElasticIndexAttribute _elasticAttr;
    private readonly ElasticsearchClient _client;
    private readonly ILogger<AbstractElasticCrudStore<TE>> _logger;
    private readonly string _dbName;

    protected AbstractElasticCrudStore(ElasticIndexAttribute elasticAttr, ElasticsearchClient client, string database, ILogger<AbstractElasticCrudStore<TE>> logger)
    {
        _elasticAttr = elasticAttr;
        _client = client;
        _logger = logger;
        _dbName = database;
    }

    public async Task MigrateAsync(CancellationToken ct)
    {
        var getReq = await _client.Indices.GetAsync(new GetIndexRequest(_dbName), ct).ConfigureAwait(false);
        if (getReq.IsValidResponse) return;

        var createReq = await _client.Indices.CreateAsync(_dbName, cfg =>
        {
            cfg.Mappings(x => x.Dynamic(DynamicMapping.True));
            cfg.Settings(x =>
                {
                    if (_elasticAttr?.Shards > 0) x.NumberOfShards(_elasticAttr?.Shards);
                    if (_elasticAttr?.Replicas > 0) x.NumberOfReplicas(_elasticAttr?.Replicas);
                    if (_elasticAttr?.RefreshIntervalMs > 0) x.RefreshInterval(_elasticAttr?.RefreshIntervalMs);
                    if (_elasticAttr?.MaxResultWindow > 0) x.MaxResultWindow(_elasticAttr?.MaxResultWindow);
                });
        }, ct).ConfigureAwait(false);

        ThrowErrors(createReq);
    }

    public async Task<TE[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        if (ids?.Any() != true)
            return Array.Empty<TE>();

        ids = ids.Distinct().ToArray();

        var res = await _client.MultiGetAsync<TE>(m => m
            .Index(_dbName)
            .Ids(ids)
            .Refresh(true)
        , ct).ConfigureAwait(false);

        ThrowErrors(res);

        return res.Docs.Select(x => x.Match(y => y.Source, z => null)).Where(x => x != null).ToArray();
    }

    public async Task<DbResult> InsertAllAsync(TE[] objs, CancellationToken ct)
    {
        var res = await _client.BulkAsync(
            b => b.Index(_dbName)
                .CreateMany(objs)
                .Refresh(ElasticIndexAttribute.GetRefresh(GetRefresh())), ct).ConfigureAwait(false);

        var result = new DbResult { PassedIds = objs.Select(x => x.Id).ToArray() };
        if (res.Errors)
        {
            var failedIds = res.ItemsWithErrors.Select(x => x.Id).ToArray();
            result.FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
            result.PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
        }

        return result;
    }

    public async Task<DbResult> UpsertAllAsync(TE[] objs, CancellationToken ct)
    {
        var res = await _client.BulkAsync(
            b => b.Index(_dbName)
                .IndexMany(objs)
                .Refresh(ElasticIndexAttribute.GetRefresh(GetRefresh())), ct).ConfigureAwait(false);

        var result = new DbResult { PassedIds = objs.Select(x => x.Id).ToArray(), FailedIds = Array.Empty<string>() };
        if (res.Errors)
        {
            var failedIds = res.ItemsWithErrors.Select(x => x.Id).ToArray();
            result.FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
            result.PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
        }

        return result;
    }

    public async Task DeleteAllAsync(string[] ids, CancellationToken ct)
    {
        var res = await _client.DeleteByQueryAsync<TE>(_dbName, q => q
            .Query(rq => rq
                .Ids(f => f.Values(ids))
            ).Refresh(ElasticIndexAttribute.GetRefresh(GetRefresh()).Value == "true"), ct).ConfigureAwait(false);

        // TODO: contact elastic and figure out why this doesn't work
        // var objs = ids.Select(x => new { Id = x }).ToArray();
        // var res = await _client.BulkAsync(
        //     b => b.Index(_dbName)
        //         .DeleteMany(objs)
        //         .Index(_dbName)
        //         .Refresh(ElasticIndexAttribute.GetRefresh(GetRefresh())), ct).ConfigureAwait(false);

        ThrowErrors(res);
    }

    public Task DropDatabaseAsync(CancellationToken ct)
    {
        return _client.Indices.DeleteAsync(_dbName, ct);
    }

    private string GetRefresh() => typeof(TE) == typeof(Lock)? Refresh.True.Value : _elasticAttr?.Refresh;

    private void ThrowErrors(BulkResponse res)
    {
        if (res.IsValidResponse) return;

        var failedHits = res.ItemsWithErrors.ToArray();
        if (failedHits.Any())
        {
            var first = failedHits.First();
            if (first.Error.Type == "index_not_found_exception")
                return;
            else
                throw new TransportException(first.Error.Type);
        }

        ThrowErrors(res as ElasticsearchResponse);
    }

    private void ThrowErrors(ElasticsearchResponse res)
    {
        if (res.IsValidResponse) return;

        // Low Level Errors
        if (res.TryGetOriginalException(out var originalException))
        {
            var max = Math.Min(2000, res.DebugInformation.Length);
            _logger.LogError(originalException, res.DebugInformation[..max]);
            throw originalException;
        }

        // Low Level Errors
        if (res.TryGetElasticsearchServerError(out var elasticsearchServerError) && elasticsearchServerError.Error != null
            && elasticsearchServerError.Error.Type != "index_already_exists_exception")
        {
            var ex = new TransportException(elasticsearchServerError.ToString());
            _logger.LogError(ex, elasticsearchServerError.ToString());
            throw ex;
        }

        throw new Exception("Unknown Elastic Exception");
    }
}
