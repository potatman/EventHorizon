using System;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;

public static class SubscriptionBuilderExtensions
{
    public static SubscriptionBuilder<T> FailedMessageRedeliveryDelay<T>(this SubscriptionBuilder<T> builder,
        TimeSpan delay)
        where T : class, ITopicMessage, new()
    {
        return builder.BackoffStrategy(new ConstantBackoffStrategy {Delay = delay});
    }
}
