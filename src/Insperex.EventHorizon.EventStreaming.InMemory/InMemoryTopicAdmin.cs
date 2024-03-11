using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventStreaming.InMemory.Databases;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

namespace Insperex.EventHorizon.EventStreaming.InMemory;

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
