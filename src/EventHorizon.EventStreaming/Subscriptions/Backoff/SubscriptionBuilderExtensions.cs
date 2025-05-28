using System;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.EventStreaming.Subscriptions.Backoff;

public static class SubscriptionBuilderExtensions
{
    public static SubscriptionBuilder<T> ExponentialBackoff<T>(this SubscriptionBuilder<T> builder,
        Func<ExponentialBackoffStrategyBuilder, ExponentialBackoffStrategyBuilder> config)
        where T : class, ITopicMessage, new()
    {
        ArgumentNullException.ThrowIfNull(config);
        var backoffBuilder = config(new ExponentialBackoffStrategyBuilder());
        return builder.BackoffStrategy(backoffBuilder.Build());
    }

    public static SubscriptionBuilder<T> FailedMessageRedeliveryDelay<T>(this SubscriptionBuilder<T> builder,
        TimeSpan delay)
        where T : class, ITopicMessage, new()
    {
        return builder.BackoffStrategy(new ConstantBackoffStrategy {Delay = delay});
    }
}
