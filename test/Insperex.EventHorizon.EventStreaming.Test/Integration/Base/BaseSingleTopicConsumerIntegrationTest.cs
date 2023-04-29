using System;
using System.Diagnostics;
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

public abstract class BaseSingleTopicConsumerIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    private readonly TimeSpan _timeout;
    private Event[] _events;
    private readonly ListTopicHandler<Event> _handler;

    protected BaseSingleTopicConsumerIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        _outputHelper = outputHelper;
        _timeout = TimeSpan.FromSeconds(30);
        _streamingClient = provider.GetRequiredService<StreamingClient>();
        _handler = new ListTopicHandler<Event>();
    }

    public Task InitializeAsync()
    {
        // Publish
        _events = EventStreamingFakers.EventFaker.Generate(100).ToArray();
        _stopwatch = Stopwatch.StartNew();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(ExampleEvent1));
    }

    [Fact]
    public async Task TestSingleConsumer()
    {
        Console.WriteLine("TestSingleConsumer");
        // Consume
        using var subscription = await _streamingClient.CreateSubscription<Event>()
            .AddActionTopic<ExampleEvent1>()
            .BatchSize(_events.Length / 10)
            .OnBatch(_handler.OnBatch)
            .Build()
            .StartAsync();

        using var publisher = await _streamingClient.CreatePublisher<Event>()
            .AddTopic<ExampleEvent1>()
            .Build()
            .PublishAsync(_events);

        // Wait for List
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);

        // Assert
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }

    [Fact]
    public async Task TestKeySharedConsumers()
    {
        Console.WriteLine("TestKeySharedConsumers");
        var builder = _streamingClient.CreateSubscription<Event>()
            .AddActionTopic<ExampleEvent1>()
            .BatchSize(_events.Length / 10)
            .OnBatch(_handler.OnBatch);

        using var publisher = await _streamingClient.CreatePublisher<Event>()
            .AddTopic<ExampleEvent1>()
            .Build()
            .PublishAsync(_events);

        // Consume
        using var subscription1 = await builder.Build().StartAsync().ConfigureAwait(false);
        using var subscription2 = await builder.Build().StartAsync().ConfigureAwait(false);

        // Assert
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }
}
