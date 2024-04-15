using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.InMemory.Databases;
using EventHorizon.EventStreaming.Interfaces.Streaming;

namespace EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicAdmin<TMessage> : ITopicAdmin<TMessage>
    where TMessage : ITopicMessage
{
    private readonly IndexDatabase _indexDatabase;
    private readonly MessageDatabase _messageDatabase;
    private readonly ConsumerDatabase _consumerDatabase;

    public InMemoryTopicAdmin(MessageDatabase messageDatabase, IndexDatabase indexDatabase,
        ConsumerDatabase consumerDatabase)
    {
        _messageDatabase = messageDatabase;
        _indexDatabase = indexDatabase;
        _consumerDatabase = consumerDatabase;
    }

    public Task RequireTopicAsync(string str, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task DeleteTopicAsync(string str, CancellationToken ct)
    {
        _messageDatabase.DeleteTopic(str);
        _indexDatabase.DeleteTopic(str);
        _consumerDatabase.DeleteTopic(str);
        return Task.CompletedTask;
    }

    public Task RequireNamespace()
    {
        return Task.CompletedTask;
    }
}
