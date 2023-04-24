using Elasticsearch.Net;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Interfaces.State;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;

namespace Insperex.EventHorizon.EventSourcing.Samples.Models.View;

[ViewStore("test_view_bank_account", nameof(AccountView))]
[EventStream("test_event_bank_account", nameof(Account))]
[ElasticConfig(Refresh = Refresh.True, RefreshIntervalMs = 200, MaxResultWindow = 5000000)]
public class AccountView : IState, 
    IApplyEvent<AccountOpened>,
    IApplyEvent<AccountDebited>,
    IApplyEvent<AccountCredited>
{
    public string Id { get; set; }
    public int Amount { get; set; }

    #region Applys
    
    public void Apply(AccountDebited payload) => Amount -= payload.Amount;
    public void Apply(AccountCredited payload) => Amount += payload.Amount;
    public void Apply(AccountOpened payload) => Amount = payload.Amount;
    
    #endregion
}