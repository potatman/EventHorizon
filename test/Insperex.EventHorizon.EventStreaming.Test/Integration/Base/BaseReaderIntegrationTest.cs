using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Test.Fakers;
using Insperex.EventHorizon.EventStreaming.Test.Models;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Base;

[Trait("Category", "Integration")]
[Collection("Integration")]
public abstract class BaseReaderIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private Stopwatch _stopwatch;
    private string _streamId;
    private Event[] _events;
    private readonly StreamingClient _streamingClient;
    private readonly IStreamFactory _streamFactory;

    protected BaseReaderIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        _outputHelper = outputHelper;
        _streamingClient = provider.GetRequiredService<StreamingClient>();
        _streamFactory = provider.GetRequiredService<IStreamFactory>();
    }

    public async Task InitializeAsync()
    {
        // Note: uncomment for large dbs
        // var preEvents =_dataClassFixture.GetMessages(100000);
        // await publisher.PublishAsync(preEvents);
        
        // Publish Events
        _events = EventStreamingFakers.EventFaker.Generate(1000).ToArray();
        using var publisher = _streamingClient.CreatePublisher<Event>().AddTopic<ExampleEvent1>().Build();
        await publisher.PublishAsync(_events);
        await Task.Delay(2000);
        
        // Setup
        _streamId = _events.Last().StreamId;
        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        foreach (var topic in _streamFactory.GetTopicResolver().GetTopics<Event>(typeof(ExampleEvent1)))
            await _streamFactory.CreateAdmin().DeleteTopicAsync(topic, CancellationToken.None);
    }
    
    [Fact]
    public async Task TestReaderGetStreamId()
    {
        using var reader = _streamingClient.CreateReader<Event>().AddTopic<ExampleEvent1>().Keys(_streamId).Build();
        
        var events = await reader.GetNextAsync(_events.Length);
        
        // Assert
        var expected = _events.Where(x => x.StreamId == _streamId).ToArray();
        AssertUtil.AssertEventsValid(expected, events);
    }
}