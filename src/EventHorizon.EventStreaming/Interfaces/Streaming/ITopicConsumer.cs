using System;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicConsumer<T> : IAsyncDisposable
    where T : ITopicMessage
{
    Task InitAsync();
    Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct);
    Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks);
}
