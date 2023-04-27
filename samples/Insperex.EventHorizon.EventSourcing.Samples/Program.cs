using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Insperex.EventHorizon.EventSourcing.Samples;

public class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(hostContext.Configuration, x =>
                {
                    x.AddEventSourcing()

                        // Hosted
                        .AddHostedSnapshot<Account>(h =>
                            h.RetryLimit(5)
                                .IsRebuildEnabled(true))
                        .AddHostedViewIndexer<SearchAccountView>(h =>
                            h.RetryLimit(5)
                                .BeforeSave(batch =>
                                {
                                    // Additional logic
                                }))
                        .AddHostedSubscription<AccountSubscription, Event>()

                        // Stores
                        .AddMongoDbSnapshotStore()
                        .AddMongoDbLockStore()
                        .AddElasticViewStore()
                        .AddPulsarEventStream();
                });
            })
            .UseSerilog((_, config) => { config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture); })
            .UseEnvironment("local")
            .Build();

        await host.StartAsync();

        // Send command and receive result
        var client = host.Services.GetRequiredService<EventSourcingClient<Account>>();

        var result = await client.CreateSender()
            .Timeout(TimeSpan.FromSeconds(120))
            .GetErrorResult((status, error) =>
            {
                return status switch
                {
                    AggregateStatus.CommandTimedOut => new AccountResponse(AccountResponseStatus.CommandTimedOut),
                    AggregateStatus.LoadSnapshotFailed => new AccountResponse(AccountResponseStatus.LoadSnapshotFailed),
                    AggregateStatus.SaveSnapshotFailed => new AccountResponse(AccountResponseStatus.SaveSnapshotFailed),
                    AggregateStatus.SaveEventsFailed => new AccountResponse(AccountResponseStatus.SaveEventsFailed),
                    _ => throw new Exception($"Unhandled ResultStatus {status} => {error}")
                };
            })
            .Build()
            .SendAndReceiveAsync("123", new OpenAccount(100));

    }
}

