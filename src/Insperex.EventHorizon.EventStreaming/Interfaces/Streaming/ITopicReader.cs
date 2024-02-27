using System;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicReader<TMessage> : IAsyncDisposable
    where TMessage : ITopicMessage
{
    public Task<MessageContext<TMessage>[]> GetNextAsync(int batchSize, TimeSpan timeout);
}
