using System;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicProducer<in TMessage> : IAsyncDisposable
    where TMessage : ITopicMessage
{
    Task SendAsync(params TMessage[] messages);
}
