using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Actions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;
using Insperex.EventHorizon.EventStore.Models;
using Xunit;

namespace Insperex.EventHorizon.EventSourcing.Test.Unit;

[Trait("Category", "Unit")]
public class AggregateUnitTests
{
    private readonly string _streamId;

    public AggregateUnitTests()
    {
        _streamId = "123";
    }

    [Fact]
    public void TestAggregateFromEvents()
    {
        var events = Enumerable.Range(0, 5).Select(x => new AccountCredited(100)).ToArray();
        var eventWrappers = events.Select((x,i) => new Event(_streamId, i, x)).ToArray();

        var typeDict = ReflectionFactory.GetStateDetail(typeof(Account)).MessageTypeDict[typeof(Event)];
        var messages = eventWrappers.Select(x => new MessageContext<Event>(x, new TopicData(Guid.NewGuid().ToString(), "topic", DateTime.UtcNow), typeDict)).ToArray();
        var aggregate = new Aggregate<Account>(messages);

        Assert.Equal(eventWrappers.Last().StreamId, aggregate.Id);
        Assert.Equal(eventWrappers.Last().SequenceId, aggregate.SequenceId);
        Assert.Equal(events.Sum(x => x.Amount), aggregate.Payload.Amount);
        Assert.True(aggregate.Exists());
    }

    [Fact]
    public void TestAggregateFromSnapshot()
    {
        var state = new Account { Id = _streamId, Amount = 100 };
        var snapshotWrapper = new Snapshot<Account>(state.Id, 1, state, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        var aggregate = new Aggregate<Account>(snapshotWrapper);

        Assert.Equal(snapshotWrapper.Id, aggregate.Id);
        Assert.Equal(snapshotWrapper.SequenceId, aggregate.SequenceId);
        Assert.Equal(snapshotWrapper.CreatedDate, aggregate.CreatedDate);
        Assert.Equal(snapshotWrapper.UpdatedDate, aggregate.UpdatedDate);
        Assert.Equal(state.Amount, aggregate.Payload.Amount);
        Assert.True(aggregate.Exists());
    }


    [Fact]
    public void TestAggregateFromOnlyStreamId()
    {
        var aggregate = new Aggregate<Account>(_streamId);

        Assert.Equal(_streamId, aggregate.Id);
        Assert.Equal(0, aggregate.SequenceId);
        Assert.NotEqual(default, aggregate.CreatedDate);
        Assert.Equal(aggregate.CreatedDate, aggregate.UpdatedDate);
        Assert.True(!aggregate.Exists());
    }

    [Fact]
    public void TestApplyEventBasicView()
    {
        // Create Aggregate and Apply
        var @event = new Event(_streamId, 1, new AccountOpened(100));
        var agg = new Aggregate<AccountView>(_streamId);
        agg.Apply(@event);

        // Assert State and Agg
        var expected = JsonSerializer.Deserialize<OpenAccount>(@event.Payload);
        Assert.Equal(_streamId, agg.Id);
        Assert.Equal(_streamId, agg.Payload.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(expected.Amount, agg.Payload.Amount);

        // Assert Event
        Assert.Single(agg.Events);
    }

    [Fact]
    public void TestApplyEventAdvancedView()
    {
        // Create Aggregate and Apply
        var @event = new Event(_streamId, 1, new AccountOpened(100));
        var agg = new Aggregate<SearchAccountView>(_streamId);
        agg.Apply(@event);

        // Assert State and Agg
        var expected = JsonSerializer.Deserialize<OpenAccount>(@event.Payload);
        Assert.Equal(_streamId, agg.Id);
        Assert.Equal(_streamId, agg.Payload.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(expected.Amount, agg.Payload.Account.Amount);

        // Assert Event
        Assert.Single(agg.Events);
    }

    [Fact]
    public void TestHandleCommand()
    {
        // Create Aggregate and Apply
        var command = new Command(_streamId, new ChangeUserName("Bob"));
        var agg = new Aggregate<User>(_streamId);
        agg.Handle(command);

        // Assert State and Agg
        var expected = JsonSerializer.Deserialize<ChangeUserName>(command.Payload);
        Assert.Equal(_streamId, agg.Id);
        Assert.Equal(_streamId, agg.Payload.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(expected.Name, agg.Payload.Name);

        // Assert Event
        var @event = agg.Events.First();
        var actual = JsonSerializer.Deserialize<AccountOpened>(@event.Payload);
        Assert.Equal(_streamId, @event.StreamId);
        Assert.Equal(1, @event.SequenceId);
        Assert.Equal(actual.Amount, actual!.Amount);
    }

    [Fact]
    public void TestHandleRequestResponse()
    {
        // Create Aggregate and Apply
        var request = new Request(_streamId, new OpenAccount(100));
        var agg = new Aggregate<Account>(_streamId);
        agg.Handle(request);

        // Assert State and Agg
        var expected = JsonSerializer.Deserialize<OpenAccount>(request.Payload);
        Assert.Equal(_streamId, agg.Id);
        Assert.Equal(_streamId, agg.Payload.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(expected.Amount, agg.Payload.Amount);

        // Assert Event
        var @event = agg.Events.First();
        var actual = JsonSerializer.Deserialize<AccountOpened>(@event.Payload);
        Assert.Equal(_streamId, @event.StreamId);
        Assert.Equal(1, @event.SequenceId);
        Assert.Equal(actual.Amount, actual!.Amount);

        // Assert Results
        var result = JsonSerializer.Deserialize<AccountResponse>(agg.Responses.First().Payload);
        Assert.Equal(HttpStatusCode.OK, result!.StatusCode);
    }

    [Fact]
    public void TestHandleRequestResponseFailedResult()
    {
        // Create Aggregate and Apply
        var request = new Request(_streamId, new Withdrawal(100));
        var agg = new Aggregate<Account>(_streamId);
        agg.Handle(request);

        // Assert State and Agg
        Assert.Equal(_streamId, agg.Id);
        Assert.Equal(_streamId, agg.Payload.Id);
        Assert.Equal(0, agg.SequenceId);
        Assert.Equal(0, agg.Payload.Amount);

        // Assert Event
        Assert.Empty(agg.Events);

        // Assert Results
        var result = agg.Responses.First();
        var actual = JsonSerializer.Deserialize<AccountResponse>(result.Payload);
        Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        Assert.Equal(AccountConstants.WithdrawalDenied, actual.Error);
    }

    [Fact]
    public void TestHandleRequestResponseAggregateRoot()
    {
        // Create Aggregate and Apply
        var request = new Request(_streamId, new OpenAccount(100));
        var agg = new Aggregate<BankAccount>(_streamId);
        agg.Handle(request);

        // Assert State and Agg
        var expected = JsonSerializer.Deserialize<OpenAccount>(request.Payload);
        Assert.Equal(_streamId, agg.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(_streamId, agg.Payload.Account.Id);
        Assert.Equal(expected.Amount, agg.Payload.Account.Amount);

        // Assert Event
        var @event = agg.Events.First();
        var actual = JsonSerializer.Deserialize<AccountOpened>(@event.Payload);
        Assert.Equal(_streamId, @event.StreamId);
        Assert.Equal(1, @event.SequenceId);
        Assert.Equal(actual.Amount, actual!.Amount);
    }
}
