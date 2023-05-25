using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.InMemory.Databases;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;

namespace Insperex.EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicProducer<T> : ITopicProducer<T>
    where T : class, ITopicMessage
{
    private readonly PublisherConfig _config;
    private readonly MessageDatabase _messageDatabase;

    public InMemoryTopicProducer(PublisherConfig config, MessageDatabase messageDatabase)
    {
        _config = config;
        _messageDatabase = messageDatabase;
    }

    public Task SendAsync(params T[] messages)
    {
        _messageDatabase.AddMessages(_config.Topic, messages);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
