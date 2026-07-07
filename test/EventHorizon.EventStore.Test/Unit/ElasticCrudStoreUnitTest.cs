using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Transport;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.EventStore.ElasticSearch;
using EventHorizon.EventStore.ElasticSearch.Attributes;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class ElasticCrudStoreUnitTest
{
    private class TestEntity : ICrudEntity
    {
        public string Id { get; set; }
        public DateTime UpdatedDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private static ElasticCrudStore<TestEntity> GetStore(string responseJson, int statusCode,
        Action<ApiCallDetails> onRequestCompleted = null)
    {
        // The v9 client validates the X-Elastic-Product header on every response.
        var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-elastic-product"] = new[] { "Elasticsearch" }
        };
        var invoker = new InMemoryRequestInvoker(
            responseJson == null ? null : Encoding.UTF8.GetBytes(responseJson), statusCode,
            exception: null, contentType: "application/json", headers: headers);

        var settings = new ElasticsearchClientSettings(invoker)
            .DisableDirectStreaming();

        if (onRequestCompleted != null)
            settings = settings.OnRequestCompleted(onRequestCompleted);

        var client = new ElasticsearchClient(settings);
        return new ElasticCrudStore<TestEntity>(null, client, "test_bucket",
            NullLogger<ElasticCrudStore<TestEntity>>.Instance);
    }

    private static TestEntity[] GetEntities(params string[] ids) =>
        ids.Select(id => new TestEntity
        {
            Id = id,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        }).ToArray();

    private const string BulkSuccessResponse =
        """
        {"took":1,"errors":false,"items":[
            {"index":{"_index":"test","_id":"1","status":200}},
            {"index":{"_index":"test","_id":"2","status":200}}]}
        """;

    private const string BulkPartialFailureResponse =
        """
        {"took":1,"errors":true,"items":[
            {"create":{"_index":"test","_id":"1","status":201}},
            {"create":{"_index":"test","_id":"2","status":409,
                "error":{"type":"version_conflict_engine_exception","reason":"[2]: version conflict"}}}]}
        """;

    [Fact]
    public async Task UpsertReportsAllPassedOnSuccess()
    {
        var store = GetStore(BulkSuccessResponse, 200);

        var result = await store.UpsertAsync(GetEntities("1", "2"), CancellationToken.None);

        Assert.Equal(new[] { "1", "2" }, result.PassedIds);
        Assert.NotNull(result.FailedIds);
        Assert.Empty(result.FailedIds);
    }

    [Fact]
    public async Task InsertReportsAllPassedOnSuccess()
    {
        var store = GetStore(BulkSuccessResponse, 200);

        var result = await store.InsertAsync(GetEntities("1", "2"), CancellationToken.None);

        Assert.Equal(new[] { "1", "2" }, result.PassedIds);
        Assert.NotNull(result.FailedIds);
        Assert.Empty(result.FailedIds);
    }

    [Fact]
    public async Task UpsertPartitionsPerItemFailures()
    {
        var store = GetStore(BulkPartialFailureResponse, 200);

        var result = await store.UpsertAsync(GetEntities("1", "2"), CancellationToken.None);

        Assert.Equal(new[] { "1" }, result.PassedIds);
        Assert.Equal(new[] { "2" }, result.FailedIds);
    }

    [Fact]
    public async Task InsertPartitionsPerItemFailures()
    {
        var store = GetStore(BulkPartialFailureResponse, 200);

        var result = await store.InsertAsync(GetEntities("1", "2"), CancellationToken.None);

        Assert.Equal(new[] { "1" }, result.PassedIds);
        Assert.Equal(new[] { "2" }, result.FailedIds);
    }

    [Fact]
    public async Task UpsertThrowsWhenBulkCallFails()
    {
        // A 500 (or unreachable cluster) has no per-item errors; it must throw,
        // never report PassedIds.
        var store = GetStore(null, 500);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.UpsertAsync(GetEntities("1", "2"), CancellationToken.None));
    }

    [Fact]
    public async Task InsertThrowsWhenBulkCallFails()
    {
        var store = GetStore(null, 500);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.InsertAsync(GetEntities("1", "2"), CancellationToken.None));
    }

    private const string EmptySearchResponse =
        """
        {"took":1,"timed_out":false,
         "_shards":{"total":1,"successful":1,"skipped":0,"failed":0},
         "hits":{"total":{"value":0,"relation":"eq"},"max_score":null,"hits":[]}}
        """;

    [Fact]
    public async Task GetLastUpdatedDateSortsByUpdatedDateDescending()
    {
        string requestBody = null;
        var store = GetStore(EmptySearchResponse, 200, details =>
        {
            if (details.RequestBodyInBytes != null)
                requestBody = Encoding.UTF8.GetString(details.RequestBodyInBytes);
        });

        var result = await store.GetLastUpdatedDateAsync(CancellationToken.None);

        Assert.Equal(DateTime.MinValue, result);
        Assert.NotNull(requestBody);

        using var doc = JsonDocument.Parse(requestBody);
        var sort = doc.RootElement.GetProperty("sort");
        var fieldSort = sort.ValueKind == JsonValueKind.Array ? sort[0] : sort;
        var order = fieldSort.GetProperty("updatedDate").GetProperty("order").GetString();
        Assert.Equal("desc", order);
    }

    #region SetupAsync mapping

    private class SetupMappedState
    {
        public string Id { get; set; }

        [StoreField(FieldIntent.FullText)]
        public string Description { get; set; }

        [StoreField(Store = false)]
        public string Hidden { get; set; }
    }

    private class SetupPlainState
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Routes the index-exists HEAD probe to a 404 so SetupAsync proceeds to create the index,
    /// while every other call gets the canned success response.
    /// </summary>
    private sealed class RoutingRequestInvoker : IRequestInvoker
    {
        private readonly InMemoryRequestInvoker _headInvoker;
        private readonly InMemoryRequestInvoker _bodyInvoker;

        public RoutingRequestInvoker(string responseJson)
        {
            var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-elastic-product"] = new[] { "Elasticsearch" }
            };
            _headInvoker = new InMemoryRequestInvoker(null, 404, null, "application/json", headers);
            _bodyInvoker = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(responseJson), 200, null, "application/json", headers);
        }

        public ResponseFactory ResponseFactory => _bodyInvoker.ResponseFactory;

        public TResponse Request<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData postData)
            where TResponse : TransportResponse, new() =>
            Pick(endpoint).Request<TResponse>(endpoint, boundConfiguration, postData);

        public Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData postData, CancellationToken cancellationToken)
            where TResponse : TransportResponse, new() =>
            Pick(endpoint).RequestAsync<TResponse>(endpoint, boundConfiguration, postData, cancellationToken);

        public void Dispose()
        {
        }

        private IRequestInvoker Pick(Endpoint endpoint) =>
            endpoint.Method == HttpMethod.HEAD ? _headInvoker : _bodyInvoker;
    }

    private const string CreateIndexResponseJson =
        """{"acknowledged":true,"shards_acknowledged":true,"index":"test"}""";

    private static async Task<JsonDocument> RunSetupAsync<T>(
        ElasticIndexAttribute attr = null,
        Action<CreateIndexRequestDescriptor> configureIndex = null)
        where T : class, ICrudEntity
    {
        string requestBody = null;
        var settings = new ElasticsearchClientSettings(
                new SingleNodePool(new Uri("http://localhost:9200")),
                new RoutingRequestInvoker(CreateIndexResponseJson))
            .DisableDirectStreaming()
            .OnRequestCompleted(details =>
            {
                if (details.RequestBodyInBytes != null)
                    requestBody = Encoding.UTF8.GetString(details.RequestBodyInBytes);
            });
        var client = new ElasticsearchClient(settings);
        var schema = new StoreSchemaFactory().GetSchema(typeof(T));
        var store = new ElasticCrudStore<T>(attr, client, "test_bucket",
            NullLogger<ElasticCrudStore<T>>.Instance, schema, configureIndex);

        await store.SetupAsync(CancellationToken.None);

        Assert.NotNull(requestBody);
        return JsonDocument.Parse(requestBody);
    }

    [Fact]
    public async Task SetupCreatesStaticMappingWhenIntentsArePresent()
    {
        using var doc = await RunSetupAsync<Snapshot<SetupMappedState>>();
        var mappings = doc.RootElement.GetProperty("mappings");

        Assert.True(mappings.GetProperty("dynamic").GetBoolean());

        var props = mappings.GetProperty("properties");
        Assert.Equal("keyword", props.GetProperty("id").GetProperty("type").GetString());
        Assert.Equal("long", props.GetProperty("sequenceId").GetProperty("type").GetString());
        Assert.Equal("date", props.GetProperty("updatedDate").GetProperty("type").GetString());

        var state = props.GetProperty("state");
        Assert.Equal("text", state.GetProperty("properties").GetProperty("description").GetProperty("type").GetString());

        var excludes = mappings.GetProperty("_source").GetProperty("excludes");
        Assert.Equal("state.hidden", excludes[0].GetString());
    }

    [Fact]
    public async Task SetupKeepsLegacyDynamicMappingWithoutIntents()
    {
        using var doc = await RunSetupAsync<Snapshot<SetupPlainState>>();
        var mappings = doc.RootElement.GetProperty("mappings");

        Assert.True(mappings.GetProperty("dynamic").GetBoolean());
        Assert.False(mappings.TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task SetupHonorsDynamicMappingOverride()
    {
        var attr = new ElasticIndexAttribute { Mapping = MappingBehavior.Dynamic };
        using var doc = await RunSetupAsync<Snapshot<SetupMappedState>>(attr);

        Assert.False(doc.RootElement.GetProperty("mappings").TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task SetupHonorsStaticMappingOverride()
    {
        var attr = new ElasticIndexAttribute { Mapping = MappingBehavior.Static };
        using var doc = await RunSetupAsync<Snapshot<SetupPlainState>>(attr);

        var props = doc.RootElement.GetProperty("mappings").GetProperty("properties");
        Assert.Equal("keyword", props.GetProperty("id").GetProperty("type").GetString());
    }

    [Fact]
    public async Task SetupAppliesConfigureIndexEscapeHatch()
    {
        using var doc = await RunSetupAsync<Snapshot<SetupMappedState>>(
            configureIndex: cfg => cfg.Settings(s => s.NumberOfShards(4)));

        var settings = doc.RootElement.GetProperty("settings");
        Assert.Equal(4, settings.GetProperty("number_of_shards").GetInt32());
    }

    #endregion
}
