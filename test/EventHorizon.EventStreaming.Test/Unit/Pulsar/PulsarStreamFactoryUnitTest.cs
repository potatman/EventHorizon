using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Pulsar.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pulsar.Client.Api;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public class PulsarStreamFactoryUnitTest
{
    private static PulsarStreamFactory GetFactory(ILoggerFactory loggerFactory) =>
        new(new PulsarClientResolver(Options.Create(new PulsarConfig
        {
            ServiceUrl = "pulsar://127.0.0.1:1",
            AdminUrl = "http://127.0.0.1:1"
        })), new AttributeUtil(), loggerFactory);

    [Fact]
    public void WiresPulsarClientLoggerWhenUnset()
    {
        PulsarClient.Logger = NullLogger.Instance;
        using var loggerFactory = LoggerFactory.Create(_ => { });

        GetFactory(loggerFactory);

        Assert.IsNotType<NullLogger>(PulsarClient.Logger);
    }

    [Fact]
    public void DoesNotOverrideExplicitPulsarClientLogger()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var appLogger = loggerFactory.CreateLogger("app-configured");
        PulsarClient.Logger = appLogger;

        GetFactory(NullLoggerFactory.Instance);

        Assert.Same(appLogger, PulsarClient.Logger);
    }
}
