using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicAdmin<TMessage>
    where TMessage : ITopicMessage
{
    Task RequireTopicAsync(string str, CancellationToken ct);
    Task DeleteTopicAsync(string str, CancellationToken ct);
}
