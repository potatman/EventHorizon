using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.InMemory;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Xunit;

namespace Insperex.EventHorizon.EventSourcing.Test.Unit
{
    public class FormatterTest
    {
        private readonly Formatter _defaultFormatter;
        private readonly Formatter _testFormatter;
        private readonly Formatter _pulsarFormatter;
        private readonly Formatter _inMemoryFormatter;

        public FormatterTest()
        {
            _defaultFormatter = new Formatter(new AttributeUtil(), new DefaultTopicFormatter(), new DefaultDatabaseFormatter(), new DefaultFormatterPostfix());
            _testFormatter = new Formatter(new AttributeUtil(), new DefaultTopicFormatter(), new DefaultDatabaseFormatter(), new TestFormatterPostfix("ABC"));
            _inMemoryFormatter = new Formatter(new AttributeUtil(), new InMemoryTopicFormatter(), new DefaultDatabaseFormatter(), new DefaultFormatterPostfix());
            _pulsarFormatter = new Formatter(new AttributeUtil(), new PulsarTopicFormatter(), new DefaultDatabaseFormatter(), new DefaultFormatterPostfix());
        }

        [Fact]
        public void TestDefaultTopic()
        {
            var topic = _defaultFormatter.GetTopic<Event>(typeof(FormatterTest));
            Assert.Equal("Insperex.EventHorizon.EventSourcing.Test-Event-FormatterTest", topic);
        }

        [Fact]
        public void TestDefaultTopicWithPostfix()
        {
            var topic = _testFormatter.GetTopic<Event>(typeof(FormatterTest));
            Assert.Equal("Insperex.EventHorizon.EventSourcing.Test-Event-FormatterTest-ABC", topic);
        }

        [Fact]
        public void TestPulsarTopic()
        {
            var topic = _pulsarFormatter.GetTopic<Event>(typeof(FormatterTest));
            Assert.Equal("persistent://Insperex.EventHorizon.EventSourcing.Test/FormatterTest/Event", topic);
        }

        [Fact]
        public void TestInMemoryTopic()
        {
            var topic = _inMemoryFormatter.GetTopic<Event>(typeof(FormatterTest));
            Assert.Equal("in-memory://Insperex.EventHorizon.EventSourcing.Test/FormatterTest/Event", topic);
        }

        [Fact]
        public void TestDefaultDatabase()
        {
            var topic = _defaultFormatter.GetDatabase<Event>(typeof(FormatterTest));
            Assert.Equal("insperex_event_formatter_test", topic);
        }

        [Fact]
        public void TestDefaultDatabaseWithPostfix()
        {
            var topic = _testFormatter.GetDatabase<Event>(typeof(FormatterTest));
            Assert.Equal("insperex_event_formatter_test_abc", topic);
        }
    }
}
