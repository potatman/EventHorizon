using System;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicProducer<in T> : IAsyncDisposable
    where T : ITopicMessage
{
    Task SendAsync(params T[] messages);
}
