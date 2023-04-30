using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Test.Fakers;
using Insperex.EventHorizon.EventStreaming.Test.Models;
using Insperex.EventHorizon.EventStreaming.Test.Shared;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Base;

[Trait("Category", "Integration")]
public abstract class BaseMultiTopicConsumerIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TimeSpan _timeout;
    private Stopwatch _stopwatch;
    private Event[] _events;
    private readonly StreamingClient _streamingClient;
    private readonly ListTopicHandler<Event> _handler;

    protected BaseMultiTopicConsumerIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        _outputHelper = outputHelper;
        _streamingClient = provider.GetRequiredService<StreamingClient>();
        _timeout = TimeSpan.FromSeconds(15);
        _handler = new ListTopicHandler<Event>();
    }

    public async Task InitializeAsync()
    {
        _events = EventStreamingFakers.EventFaker.Generate(1000).ToArray();
        // Setup
        using var publisher1 = _streamingClient.CreatePublisher<Event>().AddTopic<ExampleEvent1>().Build();
        using var publisher2 = _streamingClient.CreatePublisher<Event>().AddTopic<ExampleEvent2>().Build();

        await publisher1.PublishAsync(_events.Take(_events.Length/2).ToArray());
        await publisher2.PublishAsync(_events.Skip(_events.Length/2).ToArray());
        await Task.Delay(4000);

        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(ExampleEvent1));
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(ExampleEvent2));
    }

    [Fact]
    public async Task SubscribeToMultipleTopics()
    {
        // Consume
        using var subscription = await _streamingClient.CreateSubscription<Event>()
            .AddActionTopic<ExampleEvent1>()
            .AddActionTopic<ExampleEvent2>()
            .BatchSize(_events.Length/10)
            .OnBatch(_handler.OnBatch)
            .Build()
            .StartAsync();

        // Assert
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }

    [Fact]
    public async Task SubscribeToTopicsSeparately()
    {
        // Consume
        var builder = _streamingClient.CreateSubscription<Event>()
            .BatchSize(_events.Length / 10);

        using var subscription1 = await builder.AddActionTopic<ExampleEvent1>().OnBatch(_handler.OnBatch).Build().StartAsync();
        using var subscription2 = await builder.AddActionTopic<ExampleEvent2>().OnBatch(_handler.OnBatch).Build().StartAsync();

        // Assert
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }
}
