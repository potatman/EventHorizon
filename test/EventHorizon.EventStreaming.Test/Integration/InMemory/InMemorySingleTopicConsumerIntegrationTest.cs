using EventHorizon.EventStreaming.Test.Integration.Base;
using EventHorizon.EventStreaming.Test.Util;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Integration.InMemory;

public class InMemorySingleTopicConsumerIntegrationTest : BaseSingleTopicConsumerIntegrationTest
{
    public InMemorySingleTopicConsumerIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetInMemoryHost(outputHelper).Services)
    {
    }
}
