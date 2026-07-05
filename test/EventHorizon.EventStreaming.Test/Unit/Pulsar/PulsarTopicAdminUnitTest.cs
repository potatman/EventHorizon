using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Pulsar.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public sealed class PulsarTopicAdminUnitTest : IDisposable
{
    private const string Topic = "persistent://test_tenant/test_namespace/test_topic";

    private readonly TcpListener _listener;
    private readonly PulsarTopicAdmin<Event> _admin;
    private int _statusCode = 200;
    private string _responseBody = "{}";

    public PulsarTopicAdminUnitTest()
    {
        // Minimal single-purpose HTTP endpoint standing in for the Pulsar admin API.
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _ = Task.Run(ServeAsync);

        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        var resolver = new PulsarClientResolver(Options.Create(new PulsarConfig
        {
            ServiceUrl = "pulsar://127.0.0.1:1",
            AdminUrl = $"http://127.0.0.1:{port}"
        }));
        _admin = new PulsarTopicAdmin<Event>(resolver, new AttributeUtil(),
            NullLogger<PulsarTopicAdmin<Event>>.Instance);
    }

    private async Task ServeAsync()
    {
        while (true)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);

            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) { }

            var content = Encoding.UTF8.GetBytes(_responseBody);
            var header = $"HTTP/1.1 {_statusCode} StatusCode\r\n"
                         + "Content-Type: application/json\r\n"
                         + $"Content-Length: {content.Length}\r\n"
                         + "Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(header));
            await stream.WriteAsync(content);
        }
    }

    public void Dispose() => _listener.Stop();

    [Fact]
    public async Task SubscriptionExistsWhenPresentInTopicStats()
    {
        _responseBody = """{"subscriptions":{"my-subscription":{"consumers":[]}}}""";

        Assert.True(await _admin.SubscriptionExistsAsync(Topic, "my-subscription", CancellationToken.None));
    }

    [Fact]
    public async Task SubscriptionDoesNotExistWhenAbsentFromTopicStats()
    {
        _responseBody = """{"subscriptions":{"other-subscription":{"consumers":[]}}}""";

        Assert.False(await _admin.SubscriptionExistsAsync(Topic, "my-subscription", CancellationToken.None));
    }

    [Fact]
    public async Task SubscriptionDoesNotExistWhenTopicMissing()
    {
        _statusCode = 404;
        _responseBody = """{"reason":"Topic not found"}""";

        Assert.False(await _admin.SubscriptionExistsAsync(Topic, "my-subscription", CancellationToken.None));
    }

    [Fact]
    public async Task SubscriptionExistenceCheckThrowsOnServerError()
    {
        // Any failure other than topic-not-found must surface, never silently
        // report "new subscription" (which would trigger a cursor seek).
        _statusCode = 500;
        _responseBody = """{"reason":"boom"}""";

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _admin.SubscriptionExistsAsync(Topic, "my-subscription", CancellationToken.None));
    }

    [Fact]
    public async Task SubscriptionExistsOnAnyTopicChecksAllTopics()
    {
        _responseBody = """{"subscriptions":{}}""";

        var topics = new[] { Topic, Topic + "_2" };
        Assert.False(await _admin.SubscriptionExistsAsync(topics, "my-subscription", CancellationToken.None));

        _responseBody = """{"subscriptions":{"my-subscription":{"consumers":[]}}}""";
        Assert.True(await _admin.SubscriptionExistsAsync(topics, "my-subscription", CancellationToken.None));
    }
}
