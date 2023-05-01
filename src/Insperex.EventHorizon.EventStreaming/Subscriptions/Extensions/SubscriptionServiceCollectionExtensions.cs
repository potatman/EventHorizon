using System;
using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions.Extensions;

public static class SubscriptionServiceCollectionExtensions
{
    public static EventHorizonConfigurator AddHostedSubscription<TH, TM>(this EventHorizonConfigurator configurator,
        Action<SubscriptionBuilder<TM>> action = null)
        where TH : class, IStreamConsumer<TM>
        where TM : class, ITopicMessage, new()
    {
        configurator.Collection.AddScoped<TH>();
        configurator.Collection.AddHostedService(x =>
        {
            using var scope = x.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TH>();
            var client = scope.ServiceProvider.GetRequiredService<StreamingClient>();
            var builder = client.CreateSubscription<TM>()
                .SubscriptionName(typeof(TH).Name);
            action?.Invoke(builder);

            return new SubscriptionHostedService<TM>(builder.OnBatch(handler.OnBatch).Build());
        });
        return configurator;
    }

    public static EventHorizonConfigurator AddHostedSubscription<T>(this EventHorizonConfigurator configurator,
        Action<SubscriptionBuilder<T>> action = null)
        where T : class, ITopicMessage, new()
    {
        configurator.Collection.AddHostedService(x =>
        {
            using var scope = x.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<StreamingClient>();
            var builder = client.CreateSubscription<T>()
                .SubscriptionName(typeof(T).Name);
            action?.Invoke(builder);
            return new SubscriptionHostedService<T>(builder.Build());
        });
        return configurator;
    }
}
