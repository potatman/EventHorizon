using System;
using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Benchmark.Extensions;
using Insperex.EventHorizon.EventStreaming.Benchmark.Singletons;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;
using Insperex.EventHorizon.EventStreaming.Test.Fakers;
using Insperex.EventHorizon.EventStreaming.Test.Shared;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using NBench;

namespace Insperex.EventHorizon.EventStreaming.Benchmark.Benchmarks;

public class PulsarFailureRecoveryBenchmarks
{
    private const int InputEventCount = 1_000;
    private const int BatchSize = 100;

    private Publisher<Event> _publisher;
    private StreamingClient _streamingClient;

    private string UniqueTestId { get; set; }

    [PerfSetup]
    public void Setup(BenchmarkContext context)
    {
        var random = new Random((int)DateTime.UtcNow.Ticks);
        UniqueTestId = $"{random.Next()}";

        _streamingClient = PulsarSingleton.StreamClient.Value;

        int sequenceId = 0;
        var events = EventStreamingFakers.Feed1PriceChangedFaker.Generate(InputEventCount)
            .Select(x => new Event(x.Id, ++sequenceId, x)).ToArray();
        _publisher = PulsarSingleton.Instance.GetPublisher<Feed1PriceChanged>();
        _publisher.PublishAsync(events).Wait();  //.GetAwaiter().GetResult();
    }

    [PerfBenchmark(Description = "Pulsar consumer with failure handling",
        NumberOfIterations = 1, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void SingleConsumerInFailureScenario(BenchmarkContext context)
    {
        var handler = new PartialNackListStreamConsumer(context.Trace.AsXunitOutput(),
            0.03, 3, 2, 100, true);

        RunFailureScenario(context, handler).GetAwaiter().GetResult();
    }

    private async Task RunFailureScenario(BenchmarkContext context, PartialNackListStreamConsumer handler)
    {
        // Consume
        await using var subscription = await _streamingClient.CreateSubscription<Event>()
            .SubscriptionName($"FailBenchmark_{UniqueTestId}")
            .SubscriptionType(SubscriptionType.KeyShared)
            .AddStream<Feed1PriceChanged>()
            .BatchSize(BatchSize)
            .GuaranteeMessageOrderOnFailure(true)
            .ExponentialBackoff(b => b
                .StartAt(TimeSpan.FromMilliseconds(10))
                .Max(TimeSpan.FromSeconds(15)))
            .OnBatch(async ctx =>
            {
                try
                {
                    await handler.OnBatch(ctx);
                }
                catch (Exception e)
                {
                    context.Trace.Error(e, e.Message);
                    throw;
                }
            })
            .Build()
            .StartAsync();

        // Wait for List
        await WaitUtil.WaitForTrue(() => InputEventCount <= handler.List.Count, TimeSpan.FromMinutes(10));

        handler.Report();
    }
}
