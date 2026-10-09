using System.Linq;
using System.Threading.Tasks;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Pulsar.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public class PulsarClientResolverUnitTest
{
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
}
