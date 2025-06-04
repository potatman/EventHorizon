using EventHorizon.EventStore.Test.Integration.Base;
using EventHorizon.EventStore.Test.Util;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventStore.Test.Integration.ElasticSearch;

[Trait("Category", "Integration")]
public class ElasticCrudStoreIntegrationTest : BaseCrudStoreIntegrationTest
{
    public ElasticCrudStoreIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetElasticHost(outputHelper).Services)
    {
    }
}
