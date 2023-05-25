using System.Globalization;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.EventStore.ElasticSearch.Extensions;
using Insperex.EventHorizon.EventStore.Ignite.Extensions;
using Insperex.EventHorizon.EventStore.InMemory.Extensions;
using Insperex.EventHorizon.EventStore.MongoDb.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStore.Test.Util;

public static class HostTestUtil
{
    public static IHost GetElasticHost(ITestOutputHelper output)
    {
        return GetHostBase(output)
            .ConfigureServices((context, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddElasticSnapshotStore(context.Configuration.GetSection("ElasticSearch").Bind)
                        .AddElasticViewStore(context.Configuration.GetSection("ElasticSearch").Bind);
                });
            })
            .Build()
            .AddTestBucketIds();
    }

    public static IHost GetIgniteHost(ITestOutputHelper output)
    {
        return GetHostBase(output)
            .ConfigureServices((context, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddIgniteSnapshotStore(context.Configuration.GetSection("Ignite").Bind)
                        .AddIgniteViewStore(context.Configuration.GetSection("Ignite").Bind);
                });
            })
            .Build()
            .AddTestBucketIds();
    }

    public static IHost GetInMemoryHost(ITestOutputHelper output)
    {
        return GetHostBase(output)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddInMemorySnapshotStore()
                        .AddInMemoryViewStore();
                });
            })
            .Build()
            .AddTestBucketIds();
    }

    public static IHost GetMongoDbHost(ITestOutputHelper output)
    {
        return GetHostBase(output)
            .ConfigureServices((context, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddMongoDbSnapshotStore(context.Configuration.GetSection("MongoDb").Bind)
                        .AddMongoDbViewStore(context.Configuration.GetSection("MongoDb").Bind);
                });
            })
            .Build()
            .AddTestBucketIds();
    }

    private static IHostBuilder GetHostBase(ITestOutputHelper output)
    {
        return Host.CreateDefaultBuilder(System.Array.Empty<string>())
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

                if(output != null)
                    config.WriteTo.TestOutput(output, LogEventLevel.Information, formatProvider: CultureInfo.InvariantCulture);
            })
            .UseEnvironment("test");
    }
}
