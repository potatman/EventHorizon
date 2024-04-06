using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Destructurama;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.EventSourcing.Extensions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Actions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventSourcing.Test.Fakers;
using Insperex.EventHorizon.EventStore.ElasticSearch.Extensions;
using Insperex.EventHorizon.EventStore.Extensions;
using Insperex.EventHorizon.EventStore.InMemory.Extensions;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Microsoft.Extensions.Configuration;
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
    private readonly IHost _senderHost;
    private readonly Sender<Account> _sender;
    private Stopwatch _stopwatch;
    private readonly Sender<Account> _sender2;
    private readonly EventSourcingClient<Account> _eventSourcingClient;
    private readonly StreamingClient _streamingClient;
    private readonly IHost _consumerHost;

    public SenderIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        var postfix = Guid.NewGuid().ToString()[..8];
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
                services.AddTestingForEventHorizon(postfix);
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.TestOutput(output, LogEventLevel.Information, formatProvider: CultureInfo.InvariantCulture)
                    .Destructure.UsingAttributes();
            })
            .UseEnvironment("test")
            .Build();

        _consumerHost =  Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddEventSourcing()

                        // Hosts
                        .HandleRequests<Account>(a => a.WithBatchSize(10000))

                        // Stores
                        .AddInMemoryViewStore()
                        .AddElasticSnapshotStore(hostContext.Configuration.GetSection("ElasticSearch").Bind)
                        .AddPulsarEventStream(hostContext.Configuration.GetSection("Pulsar").Bind);
                });
                services.AddTestingForEventHorizon(postfix);
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.TestOutput(output, LogEventLevel.Information, formatProvider: CultureInfo.InvariantCulture)
                    .Destructure.UsingAttributes();
            })
            .UseEnvironment("test")
            .Build();

        _sender = _senderHost.Services.GetRequiredService<SenderBuilder<Account>>()
            .Timeout(TimeSpan.FromSeconds(30))
            .GetErrorResult((req, status, error) => new AccountResponse(status, error))
            .Build();

        _sender2 = _senderHost.Services.GetRequiredService<SenderBuilder<Account>>()
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
        await _eventSourcingClient.Aggregator().Build().DropAllAsync(CancellationToken.None);
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
        var aggregate  = await _eventSourcingClient.GetSnapshotStore().Build().GetAsync(streamId, CancellationToken.None);
        var events = await _eventSourcingClient.Aggregator().Build().GetEventsAsync(new[] { streamId });
        Assert.Equal(streamId, aggregate.Payload.Id);
        Assert.Equal(streamId, aggregate.Id);
        Assert.NotEqual(DateTime.MinValue, aggregate.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate.UpdatedDate);
        Assert.Equal(3, events.Length);
        Assert.Equal(1000, aggregate.Payload.Amount);

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
            var responses = await _sender2.SendAndReceiveAsync(allRequests);

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
        var sender = _senderHost.Services.GetRequiredService<SenderBuilder<Account>>()
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
