using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
using EventHorizon.EventStore.ElasticSearch.Attributes;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Schema;
using Microsoft.Extensions.Logging;
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.ElasticSearch;

public class ElasticCrudStore<TE> : ICrudStore<TE>
    where TE : class, ICrudEntity
{
    private readonly ElasticIndexAttribute _elasticAttr;
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticCrudStore<TE>> _logger;
    private readonly string _dbName;
    private readonly Action<CreateIndexRequest> _configureIndex;

    public ElasticCrudStore(ElasticIndexAttribute elasticAttr, ElasticsearchClient client, string bucketId, ILogger<ElasticCrudStore<TE>> logger)
        : this(elasticAttr, client, bucketId, logger, null)
    {
    }

    internal ElasticCrudStore(ElasticIndexAttribute elasticAttr, ElasticsearchClient client, string bucketId, ILogger<ElasticCrudStore<TE>> logger,
        Action<CreateIndexRequest> configureIndex)
    {
        _elasticAttr = elasticAttr;
        _client = client;
        _logger = logger;
        _configureIndex = configureIndex;
        _dbName = bucketId + "_" + typeof(TE).Name.Replace("`1", string.Empty).ToLower(CultureInfo.InvariantCulture);
    }

    public async Task SetupAsync(CancellationToken ct)
    {
        var existsResp = await _client.Indices.ExistsAsync(_dbName, ct);
        if (existsResp.Exists) return;

        var createReq = await _client.Indices.CreateAsync(BuildCreateIndexRequest(), ct);

        ThrowErrors(createReq);
    }

    /// <summary>
    /// Generated mapping and attribute settings, then the configured index hook, which mutates
    /// the populated request rather than replacing it.
    /// </summary>
    internal CreateIndexRequest BuildCreateIndexRequest()
    {
        var settings = new IndexSettings();
        if (_elasticAttr?.Shards > 0) settings.NumberOfShards = _elasticAttr.Shards;
        if (_elasticAttr?.Replicas > 0) settings.NumberOfReplicas = _elasticAttr.Replicas;
        if (_elasticAttr?.RefreshIntervalMs > 0) settings.RefreshInterval = TimeSpan.FromMilliseconds(_elasticAttr.RefreshIntervalMs);
        if (_elasticAttr?.MaxResultWindow > 0) settings.MaxResultWindow = _elasticAttr.MaxResultWindow;

        var request = new CreateIndexRequest(_dbName)
        {
            // Unmapped fields still index dynamically so a renamed property degrades to
            // dynamic mapping instead of losing data.
            Mappings = new TypeMapping
            {
                Dynamic = DynamicMapping.True,
                Properties = BuildMappingProperties()
            },
            Settings = settings
        };

        _configureIndex?.Invoke(request);
        return request;
    }

    private Properties BuildMappingProperties()
    {
        var behavior = _elasticAttr?.Mapping ?? MappingBehavior.Auto;
        if (behavior == MappingBehavior.Dynamic) return null;

        StoreFieldSchema schema;
        try
        {
            schema = StoreSchemaFactory.GetSchema(typeof(TE));
        }
        catch (StoreSchemaLimitException ex) when (behavior == MappingBehavior.Auto)
        {
            _logger.LogWarning(ex, "Index {Index} uses dynamic mapping: {Reason}", _dbName, ex.Message);
            return null;
        }

        // Auto maps only StoreField-annotated fields; everything else keeps dynamic mapping
        if (behavior == MappingBehavior.Auto && !schema.HasExplicitIntents) return null;

        return ElasticMappingBuilder.BuildProperties(schema, mapUnannotated: behavior == MappingBehavior.Static);
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
        , ct);

        ThrowErrors(res);

        return res.Docs.Select(x => x.Match(y => y.Source, z => null)).Where(x => x != null).ToArray();
    }

    public async Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        var res = await _client.SearchAsync<Snapshot<TE>>(x =>
                x.Indices(_dbName)
                    .Size(1)
                    .Source(new SourceConfig(new SourceFilter
                    {
                        Includes = new[] { "updatedDate" }
                    }))
                    .Query(q =>
                        q.Bool(b =>
                            b.Filter(f => f.MatchAll(_ => { }))
                        )
                    )
                    .Sort(s => s.Field(f => f.UpdatedDate, fs => fs.Order(SortOrder.Desc)))
            , ct);

        ThrowErrors(res);

        return res.Documents.FirstOrDefault()?.UpdatedDate ?? DateTime.MinValue;
    }

    public async Task<DbResult> InsertAsync(TE[] objs, CancellationToken ct)
    {
        var res = await _client.BulkAsync(
            b => b.Index(_dbName)
                .CreateMany(objs)
                .Refresh(GetRefresh()), ct);

        return GetBulkResult(res, objs);
    }

    public async Task<DbResult> UpsertAsync(TE[] objs, CancellationToken ct)
    {
        var res = await _client.BulkAsync(
            b => b.Index(_dbName)
                .IndexMany(objs)
                .Refresh(GetRefresh()), ct);

        return GetBulkResult(res, objs);
    }

    private DbResult GetBulkResult(BulkResponse res, TE[] objs)
    {
        // Per-item errors (e.g. duplicate ids on create) are reported via FailedIds.
        if (res.Errors)
        {
            var failedIds = res.ItemsWithErrors.Select(x => x.Id).ToHashSet();
            return new DbResult
            {
                FailedIds = objs.Where(x => failedIds.Contains(x.Id)).Select(x => x.Id).ToArray(),
                PassedIds = objs.Where(x => !failedIds.Contains(x.Id)).Select(x => x.Id).ToArray()
            };
        }

        // A transport/server failure (timeout, 5xx, connection refused) is not a per-item
        // error; reporting it as success would silently lose writes.
        ThrowErrors(res);

        return new DbResult
        {
            PassedIds = objs.Select(x => x.Id).ToArray(),
            FailedIds = Array.Empty<string>()
        };
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct)
    {
        var res = await _client.DeleteByQueryAsync<TE>(_dbName, q => q
            .Query(rq => rq
                .Ids(f => f.Values(ids))
            ).Refresh(GetRefresh() == Refresh.True), ct);

        // TODO: contact elastic and figure out why this doesn't work
        // var objs = ids.Select(x => new { Id = x }).ToArray();
        // var res = await _client.BulkAsync(
        //     b => b.Index(_dbName)
        //         .DeleteMany(objs)
        //         .Index(_dbName)
        //         .Refresh(ElasticIndexAttribute.GetRefresh(GetRefresh())), ct);

        ThrowErrors(res);
    }

    public Task DropDatabaseAsync(CancellationToken ct)
    {
        return _client.Indices.DeleteAsync(_dbName, ct);
    }

    private Refresh GetRefresh() => typeof(TE) == typeof(Lock) ? Refresh.True : _elasticAttr?.Refresh ?? Refresh.False;

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

        throw new TransportException("Unknown Elastic Exception");
    }
}
