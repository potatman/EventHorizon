using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Benchmark.Models;
using EventHorizon.EventStreaming.Benchmark.Singletons;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using NBench;

namespace EventHorizon.EventStreaming.Benchmark.Benchmarks;

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
        _publisher.PublishAsync(events).GetAwaiter().GetResult();

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
    public async Task BenchmarkBulk()
    {
        await _consumer.NextBatchAsync(CancellationToken.None);
        _counter.Increment();
    }
}
