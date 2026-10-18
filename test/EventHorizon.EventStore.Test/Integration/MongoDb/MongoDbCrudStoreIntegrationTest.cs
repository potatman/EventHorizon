using EventHorizon.EventStore.Test.Integration.Base;
using EventHorizon.EventStore.Test.Util;
using Xunit;

namespace EventHorizon.EventStore.Test.Integration.MongoDb;

[Trait("Category", "Integration")]
public class MongoDbCrudStoreIntegrationTest : BaseCrudStoreIntegrationTest
{
    public MongoDbCrudStoreIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetMongoDbHost(outputHelper).Services)
    {
    }
}
