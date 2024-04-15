using System;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicReader<TMessage> : IAsyncDisposable
    where TMessage : ITopicMessage
{
    public Task<MessageContext<TMessage>[]> GetNextAsync(int batchSize, TimeSpan timeout);
}
