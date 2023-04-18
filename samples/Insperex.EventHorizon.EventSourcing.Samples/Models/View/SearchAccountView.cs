using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;

namespace Insperex.EventHorizon.EventSourcing.Samples.Models.View;

[ViewStore("test_view_search_account", nameof(SearchAccountView))]
public class SearchAccountView : IState
{
    public string Id { get; set; }
    public User User { get; set; }
    public AccountView Account { get; set; }
}