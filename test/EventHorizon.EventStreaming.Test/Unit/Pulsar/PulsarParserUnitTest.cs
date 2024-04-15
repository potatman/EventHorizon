using EventHorizon.EventStreaming.Pulsar.Utils;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public class PulsarParserUnitTest
{
    [Fact]
    public void TestParse()
    {
        var name = "persistent://foo/bar/abc";
        var topic = PulsarTopicParser.Parse(name);

        Assert.Equal("foo", topic.Tenant);
        Assert.Equal("bar", topic.Namespace);
        Assert.Equal("abc", topic.Topic);
        Assert.Equal(name, topic.ToString());
    }
}
