using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.Abstractions.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.Abstractions.Testing;

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
