using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Benchmark.Models;
using EventHorizon.EventStreaming.Benchmark.Singletons;
using EventHorizon.EventStreaming.Readers;
using NBench;

namespace EventHorizon.EventStreaming.Benchmark.Benchmarks;

public class PulsarReaderBenchmark
{
    private Counter _counter;
    private Reader<Event> _reader;

    [PerfSetup]
    public void Setup(BenchmarkContext context)
    {
        var publisher = PulsarSingleton.Instance.GetPublisher<ExampleEvent>();
        var events = PulsarSingleton.Instance.FakeEvents(1000);
        publisher.PublishAsync(events).GetAwaiter().GetResult();

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
        _reader.GetNextAsync(1000).GetAwaiter().GetResult();
        _counter.Increment();
    }
}
