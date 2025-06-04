using System;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicReader<T> : IAsyncDisposable
    where T : ITopicMessage
{
    public Task<MessageContext<T>[]> GetNextAsync(int batchSize, TimeSpan timeout);
}
