using Insperex.EventHorizon.EventStore.Test.Integration.Base;
using Insperex.EventHorizon.EventStore.Test.Util;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStore.Test.Integration.MongoDb;

[Trait("Category", "Integration")]
public class MongoDbCrudStoreIntegrationTest : BaseCrudStoreIntegrationTest
{
    public MongoDbCrudStoreIntegrationTest(ITestOutputHelper outputHelper) : 
        base(outputHelper, HostTestUtil.GetMongoDbHost(outputHelper).Services)
    {
    }
}