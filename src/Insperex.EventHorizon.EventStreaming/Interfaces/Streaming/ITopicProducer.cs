using System;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicProducer<in T> : IAsyncDisposable
    where T : ITopicMessage
{
    Task SendAsync(params T[] messages);
}
