using System;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicResolver
{
    string[] GetTopics<TM>(Type type, string topicName = null) where TM : ITopicMessage;
}