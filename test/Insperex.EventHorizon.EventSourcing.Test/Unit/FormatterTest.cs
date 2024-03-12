using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.InMemory;
using Insperex.EventHorizon.EventStreaming.Pulsar;
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
            var topic = _defaultFormatter.GetTopic<Event>(typeof(ExampleFormatter));
            Assert.Equal("Insperex.EventHorizon.EventSourcing.Test-Event-ExampleFormatter", topic);
            var topic2 = _defaultFormatter.GetTopic<Event>(typeof(AttributeFormatter));
            Assert.Equal("TestTopic", topic2);
        }

        [Fact]
        public void TestDefaultTopicWithPostfix()
        {
            var topic = _testFormatter.GetTopic<Event>(typeof(ExampleFormatter));
            Assert.Equal("Insperex.EventHorizon.EventSourcing.Test-Event-ExampleFormatter-ABC", topic);
            var topic2 = _testFormatter.GetTopic<Event>(typeof(AttributeFormatter));
            Assert.Equal("TestTopic-ABC", topic2);
        }

        [Fact]
        public void TestPulsarTopic()
        {
            var topic = _pulsarFormatter.GetTopic<Event>(typeof(ExampleFormatter));
            Assert.Equal("persistent://Insperex.EventHorizon.EventSourcing.Test/ExampleFormatter-Event/ExampleFormatter-Event", topic);
            var topic2 = _pulsarFormatter.GetTopic<Event>(typeof(AttributeFormatter));
            Assert.Equal("TestTopic", topic2);
        }

        [Fact]
        public void TestInMemoryTopic()
        {
            var topic = _inMemoryFormatter.GetTopic<Event>(typeof(ExampleFormatter));
            Assert.Equal("in-memory://Insperex.EventHorizon.EventSourcing.Test/ExampleFormatter/Event", topic);
            var topic2 = _inMemoryFormatter.GetTopic<Event>(typeof(AttributeFormatter));
            Assert.Equal("TestTopic", topic2);
        }

        [Fact]
        public void TestDefaultDatabase()
        {
            var topic = _defaultFormatter.GetDatabase<Event>(typeof(ExampleFormatter));
            Assert.Equal("insperex_event_example_formatter", topic);
            var topic2 = _defaultFormatter.GetDatabase<Event>(typeof(AttributeFormatter));
            Assert.Equal("test_database", topic2);
        }

        [Fact]
        public void TestDefaultDatabaseWithPostfix()
        {
            var topic = _testFormatter.GetDatabase<Event>(typeof(ExampleFormatter));
            Assert.Equal("insperex_event_example_formatter_abc", topic);
            var topic2 = _testFormatter.GetDatabase<Event>(typeof(AttributeFormatter));
            Assert.Equal("test_database_abc", topic2);
        }

        public class ExampleFormatter { }

        [Stream("TestTopic")]
        [Store("TestDatabase")]
        public class AttributeFormatter { }

    }
}
