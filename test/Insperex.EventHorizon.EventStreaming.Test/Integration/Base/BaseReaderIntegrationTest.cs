using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Insperex.EventHorizon.EventStreaming.Test.Fakers;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Base;

[Trait("Category", "Integration")]
public abstract class BaseReaderIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private Stopwatch _stopwatch;
    private string _streamId;
    private Event[] _events;
    private readonly StreamingClient _streamingClient;
    private Publisher<Event> _publisher;

    protected BaseReaderIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        _outputHelper = outputHelper;
        _streamingClient = provider.GetRequiredService<StreamingClient>();
    }

    public async Task InitializeAsync()
    {
        // Note: uncomment for large dbs
        // var preEvents =_dataClassFixture.GetMessages(100000);
        // await publisher.PublishAsync(preEvents);

        // Publish Events
        _events = EventStreamingFakers.Feed1PriceChangedFaker.Generate(1000).Select(x => new Event(x.Id, x)).ToArray();
        _publisher = _streamingClient.CreatePublisher<Event>().AddStream<Feed1PriceChanged>().Build();
        await _publisher.PublishAsync(_events);

        // Setup
        _streamId = _events.Last().StreamId;
        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Feed1PriceChanged));
        await _publisher.DisposeAsync();
    }

    [Fact]
    public async Task TestReaderGetStreamId()
    {
        await using var reader = _streamingClient.CreateReader<Event>().AddStream<Feed1PriceChanged>().Keys(_streamId).Build();

        var events = await reader.GetNextAsync(_events.Length);

        // Assert
        var expected = _events.Where(x => x.StreamId == _streamId).ToArray();
        AssertUtil.AssertEventsValid(expected, events);
    }
}
