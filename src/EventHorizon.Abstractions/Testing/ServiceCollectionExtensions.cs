using System;
using EventHorizon.Abstractions.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventHorizon.Abstractions.Testing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTestingForEventHorizon(this IServiceCollection collection, string postfix = null)
    {
        postfix ??= Guid.NewGuid().ToString()[..8];

        collection.Replace(new ServiceDescriptor(typeof(IFormatterPostfix),
            x => new TestFormatterPostfix(postfix),
            ServiceLifetime.Singleton)
        );

        return collection;
    }

}
