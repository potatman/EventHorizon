using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Benchmark.Extensions;
using Insperex.EventHorizon.EventStreaming.Benchmark.Singletons;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
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
    private const int IterationsPerScenario = 3;

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

    [PerfCleanup]
    public void Cleanup()
    {
        _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Feed1PriceChanged)).Wait();

        var topicAdmin = PulsarSingleton.Instance.GetTopicAdmin();
        topicAdmin.DeleteTopicAsync(
            $"persistent://test_pricing/Event/subscription__Insperex.EventHorizon.EventStreaming.Benchmark-FailBenchmark_{UniqueTestId}__streamFailureState",
            CancellationToken.None).Wait();
    }

    [PerfBenchmark(Description = "Pulsar consumer with no failures (basic)", Skip = "Not run by default",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_no_failures_basic(BenchmarkContext context)
    {
        RunFailureScenario_with_no_failures(context, false);
    }

    [PerfBenchmark(Description = "Pulsar consumer with no failures (advanced)", Skip = "Not run by default",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_no_failures_advanced(BenchmarkContext context)
    {
        RunFailureScenario_with_no_failures(context, true);
    }

    [PerfBenchmark(Description = "Pulsar consumer with very light level of failures (basic)", Skip = "Not run by default",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_very_light_failures_basic(BenchmarkContext context)
    {
        RunFailureScenario_with_very_light_failures(context, false);
    }

    [PerfBenchmark(Description = "Pulsar consumer with very light level of failures (advanced)", Skip = "Not run by default",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_very_light_failures_advanced(BenchmarkContext context)
    {
        RunFailureScenario_with_very_light_failures(context, true);
    }

    [PerfBenchmark(Description = "Pulsar consumer with light level of failures (basic)", Skip = "Not run by default",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_light_failures_basic(BenchmarkContext context)
    {
        RunFailureScenario_with_light_failures(context, false);
    }

    [PerfBenchmark(Description = "Pulsar consumer with light level of failures (advanced)", Skip = "Not run by default",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_light_failures_advanced(BenchmarkContext context)
    {
        RunFailureScenario_with_light_failures(context, true);
    }

    [PerfBenchmark(Description = "Pulsar consumer with moderate level of failures (basic)", Skip = "Not run by default",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement,
        SkipWarmups = true)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_moderate_failures_basic(BenchmarkContext context)
    {
        RunFailureScenario_with_moderate_failures(context, false);
    }

    [PerfBenchmark(Description = "Pulsar consumer with moderate level of failures (advanced)", Skip = "Not run by default",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement,
        SkipWarmups = true)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_moderate_failures_advanced(BenchmarkContext context)
    {
        RunFailureScenario_with_moderate_failures(context, true);
    }

    [PerfBenchmark(Description = "Pulsar consumer with heavy level of failures (basic)",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement,
        SkipWarmups = true)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_heavy_failures_basic(BenchmarkContext context)
    {
        RunFailureScenario_with_heavy_failures(context, false);
    }

    [PerfBenchmark(Description = "Pulsar consumer with heavy level of failures (advanced)",
        NumberOfIterations = IterationsPerScenario, RunMode = RunMode.Iterations, TestMode = TestMode.Measurement,
        SkipWarmups = true)]
    [TimingMeasurement]
    [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
    public void Single_consumer_with_heavy_failures_advanced(BenchmarkContext context)
    {
        RunFailureScenario_with_heavy_failures(context, true);
    }

    private void RunFailureScenario_with_no_failures(BenchmarkContext context, bool useAdvancedFailureHandling)
    {
        var handler = new ListStreamConsumer<Event>();

        RunFailureScenario(context, handler, useAdvancedFailureHandling,
                () => InputEventCount <= handler.List.Count)
            .GetAwaiter().GetResult();
    }

    private void RunFailureScenario_with_very_light_failures(BenchmarkContext context, bool useAdvancedFailureHandling)
    {
        var handler = new PartialNackListStreamConsumer(context.Trace.AsXunitOutput(),
            0.005, 1, 1, 50, false);

        RunFailureScenario(context, handler, useAdvancedFailureHandling,
                () => InputEventCount <= handler.List.Count)
            .GetAwaiter().GetResult();

        handler.Report();
    }

    private void RunFailureScenario_with_light_failures(BenchmarkContext context, bool useAdvancedFailureHandling)
    {
        var handler = new PartialNackListStreamConsumer(context.Trace.AsXunitOutput(),
            0.03, 3, 2, 100, false);

        RunFailureScenario(context, handler, useAdvancedFailureHandling,
                () => InputEventCount <= handler.List.Count)
            .GetAwaiter().GetResult();

        handler.Report();
    }

    private void RunFailureScenario_with_moderate_failures(BenchmarkContext context, bool useAdvancedFailureHandling)
    {
        var handler = new PartialNackListStreamConsumer(context.Trace.AsXunitOutput(),
            0.1, 8, 4, 1000, false);

        RunFailureScenario(context, handler, useAdvancedFailureHandling,
                () => InputEventCount <= handler.List.Count)
            .GetAwaiter().GetResult();

        handler.Report();
    }

    private void RunFailureScenario_with_heavy_failures(BenchmarkContext context, bool useAdvancedFailureHandling)
    {
        var handler = new PartialNackListStreamConsumer(context.Trace.AsXunitOutput(),
            0.6, 20, 6, 2000, false);

        RunFailureScenario(context, handler, useAdvancedFailureHandling,
                () => InputEventCount <= handler.List.Count)
            .GetAwaiter().GetResult();

        handler.Report();
    }

    private async Task RunFailureScenario(BenchmarkContext context, IStreamConsumer<Event> handler,
        bool useAdvancedFailureHandling, Func<bool> endCondition)
    {
        // Consume
        var builder = _streamingClient.CreateSubscription<Event>()
            .SubscriptionName($"FailBenchmark_{UniqueTestId}")
            .SubscriptionType(SubscriptionType.KeyShared)
            .AddStream<Feed1PriceChanged>()
            .BatchSize(BatchSize)
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
            });

        if (useAdvancedFailureHandling)
        {
            builder
                .GuaranteeMessageOrderOnFailure(true)
                .ExponentialBackoff(b => b
                    .StartAt(TimeSpan.FromMilliseconds(10))
                    .Max(TimeSpan.FromSeconds(8)));
        }
        else
        {
            builder
                .FailedMessageRedeliveryDelay(TimeSpan.FromMilliseconds(5));
        }

        await using var subscription = await builder
            .Build()
            .StartAsync();

        // Wait for List
        await WaitUtil.WaitForTrue(endCondition, TimeSpan.FromMinutes(10));
    }
}
