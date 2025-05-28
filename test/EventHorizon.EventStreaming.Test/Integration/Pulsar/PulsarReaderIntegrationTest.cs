using EventHorizon.EventStreaming.Test.Integration.Base;
using EventHorizon.EventStreaming.Test.Util;
using Xunit.Abstractions;

namespace EventHorizon.EventStreaming.Test.Integration.Pulsar;

public class PulsarReaderIntegrationTest : BaseReaderIntegrationTest
{
    public PulsarReaderIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetPulsarHost(outputHelper).Services)
    {

    }
}
