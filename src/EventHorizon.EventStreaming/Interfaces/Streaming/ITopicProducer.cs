using System;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicProducer<in TMessage> : IAsyncDisposable
    where TMessage : ITopicMessage
{
    Task SendAsync(params TMessage[] messages);
}
