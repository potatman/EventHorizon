using System;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions.Extensions;

public static class SubscriptionServiceCollectionExtensions
{
    public static IServiceCollection AddHostedSubscription<TH, TM>(this IServiceCollection collection,
        Action<SubscriptionBuilder<TM>> action = null)
        where TH : class, ITopicHandler<TM>
        where TM : class, ITopicMessage, new()
    {
        collection.AddScoped<TH>();
        collection.AddHostedService(x =>
        {
            using var scope = x.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TH>();
            var client = scope.ServiceProvider.GetRequiredService<StreamingClient>();
            var builder = client.CreateSubscription<TM>();
            action?.Invoke(builder);

            return new SubscriptionHostedService<TM>(builder.OnBatch(handler.OnBatch).Build());
        });
        return collection;
    }

    public static IServiceCollection AddHostedSubscription<T>(this IServiceCollection collection,
        Action<SubscriptionBuilder<T>> action = null)
        where T : class, ITopicMessage, new()
    {
        collection.AddHostedService(x =>
        {
            using var scope = x.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<StreamingClient>();
            var builder = client.CreateSubscription<T>()
                .SubscriptionName(typeof(T).Name);
            action?.Invoke(builder);
            return new SubscriptionHostedService<T>(builder.Build());
        });
        return collection;
    }
}