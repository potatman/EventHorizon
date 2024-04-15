using System.Linq;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Benchmark.Models;
using EventHorizon.EventStreaming.Benchmark.Singletons;
using EventHorizon.EventStreaming.Publishers;
using NBench;

namespace EventHorizon.EventStreaming.Benchmark.Benchmarks;

public class PulsarPublisherBenchmark
{
    private Counter _counter;
    private Publisher<Event> _publisher;
    private Event[] _bulkList;

    [PerfSetup]
    public void Setup(BenchmarkContext context)
    {
        _publisher = PulsarSingleton.Instance.GetPublisher<ExampleEvent>();
        _bulkList = PulsarSingleton.Instance.FakeEvents(1000);
        _counter = context.GetCounter("TestCounter");
    }

    // [PerfCleanup]
    // public void Cleanup()
    // {
    //     _publisher.Dispose();
    //     _publisher = null;
    // }

    [PerfBenchmark(Description = "Test Pulsar Publisher throughput",
        NumberOfIterations = 3, RunMode = RunMode.Throughput, RunTimeMilliseconds = 1000, TestMode = TestMode.Test)]
    [CounterThroughputAssertion("TestCounter", MustBe.GreaterThan, 15.0d)]
    public async void Benchmark()
    {
        await _publisher.PublishAsync(_bulkList.ToArray());
        _counter.Increment();
    }
}
