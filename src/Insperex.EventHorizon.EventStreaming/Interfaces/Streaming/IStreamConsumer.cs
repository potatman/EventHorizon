﻿using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface IStreamConsumer<TMessage> where TMessage : ITopicMessage
{
    public Task OnBatch(SubscriptionContext<TMessage> context);
}
