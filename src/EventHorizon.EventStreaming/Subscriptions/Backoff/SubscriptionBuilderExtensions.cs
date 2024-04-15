using System;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.EventStreaming.Subscriptions.Backoff;

public static class SubscriptionBuilderExtensions
{
    public static SubscriptionBuilder<TMessage> ExponentialBackoff<TMessage>(this SubscriptionBuilder<TMessage> builder,
        Func<ExponentialBackoffStrategyBuilder, ExponentialBackoffStrategyBuilder> config)
        where TMessage : ITopicMessage
    {
        ArgumentNullException.ThrowIfNull(config);
        var backoffBuilder = config(new ExponentialBackoffStrategyBuilder());
        return builder.BackoffStrategy(backoffBuilder.Build());
    }

    public static SubscriptionBuilder<TMessage> FailedMessageRedeliveryDelay<TMessage>(this SubscriptionBuilder<TMessage> builder,
        TimeSpan delay)
        where TMessage : ITopicMessage
    {
        return builder.BackoffStrategy(new ConstantBackoffStrategy {Delay = delay});
    }
}
