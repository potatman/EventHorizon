using Insperex.EventHorizon.EventStreaming.Test.Integration.Base;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Pulsar;

public class PulsarMultiTopicConsumerIntegrationTest : BaseMultiTopicConsumerIntegrationTest
{
    public PulsarMultiTopicConsumerIntegrationTest(ITestOutputHelper outputHelper) : 
        base(outputHelper, HostTestUtil.GetPulsarHost(outputHelper).Services)
    {
    }
}