using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.InMemory.Databases;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

namespace Insperex.EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicAdmin<TMessage> : ITopicAdmin<TMessage>
    where TMessage : ITopicMessage
{
    private readonly IndexDatabase _indexDatabase;
    private readonly AttributeUtil _attributeUtil;
    private readonly MessageDatabase _messageDatabase;
    private readonly ConsumerDatabase _consumerDatabase;

    public InMemoryTopicAdmin(AttributeUtil attributeUtil, MessageDatabase messageDatabase, IndexDatabase indexDatabase,
        ConsumerDatabase consumerDatabase)
    {
        _attributeUtil = attributeUtil;
        _messageDatabase = messageDatabase;
        _indexDatabase = indexDatabase;
        _consumerDatabase = consumerDatabase;
    }

    public string GetTopic(Type type, string senderId = null)
    {
        var attribute = _attributeUtil.GetAll<StreamAttribute>(type).FirstOrDefault(x => x.SubType == null);
        if (attribute == null) return null;

        var topic = senderId == null ? attribute.Topic : $"{attribute.Topic}-{senderId}";
        return $"in-memory://{typeof(TMessage).Name}/{topic}".Replace("$type", typeof(TMessage).Name);
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
