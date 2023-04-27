using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.Abstractions.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static void AddEventHorizon(this IServiceCollection collection, IConfiguration config, Action<EventHorizonConfigurator> configure)
        {
            configure(new EventHorizonConfigurator(collection, config));
        }
    }
}
