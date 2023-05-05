using Elasticsearch.Net;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Handlers;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;

namespace Insperex.EventHorizon.EventSourcing.Samples.Models.View;

[ViewStore("test_bank_view_account")]
[Stream(typeof(Account))]
[ElasticIndex(Refresh = Refresh.True, RefreshIntervalMs = 200, MaxResultWindow = 5000000)]
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
