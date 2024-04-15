using System.Linq;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Serialization.Compression;
using EventHorizon.Abstractions.Serialization.Compression.Extensions;
using Xunit;

namespace EventHorizon.EventSourcing.Test.Unit
{
    public class CompressionUnitTest
    {
        [Fact]
        public void TestCompression()
        {
            // Act
            var @event = new Event("123", new ExampleEvent { Name = "Name" });
            @event.Compress(Compression.Gzip);

            // Assert
            Assert.Null(@event.Payload);
            Assert.NotNull(@event.Compression);
            Assert.NotNull(@event.Data);
        }


        [Fact]
        public void TestDecompress()
        {
            // Act
            var @event = new Event("123", new ExampleEvent { Name = "Name" });
            @event.Compress(Compression.Gzip);
            @event.Decompress();

            // Assert
            var types = new [] { typeof(ExampleEvent) };
            var entity = @event.GetPayload(types.ToDictionary(x => x.Name)) as ExampleEvent;
            Assert.NotNull(@event.Payload);
            Assert.Null(@event.Compression);
            Assert.Null(@event.Data);
            Assert.Equal("Name", entity.Name);
        }

        public class ExampleEvent : IEvent
        {
            public string Name { get; set; }
        }
    }
}
