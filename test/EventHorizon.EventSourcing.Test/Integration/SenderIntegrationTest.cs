﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Destructurama;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Testing;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventSourcing.Extensions;
using EventHorizon.EventSourcing.Samples.Models.Actions;
using EventHorizon.EventSourcing.Samples.Models.Snapshots;
using EventHorizon.EventSourcing.Senders;
using EventHorizon.EventSourcing.Test.Fakers;
using EventHorizon.EventStore.ElasticSearch.Extensions;
using EventHorizon.EventStore.Extensions;
using EventHorizon.EventStore.InMemory.Extensions;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.MongoDb.Extensions;
using EventHorizon.EventStreaming;
using EventHorizon.EventStreaming.InMemory.Extensions;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Pulsar.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventSourcing.Test.Integration;

[Trait("Category", "Integration")]
public class SenderIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _senderHost;
    private readonly Sender _sender;
    private Stopwatch _stopwatch;
    private readonly Sender _sender2;
    private readonly EventSourcingClient<Account> _eventSourcingClient;
    private readonly StreamingClient _streamingClient;
    private readonly IHost _consumerHost;

    public SenderIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        var postfix = $"_{Guid.NewGuid().ToString()[..8]}";
        _senderHost = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddEventSourcing()

                        // Stores
                        .AddInMemoryViewStore()
                        .AddElasticSnapshotStore(hostContext.Configuration.GetSection("ElasticSearch").Bind)
                        .AddPulsarEventStream(hostContext.Configuration.GetSection("Pulsar").Bind);
                });
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.TestOutput(output, LogEventLevel.Information, formatProvider: CultureInfo.InvariantCulture)
                    .Destructure.UsingAttributes();
            })
            .UseEnvironment("test")
            .Build()
            .AddTestBucketIds(postfix);

        _consumerHost =  Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddEventSourcing()

                        // Hosts
                        .ApplyRequestsToSnapshot<Account>(a => a.BatchSize(10000))

                        // Stores
                        .AddInMemoryViewStore()
                        .AddElasticSnapshotStore(hostContext.Configuration.GetSection("ElasticSearch").Bind)
                        .AddPulsarEventStream(hostContext.Configuration.GetSection("Pulsar").Bind);
                });
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.TestOutput(output, LogEventLevel.Information, formatProvider: CultureInfo.InvariantCulture)
                    .Destructure.UsingAttributes();
            })
            .UseEnvironment("test")
            .Build()
            .AddTestBucketIds(postfix);

        _sender = _senderHost.Services.GetRequiredService<SenderBuilder>()
            .Timeout(TimeSpan.FromSeconds(30))
            .GetErrorResult((req, status, error) => new AccountResponse(status, error))
            .Build();

        _sender2 = _senderHost.Services.GetRequiredService<SenderBuilder>()
            .Timeout(TimeSpan.FromSeconds(120))
            .GetErrorResult((req, status, error) => new AccountResponse(status, error))
            .Build();

        _eventSourcingClient = _senderHost.Services.GetRequiredService<EventSourcingClient<Account>>();
        _streamingClient = _senderHost.Services.GetRequiredService<StreamingClient>();
    }

    public async Task InitializeAsync()
    {
        await _consumerHost.StartAsync();
        await _senderHost.StartAsync();
        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _eventSourcingClient.GetSnapshotStore().DropDatabaseAsync(CancellationToken.None);
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Account));
        await _streamingClient.GetAdmin<Request>().DeleteTopicAsync(typeof(Account));
        await _senderHost.StopAsync();
        await _consumerHost.StopAsync();
        _senderHost.Dispose();
    }

    [Fact]
    public async Task TestSendAndReceiveAsync()
    {
        // Send Command
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(10);
        var result1 = await _sender2.SendAndReceiveAsync(streamId, new OpenAccount(1000));
        var result2 = _sender2.SendAndReceiveAsync(streamId, new Withdrawal(100));
        var result3 = _sender2.SendAndReceiveAsync(streamId, new Deposit(100));
        var result4 = _sender.SendAndReceiveAsync("ABC", new OpenAccount(100));
        var result5 = _sender.SendAndReceiveAsync("DFG", new OpenAccount(100));
        await Task.WhenAll(result2, result3, result4, result5);

        // Assert Status
        Assert.True(HttpStatusCode.OK == result1.StatusCode, result1.Error);
        Assert.True(HttpStatusCode.OK == (await result2).StatusCode, (await result2).Error);
        Assert.True(HttpStatusCode.OK == (await result3).StatusCode, (await result3).Error);
        Assert.True(HttpStatusCode.OK == (await result4).StatusCode, (await result4).Error);
        Assert.True(HttpStatusCode.OK == (await result5).StatusCode, (await result5).Error);

        // Assert Account
        var aggregate  = await _eventSourcingClient.GetSnapshotStore().GetAsync(streamId, CancellationToken.None);
        var events = await _eventSourcingClient.Aggregator().Build().GetEventsAsync(new[] { streamId });
        Assert.Equal(streamId, aggregate.State.Id);
        Assert.Equal(streamId, aggregate.Id);
        Assert.NotEqual(DateTime.MinValue, aggregate.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate.UpdatedDate);
        Assert.Equal(3, events.Length);
        Assert.Equal(1000, aggregate.State.Amount);

        // // Assert User Account
        // var store2 = _host.Services.GetRequiredService<Aggregator<Snapshot<UserAccount>, UserAccount>>();
        // var aggregate2  = await store2.GetAsync(streamId);
        // Assert.Equal(streamId, aggregate2.State.Id);
        // Assert.Equal(streamId, aggregate2.Id);
        // Assert.NotEqual(DateTime.MinValue, aggregate2.CreatedDate);
        // Assert.NotEqual(DateTime.MinValue, aggregate2.UpdatedDate);
        // Assert.Equal(command.Amount, aggregate2.State.Account.Amount);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 1)]
    [InlineData(1000, 1)]
    // [InlineData(10000, 10)]
    // [InlineData(100000, 10)]
    public async Task TestLargeSendAndReceiveAsync(int numOfEvents, int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            // Send Command
            var largeRequests  = Enumerable.Range(0, numOfEvents).Select(x => new Deposit(1)).ToArray();
            var allRequests = largeRequests.Select(x => new Request(Guid.NewGuid().ToString(), x)).ToArray();
            var responses = await _sender2.SendAndReceiveAsync<Account>(allRequests);

            Assert.Equal(numOfEvents, responses.Length);
            foreach (var response in responses)
                Assert.True(response.StatusCode < 300, response.Error);
        }
    }

    [Fact]
    public async Task TestSendAndReceiveFailedAsync()
    {
        // Send Command
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(10);
        var command = new Withdrawal(100);
        var result = await _sender.SendAndReceiveAsync(streamId, command);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal(AccountConstants.WithdrawalDenied, result.Error);
    }

    [Fact]
    public async Task TestSenderTimedOut()
    {
        var sender = _senderHost.Services.GetRequiredService<SenderBuilder>()
            .Timeout(TimeSpan.Zero)
            .GetErrorResult((req, status, error) => new AccountResponse(status, error))
            .Build();

        // Send Command
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(10);
        var command = new OpenAccount(100);
        var result = await sender.SendAndReceiveAsync(streamId, command);
        Assert.Equal(HttpStatusCode.RequestTimeout, result.StatusCode);
    }
}
