using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicConsumer<T> : IDisposable
    where T : ITopicMessage
{
    Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct);
    Task AckAsync(params MessageContext<T>[] events);
    Task NackAsync(params MessageContext<T>[] events);
}