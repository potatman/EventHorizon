using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStore.ElasticSearch;

public class ElasticCrudStore<TE> : ICrudStore<TE>
    where TE : class, ICrudEntity
{
    private readonly ElasticIndexAttribute _elasticAttr;
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticCrudStore<TE>> _logger;
    private readonly string _dbName;

    public ElasticCrudStore(ElasticIndexAttribute elasticAttr, ElasticsearchClient client, string bucketId, ILogger<ElasticCrudStore<TE>> logger)
    {
        _elasticAttr = elasticAttr;
        _client = client;
        _logger = logger;
        _dbName = bucketId;
    }

    public async Task SetupAsync(CancellationToken ct)
    {
        var getReq = await _client.Indices.GetAsync(new GetIndexRequest(_dbName), ct);
        if (getReq.IsValidResponse) return;

        var createReq = await _client.Indices.CreateAsync(_dbName, cfg =>
        {
            cfg.Settings(x =>
                {
                    if (_elasticAttr?.Shards > 0) x.NumberOfShards(_elasticAttr?.Shards);
                    if (_elasticAttr?.Replicas > 0) x.NumberOfReplicas(_elasticAttr?.Replicas);
                    if (_elasticAttr?.RefreshIntervalMs > 0) x.RefreshInterval(_elasticAttr?.RefreshIntervalMs);
                    if (_elasticAttr?.MaxResultWindow > 0) x.MaxResultWindow(_elasticAttr?.MaxResultWindow);
                });
        }, ct);

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
            .Refresh(ElasticIndexAttribute.GetRefresh(_elasticAttr?.Refresh).Value == Refresh.True.Value)
        , ct);

        ThrowErrors(res);

        return res.Docs.Select(x => x.Match(y => y.Source, z => null)).Where(x => x != null).ToArray();
    }

    public async Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        var res = await _client.SearchAsync<Snapshot<TE>>(x =>
                x.Index(_dbName)
                    .Size(1)
                    .Source(new SourceConfig(new SourceFilter
                    {
                        Includes = new[] { "updatedDate" }
                    }))
                    .Query(q =>
                        q.Bool(b =>
                            b.Filter(f => f.MatchAll())
                        )
                    )
                    .Sort(s => s.Field(f => f.UpdatedDate).Doc(d => d.Order(SortOrder.Desc)))
            , ct);

        ThrowErrors(res);

        return res.Documents.FirstOrDefault()?.UpdatedDate ?? DateTime.MinValue;
    }

    public async Task<DbResult> InsertAsync(TE[] objs, CancellationToken ct)
    {
        var res = await _client.BulkAsync(
            b => b.Index(_dbName)
                .CreateMany(objs)
                .Refresh(ElasticIndexAttribute.GetRefresh(_elasticAttr?.Refresh)), ct);

        var result = new DbResult { PassedIds = objs.Select(x => x.Id).ToArray() };
        if (res.Errors)
        {
            var failedIds = res.ItemsWithErrors.Select(x => x.Id).ToArray();
            result.FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
            result.PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
        }

        return result;
    }

    public async Task<DbResult> UpsertAsync(TE[] objs, CancellationToken ct)
    {
        var res = await _client.BulkAsync(
            b => b.Index(_dbName)
                .IndexMany(objs)
                .Refresh(ElasticIndexAttribute.GetRefresh(_elasticAttr?.Refresh)), ct);

        var result = new DbResult { PassedIds = objs.Select(x => x.Id).ToArray(), FailedIds = Array.Empty<string>() };
        if (res.Errors)
        {
            var failedIds = res.ItemsWithErrors.Select(x => x.Id).ToArray();
            result.FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
            result.PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).ToArray();
        }

        return result;
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct)
    {
        var res = await _client.DeleteByQueryAsync<TE>(_dbName, q => q
            .Query(rq => rq
                .Ids(f => f.Values(ids))
            ).Refresh(ElasticIndexAttribute.GetRefresh(_elasticAttr?.Refresh).Value == "true"), ct);

        // TODO: contact elastic and figure out why this doesn't work
        // var objs = ids.Select(x => new { Id = x }).ToArray();
        // var res = await _client.BulkAsync(
        //     b => b.Index(_dbName)
        //         .DeleteMany(objs)
        //         .Index(_dbName)
        //         .Refresh(ElasticIndexAttribute.GetRefresh(_elasticAttr?.Refresh)), ct);

        ThrowErrors(res);
    }

    public Task DropDatabaseAsync(CancellationToken ct)
    {
        return _client.Indices.DeleteAsync(_dbName, ct);
    }

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
