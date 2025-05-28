using EventHorizon.EventStore.Test.Integration.Base;
using EventHorizon.EventStore.Test.Util;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventStore.Test.Integration.InMemory;

[Trait("Category", "Integration")]
public class InMemoryCrudStoreIntegrationTest : BaseCrudStoreIntegrationTest
{
    public InMemoryCrudStoreIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetInMemoryHost(outputHelper).Services)
    {
    }
}
