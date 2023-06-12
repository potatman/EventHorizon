using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar;
using Insperex.EventHorizon.EventStreaming.Test.Integration.Base;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Pulsar;

public class PulsarMultiTopicConsumerIntegrationTest : BaseMultiTopicConsumerIntegrationTest
{
    public PulsarMultiTopicConsumerIntegrationTest(ITestOutputHelper outputHelper) :
        base(outputHelper, HostTestUtil.GetPulsarHost(outputHelper).Services)
    {
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();

        var streamFactory = Provider.GetRequiredService<IStreamFactory>();
        var topicAdmin = (PulsarTopicAdmin<Event>)streamFactory.CreateAdmin<Event>();
        await topicAdmin.DeleteTopicAsync(
            $"persistent://test_pricing/Event/subscription__ReSharperTestRunner-Fails_{UniqueTestId}__streamFailureState",
            CancellationToken.None);
    }
}
