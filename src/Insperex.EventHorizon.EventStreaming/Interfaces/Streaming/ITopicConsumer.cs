using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicConsumer<TMessage> : IAsyncDisposable
    where TMessage : ITopicMessage
{
    Task InitAsync();
    Task<MessageContext<TMessage>[]> NextBatchAsync(CancellationToken ct);
    Task FinalizeBatchAsync(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks);
}
