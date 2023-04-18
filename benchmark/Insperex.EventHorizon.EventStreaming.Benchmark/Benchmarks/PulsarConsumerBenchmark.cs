using System.Threading;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Benchmark.Models;
using Insperex.EventHorizon.EventStreaming.Benchmark.Singletons;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using NBench;

namespace Insperex.EventHorizon.EventStreaming.Benchmark.Benchmarks;

public class PulsarConsumerBenchmark
{
    private Counter _counter;
    private ITopicConsumer<Event> _consumer;
    private Publisher<Event> _publisher;

    [PerfSetup]
    public void Setup(BenchmarkContext context)
    {
        var events = PulsarSingleton.Instance.FakeEvents(1000);
        _publisher = PulsarSingleton.Instance.GetPublisher<ExampleEvent>();
        _publisher.PublishAsync(events).ConfigureAwait(false).GetAwaiter().GetResult();

        _consumer = PulsarSingleton.Instance.GetConsumer<ExampleEvent>();
        _counter = context.GetCounter("TestCounter");
    }

    // [PerfCleanup]
    // public void Cleanup()
    // {
    //     _publisher.Dispose();
    //     _consumer.Dispose();
    //     _publisher = null;
    //     _consumer = null;
    // }
    
    [PerfBenchmark(Description = "Test Pulsar Publisher throughput", 
        NumberOfIterations = 3, RunMode = RunMode.Throughput, RunTimeMilliseconds = 1000, TestMode = TestMode.Test)]
    [CounterThroughputAssertion("TestCounter", MustBe.GreaterThan, 1.0d)]
    public void BenchmarkBulk()
    {
        _consumer.NextBatchAsync(CancellationToken.None).Wait();
        _counter.Increment();
    }
}