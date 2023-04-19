using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Extensions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventSourcing.Test.Fakers;
using Insperex.EventHorizon.EventStore.Extensions;
using Insperex.EventHorizon.EventStore.InMemory.Extensions;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.InMemory.Extensions;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventSourcing.Test.Integration;

[Trait("Category", "Integration")]
public class SenderIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _host;
    private readonly Sender _sender;
    private Stopwatch _stopwatch;
    private readonly IStreamFactory _streamFactory;
    private readonly Sender _sender2;
    private readonly EventSourcingClient<Account> _eventSourcingClient;

    public SenderIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        _host = Host.CreateDefaultBuilder(new string[] { })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddInMemorySnapshotStore();
                services.AddInMemoryEventStream();
                // services.AddPulsarEventStream(hostContext.Configuration);
                // services.AddMongoDbSnapshotStore(hostContext.Configuration);
                
                services.AddEventSourcing();
                services.AddHostedAggregate<Account>();
                // services.AddHostedAggregate<UserAccount>();
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console()
                    .WriteTo.TestOutput(output, LogEventLevel.Information);
            })
            .UseEnvironment("test")
            .Build()
            .AddTestBucketIds();
        
        _sender = _host.Services.GetRequiredService<SenderBuilder>()
            .Timeout(TimeSpan.FromSeconds(30))
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
            .Build();
        
        _sender2 = _host.Services.GetRequiredService<SenderBuilder>()
            .Timeout(TimeSpan.FromSeconds(30))
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
            .Build();

        _eventSourcingClient = _host.Services.GetRequiredService<EventSourcingClient<Account>>();
        _streamFactory = _host.Services.GetRequiredService<IStreamFactory>();
    }
    
    public async Task InitializeAsync()
    {
        await _host.StartAsync();
        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _eventSourcingClient.GetSnapshotStore().DropDatabaseAsync(CancellationToken.None);
        foreach (var topic in _streamFactory.GetTopicResolver().GetTopics<Event>(typeof(Account)))
            await _streamFactory.CreateAdmin().DeleteTopicAsync(topic, CancellationToken.None);
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task TestSendAndReceiveAsync()
    {
        // Send Command
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(10);
        var command = new OpenAccount(100);
        var result1 = _sender2.SendAndReceiveAsync(streamId, command);
        var result2 = _sender.SendAndReceiveAsync("ABC", command);
        var result3 = _sender2.SendAndReceiveAsync("DFG", command);
        await Task.WhenAll(result1, result2, result3);

        // Assert Status
        Assert.Equal(AccountResponseStatus.Success, result1.Result.Status);

        // Assert Account
        var aggregate  = await _eventSourcingClient.GetSnapshotStore().GetAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, aggregate.State.Id);
        Assert.Equal(streamId, aggregate.Id);
        Assert.NotEqual(DateTime.MinValue, aggregate.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate.UpdatedDate);
        Assert.Equal(command.Amount, aggregate.State.Amount);
        
        // // Assert User Account
        // var store2 = _host.Services.GetRequiredService<Aggregator<Snapshot<UserAccount>, UserAccount>>();
        // var aggregate2  = await store2.GetAsync(streamId);
        // Assert.Equal(streamId, aggregate2.State.Id);
        // Assert.Equal(streamId, aggregate2.Id);
        // Assert.NotEqual(DateTime.MinValue, aggregate2.CreatedDate);
        // Assert.NotEqual(DateTime.MinValue, aggregate2.UpdatedDate);
        // Assert.Equal(command.Amount, aggregate2.State.Account.Amount);
    }

    [Fact]
    public async Task TestSendAndReceiveFailedAsync()
    {
        // Send Command
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(10);
        var command = new Withdrawal(100);
        var result = await _sender.SendAndReceiveAsync(streamId, command);

        // Assert
        Assert.Equal(AccountResponseStatus.WithdrawalDenied, result.Status);
    }

    [Fact]
    public async Task TestSenderTimedOut()
    {
        var sender = _host.Services.GetRequiredService<SenderBuilder>()
            .Timeout(TimeSpan.Zero)
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
            .Build();
        
        // Send Command
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(10);
        var command = new OpenAccount(100);
        var result = await sender.SendAndReceiveAsync(streamId, command);
        Assert.Equal(AccountResponseStatus.CommandTimedOut, result.Status);
    }
}