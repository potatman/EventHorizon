using System;
using Insperex.EventHorizon.Abstractions.Util;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.Abstractions.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static void AddEventHorizon(this IServiceCollection collection, Action<EventHorizonConfigurator> configure)
        {
            configure(new EventHorizonConfigurator(collection));
            collection.AddSingleton<AttributeUtil>();
        }
    }
}
