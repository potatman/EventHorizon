using System.Globalization;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Extensions;
using Insperex.EventHorizon.EventSourcing.Samples.Middleware;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;
using Insperex.EventHorizon.EventSourcing.Samples.Subscriptions;
using Insperex.EventHorizon.EventStore.ElasticSearch.Extensions;
using Insperex.EventHorizon.EventStore.MongoDb.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Insperex.EventHorizon.EventSourcing.Samples;

public class Program
{
    static async Task Main(string[] args)
    {
        var opts = new WebApplicationOptions { EnvironmentName = "local", Args = args };
        var builder = WebApplication.CreateBuilder(opts);

        builder.Host.UseSerilog((_, config) => { config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture); });

        var services = builder.Services;
        services.AddMvc();
        services.AddEndpointsApiExplorer();
        services.AddControllers();
        services.AddSwaggerGen();

        services.AddScoped<SearchAccountViewMiddleware>();
        services.AddEventHorizon(x =>
        {
            x.AddEventSourcing()

                // Stores
                .AddMongoDbSnapshotStore(builder.Configuration)
                .AddElasticViewStore(builder.Configuration)
                .AddPulsarEventStream(builder.Configuration)

                // Hosted
                .ApplyRequestsToSnapshot<Account>()
                .ApplyEventsToView<SearchAccountView>(h =>
                    h.UseMiddleware<SearchAccountViewMiddleware>())

                .AddSubscription<AccountConsumer, Event>();
        });

        var app = builder.Build();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapEventSourcingEndpoints<Account>();
        app.MapEventSourcingEndpoints<User>();

        await app.RunAsync();
    }
}

