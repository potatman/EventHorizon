using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using EventHorizon.EventStore.ElasticSearch;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Models;
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
}
