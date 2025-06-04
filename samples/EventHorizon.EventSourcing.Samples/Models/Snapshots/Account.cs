using System.Collections.Generic;
using System.Net;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Interfaces.Handlers;
using EventHorizon.Abstractions.Models;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventSourcing.Samples.Models.Actions;
using EventHorizon.EventStore.MongoDb.Attributes;
using EventHorizon.EventStore.MongoDb.Models;
using EventHorizon.EventStreaming.Pulsar.Attributes;
using MongoDB.Driver;

namespace EventHorizon.EventSourcing.Samples.Models.Snapshots;

[Stream("$type")]
[PulsarNamespace("test_bank", "account")]
[SnapshotStore("test_bank_snapshot_account")]
[MongoCollection(ReadPreferenceMode = ReadPreferenceMode.SecondaryPreferred,
    ReadConcernLevel = ReadConcernLevel.Majority,
    WriteConcernLevel = WriteConcernLevel.Majority)]
public class Account : IState,
    IHandleRequest<OpenAccount, AccountResponse>,
    IHandleRequest<Withdrawal, AccountResponse>,
    IHandleRequest<Deposit, AccountResponse>,
    IApplyEvent<AccountOpened>,
    IApplyEvent<AccountDebited>,
    IApplyEvent<AccountCredited>
{
    public string Id { get; set; }

    [StreamPartitionKey]
    public string BankAccount { get; set; }
    public int Amount { get; set; }

    #region Requests

    public AccountResponse Handle(OpenAccount request, AggregateContext context)
    {
        if(!context.Exists)
            context.AddEvent(new AccountOpened(request.Amount));

        return new AccountResponse();
    }

    public AccountResponse Handle(Withdrawal request, AggregateContext context)
    {
        if(Amount < request.Amount)
            return new AccountResponse(HttpStatusCode.InternalServerError, AccountConstants.WithdrawalDenied);

        if(request.Amount != 0 && Amount >= request.Amount)
            context.AddEvent(new AccountDebited(request.Amount));

        return new AccountResponse();
    }

    public AccountResponse Handle(Deposit request, AggregateContext context)
    {
        context.AddEvent(new AccountCredited(request.Amount));
        return new AccountResponse();
    }

    #endregion

    #region Applys

    public void Apply(AccountDebited @event) => Amount -= @event.Amount;
    public void Apply(AccountCredited @event) => Amount += @event.Amount;
    public void Apply(AccountOpened @event) => Amount = @event.Amount;

    #endregion
}
