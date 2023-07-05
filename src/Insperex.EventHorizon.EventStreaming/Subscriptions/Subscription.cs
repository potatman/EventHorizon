using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Extensions;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class Subscription<T> : IAsyncDisposable where T : class, ITopicMessage, new()
{
    private readonly SubscriptionConfig<T> _config;
    private readonly ILogger<Subscription<T>> _logger;
    private readonly ITopicConsumer<T> _consumer;
    private bool _disposed;
    private bool _running;
    private bool _stopped;

    public Subscription(IStreamFactory factory, SubscriptionConfig<T> config, ILogger<Subscription<T>> logger)
    {
        _config = config;
        _logger = logger;
        _consumer = factory.CreateConsumer(_config);
    }

    public Task<Subscription<T>> StartAsync()
    {
        if (_running) return Task.FromResult(this);
        _running = true;
        _stopped = false;
        Task.Run(Loop);

        _logger.LogInformation("Started Subscription with config {@Config}", _config);

        return Task.FromResult(this);
    }

    public async Task<Subscription<T>> StopAsync()
    {
        if (!_running) return this;

        // Cancel
        _running = false;
        while (!_stopped)
            await Task.Delay(TimeSpan.FromMilliseconds(250));

        // Cleanup
        _logger.LogInformation("Stopped Subscription with config {@Config}", _config);

        return this;
    }

    private async void Loop()
    {
        while (_running)
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
        _stopped = true;
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
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
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

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await StopAsync();
    }
}
