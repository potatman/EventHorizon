using System;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;

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
}
