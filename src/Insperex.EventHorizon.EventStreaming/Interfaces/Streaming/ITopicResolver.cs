using System;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicResolver
{
    string GetTopic<TM>(Type type, string senderId = null) where TM : ITopicMessage;
}
