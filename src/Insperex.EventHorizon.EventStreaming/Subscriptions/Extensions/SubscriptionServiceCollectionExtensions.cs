using System;
using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions.Extensions;

public static class SubscriptionServiceCollectionExtensions
{
    public static EventHorizonConfigurator AddSubscription<TConsumer, TMessage>(this EventHorizonConfigurator configurator,
        Action<SubscriptionBuilder<TMessage>> action = null)
        where TConsumer : class, IStreamConsumer<TMessage>
        where TMessage : class, ITopicMessage, new()
    {
        configurator.Collection.AddHostedService<SubscriptionHostedService<TMessage>>();
        configurator.Collection.AddScoped<TConsumer>();
        configurator.Collection.AddTransient(x =>
        {
            using var scope = x.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TConsumer>();
            var client = scope.ServiceProvider.GetRequiredService<StreamingClient>();
            var builder = client.CreateSubscription<TMessage>()
                .SubscriptionName(typeof(TConsumer).Name);
            action?.Invoke(builder);

            return builder.OnBatch(handler.OnBatch).Build();
        });
        return configurator;
    }

    public static EventHorizonConfigurator AddSubscription<TMessage>(this EventHorizonConfigurator configurator,
        Action<SubscriptionBuilder<TMessage>> action = null)
        where TMessage : class, ITopicMessage
    {
        configurator.Collection.AddHostedService<SubscriptionHostedService<TMessage>>();
        configurator.Collection.AddTransient(x =>
        {
            using var scope = x.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<StreamingClient>();
            var builder = client.CreateSubscription<TMessage>()
                .SubscriptionName(typeof(TMessage).Name);
            action?.Invoke(builder);

            return builder.Build();
        });
        return configurator;
    }
}
