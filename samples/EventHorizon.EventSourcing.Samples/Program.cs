using System.Globalization;
using System.Threading.Tasks;
using Destructurama;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventSourcing.Extensions;
using EventHorizon.EventSourcing.Samples.Middleware;
using EventHorizon.EventSourcing.Samples.Models.Snapshots;
using EventHorizon.EventSourcing.Samples.Models.View;
using EventHorizon.EventSourcing.Samples.Subscriptions;
using EventHorizon.EventStore.ElasticSearch.Extensions;
using EventHorizon.EventStore.MongoDb.Extensions;
using EventHorizon.EventStreaming.Pulsar.Extensions;
using EventHorizon.EventStreaming.Subscriptions.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EventHorizon.EventSourcing.Samples;

public class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .UseSerilog((_, config) => { config
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .Destructure.UsingAttributes();
            })
            .UseEnvironment("local")
            .ConfigureWebHostDefaults(builder =>
            {
                builder.ConfigureServices((context, services) =>
                    {
                        services.AddMvc();
                        services.AddEndpointsApiExplorer();
                        services.AddControllers();
                        services.AddSwaggerGen();

                        services.AddScoped<SearchAccountViewMiddleware>();
                        services.AddEventHorizon(x =>
                        {
                            x.AddEventSourcing()

                                // Stores
                                .AddMongoDbSnapshotStore(context.Configuration.GetSection("MongoDb").Bind)
                                .AddElasticViewStore(context.Configuration.GetSection("ElasticSearch").Bind)
                                .AddPulsarEventStream(context.Configuration.GetSection("Pulsar").Bind)

                                // Hosted
                                .HandleRequests<Account>()
                                .ApplyEvents<SearchAccountView>(h => h.WithMiddleware<SearchAccountViewMiddleware>())

                                .AddSubscription<AccountConsumer, Event>(s => s.AddStream<IEvent<Account>>());
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseSwagger();
                        app.UseSwaggerUI();
                        app.UseRouting();
                        app.UseEndpoints(e => e.MapEventSourcingEndpoints<Account>());
                        app.UseEndpoints(e => e.MapEventSourcingEndpoints<User>());
                        // app.MapEventSourcingEndpoints<Account>();
                        // app.MapEventSourcingEndpoints<User>();
                    });
            })
            .Build()
            .RunAsync()
            ;
    }
}

