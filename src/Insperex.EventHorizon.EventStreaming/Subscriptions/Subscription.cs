using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Serialization.Compression.Extensions;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class Subscription<TMessage> : IAsyncDisposable
    where TMessage : ITopicMessage
{
    private readonly SubscriptionConfig<TMessage> _config;
    private readonly ILogger<Subscription<TMessage>> _logger;
    private readonly ITopicConsumer<TMessage> _consumer;
    private readonly ITopicAdmin<TMessage> _admin;
    private bool _disposed;
    private bool _running;
    private bool _stopped;

    public Subscription(IStreamFactory factory, SubscriptionConfig<TMessage> config, ILogger<Subscription<TMessage>> logger)
    {
        _config = config;
        _logger = logger;
        _admin = factory.CreateAdmin<TMessage>();
        _consumer = factory.CreateConsumer(_config);
    }

    public async Task<Subscription<TMessage>> StartAsync()
    {
        if (_running) return this;
        _running = true;
        _stopped = false;

        // Initialize
        await _consumer.InitAsync();

        _logger.LogInformation("Subscription - Started {@Config}", _config);

        // Start Loop
        Task.Run(BasicLoop);

        return this;
    }

    public async Task<Subscription<TMessage>> StopAsync()
    {
        if (!_running) return this;

        // Cancel
        _running = false;
        // while (!_stopped)
        //     await Task.Delay(TimeSpan.FromMilliseconds(250));

        // Cleanup
        _logger.LogInformation("Subscription - Stopped {@Config}", _config);

        return this;
    }

    public async Task DeleteTopicsAsync()
    {
        foreach (var topic in _config.Topics)
            await _admin.DeleteTopicAsync(topic, CancellationToken.None);
    }

    private async void BasicLoop()
    {
        while (_running)
        {
            try
            {
                var batch = await GetNextBatch();
                if (batch?.Length > 0)
                {
                    await ProcessBatch(batch);
                }
                else
                {
                    if (_config.StopAtEnd)
                        _running = false;
                    else
                        await Task.Delay(_config.NoBatchDelay);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore Cancels
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription - Unhandled Exception {Message} {Subscription}", ex.Message, _config.SubscriptionName);
            }
        }
        _stopped = true;
    }

    public async Task<MessageContext<TMessage>[]> NextBatch()
    {
            var batch = await GetNextBatch();
            if (batch?.Any() == true)
                await ProcessBatch(batch);
            return batch;
    }

    public async Task<MessageContext<TMessage>[]> GetNextBatch()
    {
        using var activity = TraceConstants.ActivitySource.StartActivity();
        try
        {
            var sw = Stopwatch.StartNew();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var batch = await _consumer.NextBatchAsync(cts.Token);
            activity?.SetTag(TraceConstants.Tags.Count, batch?.Length ?? 0);
            activity?.SetStatus(ActivityStatusCode.Ok);

            if (batch?.Any() != true) return batch;

            // Decompress
            foreach (var item in batch)
                item.Data.Decompress();

            // Upgrade
            foreach (var item in batch)
                item.Data = item.Upgrade();

            _logger.LogInformation("Subscription - Loaded {Type}(s) {Count} in {Duration} {Subscription}",
                typeof(TMessage).Name, batch.Length, sw.ElapsedMilliseconds, _config.SubscriptionName);

            return batch;
        }
        catch (TaskCanceledException)
        {
            // ignore
            return Array.Empty<MessageContext<TMessage>>();
        }
        catch (Exception ex)
        {
            // ignore disposed
            if (_disposed) return Array.Empty<MessageContext<TMessage>>();

            // log error
            _logger.LogError(ex, "Subscription - Failed to load events => {Error} {Subscription}", ex.Message, _config.SubscriptionName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task ProcessBatch(MessageContext<TMessage>[] batch)
    {
        var sw = Stopwatch.StartNew();
        using var activity = TraceConstants.ActivitySource.StartActivity();
        var context = new SubscriptionContext<TMessage> { Messages = batch };
        try
        {
            if(_config.OnBatch != null)
                await _config.OnBatch.Invoke(context);

            // Auto-Ack
            var autoAcks = batch.Except(context.NackList).Except(context.AckList).ToArray();
            context.AckList.AddRange(autoAcks);

            await _consumer.FinalizeBatchAsync(context.AckList.ToArray(), context.NackList.ToArray());

            // Logging
            var min = batch.Min(x => x.TopicData.CreatedDate);
            var max = batch.Max(x => x.TopicData.CreatedDate);
            _logger.LogInformation("Subscription - Processed {Type}(s) {Count} in {Duration}, from {Start}-{End} {Subscription}",
                typeof(TMessage).Name, batch.Length, sw.ElapsedMilliseconds, min, max, _config.SubscriptionName);
            activity?.SetTag(TraceConstants.Tags.Count, batch.Length);
            activity?.SetTag(TraceConstants.Tags.Start, min);
            activity?.SetTag(TraceConstants.Tags.End, max);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Subscription - Failed to process {Message} {Subscription}", ex.Message, _config.SubscriptionName);
            await _consumer.FinalizeBatchAsync(Array.Empty<MessageContext<TMessage>>(), batch);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await StopAsync();
    }
}
