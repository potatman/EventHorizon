using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventSourcing.Samples.Models.Snapshots;
using EventHorizon.EventStore.ElasticSearch.Attributes;

namespace EventHorizon.EventSourcing.Samples.Models.View;

[Store("test_view_search_account")]
[ElasticIndex(Refresh = "true", RefreshIntervalMs = 30000, MaxResultWindow = 5000000)]
public class SearchAccountView : IState
{
    public string Id { get; set; }
    public User User { get; set; }
    public AccountView Account { get; set; }
}
