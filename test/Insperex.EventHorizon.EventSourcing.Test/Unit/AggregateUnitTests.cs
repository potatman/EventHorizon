using System;
using System.Linq;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.Extensions;
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
        var eventWrappers = events.Select(x => new Event(_streamId, 1, x)).ToArray();
        var messages = eventWrappers.Select(x => new MessageContext<Event>
            { Data = x, TopicData = new TopicData(Guid.NewGuid().ToString(), "topic", DateTime.UtcNow) }).ToArray();
        var aggregate = new Aggregate<Account>(messages);
        
        Assert.Equal(eventWrappers.Last().StreamId, aggregate.Id);
        Assert.Equal(eventWrappers.Last().SequenceId, aggregate.SequenceId);
        Assert.Equal(events.Sum(x => x.Amount), aggregate.State.Amount);
    }
    
    [Fact]
    public void TestAggregateFromSnapshot()
    {
        var state = new Account { Id = _streamId, Amount = 100 };
        var snapshotWrapper = new Snapshot<Account>(state.Id, 1, state, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        var aggregate = new Aggregate<Account>(snapshotWrapper);
        
        Assert.Equal(snapshotWrapper.Id, aggregate.Id);
        Assert.Equal(snapshotWrapper.SequenceId+1, aggregate.SequenceId);
        Assert.Equal(snapshotWrapper.CreatedDate, aggregate.CreatedDate);
        Assert.Equal(snapshotWrapper.UpdatedDate, aggregate.UpdatedDate);
        Assert.Equal(state.Amount, aggregate.State.Amount);
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
        Assert.Equal(_streamId, agg.State.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(expected.Amount, agg.State.Amount);

        // Assert Event
        Assert.Single(agg.GetEvents());
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
        Assert.Equal(_streamId, agg.State.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(expected.Amount, agg.State.Account.Amount);

        // Assert Event
        Assert.Single(agg.GetEvents());
    }
    
    [Fact]
    public void TestHandleCommand()
    {
        // Create Aggregate and Apply
        var command = new Command(_streamId, new CreateUser("Bob")).Upgrade();
        var agg = new Aggregate<User>(_streamId);
        agg.Handle(command);
        
        // Assert State and Agg
        var expected = JsonSerializer.Deserialize<CreateUser>(command.Payload);
        Assert.Equal(_streamId, agg.Id);
        Assert.Equal(_streamId, agg.State.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(expected.Name, agg.State.Name);
    
        // Assert Event
        var @event = agg.GetEvents().First();
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
        Assert.Equal(_streamId, agg.State.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(expected.Amount, agg.State.Amount);

        // Assert Event
        var @event = agg.GetEvents().First();
        var actual = JsonSerializer.Deserialize<AccountOpened>(@event.Payload);
        Assert.Equal(_streamId, @event.StreamId);
        Assert.Equal(1, @event.SequenceId);
        Assert.Equal(actual.Amount, actual!.Amount);

        // Assert Results
        var result = JsonSerializer.Deserialize<AccountResponse>(agg.GetResponses().First().Payload);
        Assert.Equal(AccountResponseStatus.Success, result!.Status);
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
        Assert.Equal(_streamId, agg.State.Id);
        Assert.Equal(1, agg.SequenceId);
        Assert.Equal(0, agg.State.Amount);

        // Assert Event
        Assert.Empty(agg.GetEvents());

        // Assert Results
        var result = agg.GetResponses().First();
        var actual = JsonSerializer.Deserialize<AccountResponse>(result.Payload);
        Assert.Equal(AccountResponseStatus.WithdrawalDenied, actual!.Status);
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
        Assert.Equal(_streamId, agg.State.Account.Id);
        Assert.Equal(expected.Amount, agg.State.Account.Amount);

        // Assert Event
        var @event = agg.GetEvents().First();
        var actual = JsonSerializer.Deserialize<AccountOpened>(@event.Payload);
        Assert.Equal(_streamId, @event.StreamId);
        Assert.Equal(1, @event.SequenceId);
        Assert.Equal(actual.Amount, actual!.Amount);
    }
}