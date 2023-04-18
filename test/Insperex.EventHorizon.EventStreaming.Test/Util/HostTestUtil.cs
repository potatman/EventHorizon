using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.EventStreaming.InMemory.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Util;

public static class HostTestUtil
{
    public static IHost GetPulsarHost(ITestOutputHelper output)
    {
        return GetHostBase(output)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddPulsarEventStream(hostContext.Configuration);
            })
            .Build()
            .AddTestBucketIds();
    }
    
    public static IHost GetInMemoryHost(ITestOutputHelper output)
    {
        return GetHostBase(output)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddInMemoryEventStream();
            })
            .Build()
            .AddTestBucketIds();
    }
    

    private static IHostBuilder GetHostBase(ITestOutputHelper output)
    {
        return Host.CreateDefaultBuilder(new string[] { })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console();
                    
                if(output != null)
                    config.WriteTo.TestOutput(output, LogEventLevel.Information);
            })
            .UseEnvironment("test");
    }
}