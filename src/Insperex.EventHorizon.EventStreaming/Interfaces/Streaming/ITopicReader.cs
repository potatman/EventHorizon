using System;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicReader<T> : IAsyncDisposable
    where T : ITopicMessage
{
    public Task<MessageContext<T>[]> GetNextAsync(int batchSize, TimeSpan timeout);
}
