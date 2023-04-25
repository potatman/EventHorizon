using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Extensions;
using Insperex.EventHorizon.EventStreaming.Interfaces;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class Subscription<T> : IDisposable where T : class, ITopicMessage, new()
{
    private readonly SubscriptionConfig<T> _config;
    private readonly ILogger<Subscription<T>> _logger;
    private readonly ITopicConsumer<T> _consumer;
    private bool _disposed;
    private bool Running { get; set; }
    private bool Stopped { get; set; }

    public Subscription(IStreamFactory factory, SubscriptionConfig<T> config, ILogger<Subscription<T>> logger)
    {
        _config = config;
        _logger = logger;
        _consumer = factory.CreateConsumer(_config);
    }

    public void Dispose()
    {
        _disposed = true;
        StopAsync().Wait();
    }

    public Task<Subscription<T>> StartAsync()
    {
        if (Running) return Task.FromResult(this);
        Running = true;
        Stopped = false;
        Task.Run(Loop);

        _logger.LogInformation("Started Subscription with config {@Config}", _config);

        return Task.FromResult(this);
    }

    public Task<Subscription<T>> StopAsync()
    {
        if (!Running) return Task.FromResult(this);

        // Cancel
        Running = false;
        // while (!Stopped)
        //     await Task.Delay(TimeSpan.FromSeconds(1));

        // Cleanup
        _consumer.Dispose();
        _logger.LogInformation("Stopped Subscription with config {@Config}", _config);

        return Task.FromResult(this);
    }

    private async void Loop()
    {
        while (Running)
        {
            try
            {
                await RunIteration();
            }
            catch (TaskCanceledException)
            {
                // Ignore Cancels
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled Exception {Message}", ex.Message);
            }
        }
        Stopped = true;
    }

    private async Task RunIteration()
    {
        var batch = await LoadEvents();
        if (batch?.Any() != true)
        {
            await Task.Delay(_config.NoBatchDelay);
            return;
        }

        await OnEvents(batch);
    }

    private async Task<MessageContext<T>[]> LoadEvents()
    {
        using var activity = TraceConstants.ActivitySource.StartActivity();
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var batch = await _consumer.NextBatchAsync(cts.Token);
            activity?.SetTag(TraceConstants.Tags.Count, batch?.Length ?? 0);
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Upgrade Actions
            if(batch?.Any() == true)
                foreach (var item in batch)
                    item.Data = item.Data.Upgrade();

            return batch;
        }
        catch (TaskCanceledException)
        {
            // ignore
            return Array.Empty<MessageContext<T>>();
        }
        catch (Exception ex)
        {
            // ignore disposed
            if (!_disposed) return Array.Empty<MessageContext<T>>();

            // log error
            _logger.LogError(ex, "Failed to load events => {Error}", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task OnEvents(MessageContext<T>[] batch)
    {
        using var activity = TraceConstants.ActivitySource.StartActivity();
        var context = new SubscriptionContext<T> { Messages = batch };
        try
        {
            await _config.OnBatch(context);

            // Ack/Nack Lists
            await _consumer.AckAsync(context.AckList.ToArray());
            await _consumer.NackAsync(context.NackList.ToArray());

            // Auto-Ack
            var list = batch
                .Where(x => !context.NackList.Contains(x))
                .Where(x => !context.AckList.Contains(x))
                .ToArray();
            await _consumer.AckAsync(list.ToArray());

            // Logging
            var min = batch.Min(x => x.TopicData.CreatedDate);
            var max = batch.Max(x => x.TopicData.CreatedDate);
            _logger.LogInformation("Processed {Type}(s) {Count}, from {Start}-{End}",
                typeof(T).Name, batch.Length, min, max);
            activity?.SetTag(TraceConstants.Tags.Count, batch.Length);
            activity?.SetTag(TraceConstants.Tags.Start, min);
            activity?.SetTag(TraceConstants.Tags.End, max);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to process {Message}", ex.Message);
            await _consumer.NackAsync(batch);
        }
    }
}
