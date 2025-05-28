using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Handlers;
using EventHorizon.EventSourcing.Samples.Models.Actions;
using EventHorizon.EventSourcing.Samples.Models.Snapshots;
using EventHorizon.EventStore.ElasticSearch.Attributes;

namespace EventHorizon.EventSourcing.Samples.Models.View;

[ViewStore("test_bank_view_account")]
[Stream(typeof(Account))]
[ElasticIndex(Refresh = "true", RefreshIntervalMs = 200, MaxResultWindow = 5000000)]
public class AccountView : IState,
    IApplyEvent<AccountOpened>,
    IApplyEvent<AccountDebited>,
    IApplyEvent<AccountCredited>
{
    public string Id { get; set; }
    public int Amount { get; set; }

    #region Applys

    public void Apply(AccountDebited @event) => Amount -= @event.Amount;
    public void Apply(AccountCredited @event) => Amount += @event.Amount;
    public void Apply(AccountOpened @event) => Amount = @event.Amount;

    #endregion
}
