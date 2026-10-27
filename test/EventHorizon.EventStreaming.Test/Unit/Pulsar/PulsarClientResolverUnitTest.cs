using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Pulsar.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public class PulsarClientResolverUnitTest
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

    private static PulsarClientResolver GetResolver() =>
        new(Options.Create(new PulsarConfig
        {
            ServiceUrl = "pulsar://localhost:6650",
            AdminUrl = "http://localhost:8080"
        }));

    [Fact]
    public async Task ReturnsSameClientInstanceOnRepeatedCalls()
    {
        using var resolver = GetResolver();

        var first = await resolver.GetPulsarClientAsync();
        var second = await resolver.GetPulsarClientAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ReturnsSameClientInstanceUnderConcurrency()
    {
        using var resolver = GetResolver();

        var clients = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(resolver.GetPulsarClientAsync)));

        Assert.All(clients, c => Assert.Same(clients[0], c));
    }

    [Fact]
    public async Task ConcurrentCallersForOneTopicShareOneProducer()
    {
        using var resolver = GetResolver();
        var created = 0;

        async Task<object> Create()
        {
            Interlocked.Increment(ref created);
            await Task.Delay(20);
            return new object();
        }

        var producers = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => resolver.GetOrAddProducerAsync(typeof(string), "topic-a", SendTimeout, Create)));

        Assert.Equal(1, created);
        Assert.All(producers, producer => Assert.Same(producers[0], producer));
    }

    [Fact]
    public async Task EachTopicAndMessageTypeGetsItsOwnProducer()
    {
        using var resolver = GetResolver();
        Task<object> Create() => Task.FromResult(new object());

        var a = await resolver.GetOrAddProducerAsync(typeof(string), "topic-a", SendTimeout, Create);
        var b = await resolver.GetOrAddProducerAsync(typeof(string), "topic-b", SendTimeout, Create);
        var c = await resolver.GetOrAddProducerAsync(typeof(int), "topic-a", SendTimeout, Create);

        Assert.NotSame(a, b);
        Assert.NotSame(a, c);
        Assert.Same(a, await resolver.GetOrAddProducerAsync(typeof(string), "topic-a", SendTimeout, Create));
    }

    [Fact]
    public async Task FailedProducerCreationIsRetriedByTheNextCaller()
    {
        using var resolver = GetResolver();
        var attempts = 0;

        Task<object> Create() => ++attempts == 1
            ? Task.FromException<object>(new InvalidOperationException("broker unavailable"))
            : Task.FromResult(new object());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.GetOrAddProducerAsync(typeof(string), "topic-a", SendTimeout, Create));
        var producer = await resolver.GetOrAddProducerAsync(typeof(string), "topic-a", SendTimeout, Create);

        Assert.NotNull(producer);
        Assert.Equal(2, attempts);
    }
}
