using EventHorizon.EventStreaming.Test.Integration.Base;
using EventHorizon.EventStreaming.Test.Util;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Integration.InMemory;

public class InMemoryReaderIntegrationTest : BaseReaderIntegrationTest
{
    public InMemoryReaderIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetInMemoryHost(outputHelper).Services)
    {

    }
}
