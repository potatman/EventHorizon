using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Benchmark.Models;
using Insperex.EventHorizon.EventStreaming.Benchmark.Singletons;
using Insperex.EventHorizon.EventStreaming.Readers;
using NBench;

namespace Insperex.EventHorizon.EventStreaming.Benchmark.Benchmarks;

public class PulsarReaderBenchmark
{
    private Counter _counter;
    private Reader<Event> _reader;

    [PerfSetup]
    public void Setup(BenchmarkContext context)
    {
        var publisher = PulsarSingleton.Instance.GetPublisher<ExampleEvent>();
        var events = PulsarSingleton.Instance.FakeEvents(1000);
        publisher.PublishAsync(events).ConfigureAwait(false).GetAwaiter().GetResult();

        // Setup
        _reader = PulsarSingleton.Instance.GetReader<ExampleEvent>();
        _counter = context.GetCounter("TestCounter");
    }

    // [PerfCleanup]
    // public void Cleanup()
    // {
    //     _reader.Dispose();
    // }

    [PerfBenchmark(Description = "Test Pulsar Reader throughput",
        NumberOfIterations = 3, RunMode = RunMode.Throughput, RunTimeMilliseconds = 1000, TestMode = TestMode.Test)]
    [CounterThroughputAssertion("TestCounter", MustBe.GreaterThan, 1.0d)]
    public void BenchmarkBulk()
    {
        _reader.GetNextAsync(1000).ConfigureAwait(false).GetAwaiter().GetResult();
        _counter.Increment();
    }
}
