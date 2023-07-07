using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicConsumer<T> : IAsyncDisposable
    where T : ITopicMessage
{
    Task InitAsync();
    Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct);
    Task AckAsync(params MessageContext<T>[] messages);
    Task NackAsync(params MessageContext<T>[] messages);
}
