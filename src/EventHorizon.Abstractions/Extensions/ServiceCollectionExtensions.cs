using System;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Util;
using Microsoft.Extensions.DependencyInjection;

namespace EventHorizon.Abstractions.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static void AddEventHorizon(this IServiceCollection collection, Action<EventHorizonConfigurator> configure)
        {
            collection.AddSingleton<AttributeUtil>();
            collection.AddSingleton<Formatter>();
            collection.AddSingleton<IFormatterPostfix, DefaultFormatterPostfix>();
            collection.AddSingleton<ITopicFormatter, DefaultTopicFormatter>();
            collection.AddSingleton<IDatabaseFormatter, DefaultDatabaseFormatter>();

            configure(new EventHorizonConfigurator(collection));
        }
    }
}
