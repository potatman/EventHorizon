using Insperex.EventHorizon.EventStreaming.Test.Integration.Base;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Pulsar;

public class PulsarSingleTopicConsumerIntegrationTest : BaseSingleTopicConsumerIntegrationTest
{
    public PulsarSingleTopicConsumerIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetPulsarHost(outputHelper).Services)
    {
    }
}
