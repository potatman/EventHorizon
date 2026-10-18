using System.Globalization;
using Destructurama;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Testing;
using EventHorizon.EventStreaming.InMemory.Extensions;
using EventHorizon.EventStreaming.Pulsar.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.XUnit3;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Util;

public static class HostTestUtil
{
    public static IHost GetPulsarHost(ITestOutputHelper output)
    {
        return GetHostBase(output)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddPulsarEventStream(hostContext.Configuration.GetSection("Pulsar").Bind);
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
                    x.AddInMemoryEventStream();
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
                config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .Destructure.UsingAttributes();

                if(output != null)
                    config.WriteTo.XUnit3TestOutput(new XUnit3TestOutputSink(new XUnit3TestOutputSinkOptions(FormatProvider: CultureInfo.InvariantCulture)) { TestOutputHelper = output }, LogEventLevel.Information);
            })
            .UseEnvironment("test");
    }
}
