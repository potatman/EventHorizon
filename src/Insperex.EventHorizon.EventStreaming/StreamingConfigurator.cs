using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Admins;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventStreaming
{
    public class StreamingConfigurator
    {
        internal readonly IServiceCollection Collection;
        internal readonly IConfiguration Config;

        public StreamingConfigurator(IServiceCollection collection, IConfiguration config)
        {
            Collection = collection;
            Config = config;
        }
    }
}
