using System;
using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace EventHorizon.EventStreaming.Subscriptions.Extensions;

public static class SubscriptionServiceCollectionExtensions
{
    public static EventHorizonConfigurator AddSubscription<TH, TM>(this EventHorizonConfigurator configurator,
        Action<SubscriptionBuilder<TM>> action = null)
        where TH : class, IStreamConsumer<TM>
        where TM : class, ITopicMessage, new()
    {
        configurator.Collection.AddHostedService<SubscriptionHostedService<TM>>();
        configurator.Collection.AddScoped<TH>();
        configurator.Collection.AddTransient(x =>
        {
            using var scope = x.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TH>();
            var client = scope.ServiceProvider.GetRequiredService<StreamingClient>();
            var builder = client.CreateSubscription<TM>()
                .SubscriptionName(typeof(TH).Name);
            action?.Invoke(builder);

            return builder.OnBatch(handler.OnBatch).Build();
        });
        return configurator;
    }

    public static EventHorizonConfigurator AddSubscription<TM>(this EventHorizonConfigurator configurator,
        Action<SubscriptionBuilder<TM>> action = null)
        where TM : class, ITopicMessage, new()
    {
        configurator.Collection.AddHostedService<SubscriptionHostedService<TM>>();
        configurator.Collection.AddTransient(x =>
        {
            using var scope = x.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<StreamingClient>();
            var builder = client.CreateSubscription<TM>()
                .SubscriptionName(typeof(TM).Name);
            action?.Invoke(builder);

            return builder.Build();
        });
        return configurator;
    }
}
