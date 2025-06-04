using EventHorizon.EventStreaming.Test.Integration.Base;
using EventHorizon.EventStreaming.Test.Util;
using Xunit.Abstractions;

namespace EventHorizon.EventStreaming.Test.Integration.InMemory;

public class InMemorySingleTopicConsumerIntegrationTest : BaseSingleTopicConsumerIntegrationTest
{
    public InMemorySingleTopicConsumerIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetInMemoryHost(outputHelper).Services)
    {
    }
}
