using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;
using Insperex.EventHorizon.EventStreaming.Test.Integration.Base;
using Insperex.EventHorizon.EventStreaming.Test.Shared;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Pulsar;

public class PulsarSingleTopicConsumerIntegrationTest : BaseSingleTopicConsumerIntegrationTest
{
    public PulsarSingleTopicConsumerIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetPulsarHost(outputHelper).Services)
    {
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();

        var streamFactory = Provider.GetRequiredService<IStreamFactory>();
        var topicAdmin = (PulsarTopicAdmin<Event>)streamFactory.CreateAdmin<Event>();
        await topicAdmin.DeleteTopicAsync(
            $"persistent://test_pricing/Event/subscription__ReSharperTestRunner-Fails_{UniqueTestId}__streamFailureState",
            CancellationToken.None);
    }

    [Fact]
    public async Task TestSingleConsumerWithNativePulsarFailures()
    {
        var handler = new PartialNackListStreamConsumer(_outputHelper, 0.03, 3, 2,
            100, true);

        // Consume
        await using var subscription = await _streamingClient.CreateSubscription<Event>()
            .SubscriptionName($"Fails_{UniqueTestId}")
            .SubscriptionType(SubscriptionType.KeyShared)
            .AddStream<Feed1PriceChanged>()
            .BatchSize(_events.Length / 10)
            .FailedMessageRedeliveryDelay(TimeSpan.FromMilliseconds(5))
            .OnBatch(handler.OnBatch) // Will nack at least some messages.
            .Build()
            .StartAsync();

        // Wait for List
        await WaitUtil.WaitForTrue(() => _events.Length <= handler.List.Count, _timeout);

        handler.Report();

        // ONLY for advanced failure scenario - for basic failures, we must accept some message redelivery.
        //Assert.True(handler.RedeliveredMessages == 0,
        //    $"There were {handler.RedeliveredMessages} redeliveries of previously-accepted messages. Should not have any!");

        // Assert
        // Expecting the advanced failure handling to preserve message ordering despite the nacks.
        AssertUtil.AssertEventsValid(_events, false, handler.List.ToArray());
    }
}
