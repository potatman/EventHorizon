using System.Globalization;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Extensions;
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
        var opts = new WebApplicationOptions { EnvironmentName = "local" };
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

                // Hosted
                .ApplyRequestsToSnapshot<Account>()
                .ApplyEventsToView<SearchAccountView>(h =>
                    h.UseMiddleware<SearchAccountViewMiddleware>())

                .AddSubscription<AccountConsumer, Event>()

                // Stores
                .AddMongoDbSnapshotStore(builder.Configuration)
                .AddElasticViewStore(builder.Configuration)
                .AddPulsarEventStream(builder.Configuration);
        });

        var app = builder.Build();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapEventSourcingEndpoints<Account>();
        app.MapEventSourcingEndpoints<User>();

        await app.RunAsync();



        // var host = Host.CreateDefaultBuilder(args)
        //     .ConfigureWebHostDefaults(x =>
        //     {
        //         x.ConfigureServices((hostContext, services) =>
        //         {
        //             services.AddMvc();
        //             services.AddEndpointsApiExplorer();
        //             services.AddControllers();
        //             services.AddSwaggerGen();
        //
        //             services.AddScoped<SearchAccountViewMiddleware>();
        //             services.AddEventHorizon(x =>
        //             {
        //                 x.AddEventSourcing()
        //
        //                     // Hosted
        //                     .ApplyRequestsToSnapshot<Account>()
        //                     .ApplyEventsToView<SearchAccountView>(h =>
        //                         h.UseMiddleware<SearchAccountViewMiddleware>())
        //
        //                     .AddSubscription<AccountConsumer, Event>()
        //
        //                     // Stores
        //                     .AddMongoDbSnapshotStore(hostContext.Configuration)
        //                     .AddElasticViewStore(hostContext.Configuration)
        //                     .AddPulsarEventStream(hostContext.Configuration);
        //             });
        //         });
        //         x.Configure(app =>
        //         {
        //             app.UseSwagger();
        //             app.UseSwaggerUI();
        //             app.UseRouter(r =>
        //             {
        //
        //                 r.MapGet("hello/{name}", (req, res, data) =>
        //                 {
        //                     return res.WriteAsync($"Hello, {data.Values["name"]}!");
        //                 });
        //             });
        //         });
        //     })
        //
        //     .UseSerilog((_, config) => { config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture); })
        //     .UseEnvironment("local")
        //     .Build();
        //
        // await host.RunAsync();

        // // Send command and receive result
        // var client = host.Services.GetRequiredService<EventSourcingClient<Account>>();
        //
        // var result = await client.CreateSender()
        //     .Timeout(TimeSpan.FromSeconds(120))
        //     .GetErrorResult((status, error) =>
        //     {
        //         switch (status)
        //         {
        //             case AggregateStatus.CommandTimedOut: return new AccountResponse(AccountResponseStatus.CommandTimedOut, error);
        //             case AggregateStatus.LoadSnapshotFailed: return new AccountResponse(AccountResponseStatus.LoadSnapshotFailed, error);
        //             case AggregateStatus.HandlerFailed: return new AccountResponse(AccountResponseStatus.HandlerFailed, error);
        //             case AggregateStatus.BeforeSaveFailed: return new AccountResponse(AccountResponseStatus.BeforeSaveFailed, error);
        //             case AggregateStatus.AfterSaveFailed: return new AccountResponse(AccountResponseStatus.AfterSaveFailed, error);
        //             case AggregateStatus.SaveSnapshotFailed: return new AccountResponse(AccountResponseStatus.SaveSnapshotFailed, error);
        //             case AggregateStatus.SaveEventsFailed: return new AccountResponse(AccountResponseStatus.SaveEventsFailed, error);
        //             default: throw new Exception($"Unhandled ResultStatus {status} => {error}");
        //         }
        //     })
        //     .Build()
        //     .SendAndReceiveAsync("123", new OpenAccount(100));

    }
}

