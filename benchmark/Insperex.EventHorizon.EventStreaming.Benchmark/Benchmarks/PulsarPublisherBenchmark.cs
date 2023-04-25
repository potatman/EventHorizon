using System.Linq;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Benchmark.Models;
using Insperex.EventHorizon.EventStreaming.Benchmark.Singletons;
using Insperex.EventHorizon.EventStreaming.Publishers;
using NBench;

namespace Insperex.EventHorizon.EventStreaming.Benchmark.Benchmarks;

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
    public void Benchmark()
    {
        _publisher.PublishAsync(_bulkList.ToArray()).Wait();
        _counter.Increment();
    }
}
