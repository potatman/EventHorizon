using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicAdmin<TMessage>
    where TMessage : ITopicMessage
{
    string GetTopic(Type type, string senderId = null);
    Task RequireTopicAsync(string str, CancellationToken ct);
    Task DeleteTopicAsync(string str, CancellationToken ct);
}
