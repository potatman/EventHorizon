using Insperex.EventHorizon.EventStreaming.Test.Integration.Base;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.InMemory;

public class InMemoryMultiTopicConsumerIntegrationTest : BaseMultiTopicConsumerIntegrationTest
{
    public InMemoryMultiTopicConsumerIntegrationTest(ITestOutputHelper outputHelper) : 
        base(outputHelper, HostTestUtil.GetInMemoryHost(outputHelper).Services)
    {
    }
}