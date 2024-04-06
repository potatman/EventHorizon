using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Serialization.Compression.Extensions;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Publishers;

public class Publisher<TMessage> : IAsyncDisposable
    where TMessage : class, ITopicMessage
{
    private readonly PublisherConfig _config;
    private readonly ILogger<Publisher<TMessage>> _logger;
    private readonly string _typeName;
    private readonly ITopicProducer<TMessage> _producer;

    public Publisher(IStreamFactory factory, PublisherConfig config, ILogger<Publisher<TMessage>> logger)
    {
        _config = config;
        _logger = logger;
        _typeName = typeof(TMessage).Name;
        _producer = factory.CreateProducer<TMessage>(config);
    }

    public Task PublishAsync(string streamId, params object[] objs)
    {
        var wrapped = objs.Select(x => Activator.CreateInstance(typeof(TMessage), streamId, x) as TMessage).ToArray();
        return PublishAsync(wrapped);
    }

    public async Task<Publisher<TMessage>> PublishAsync(params TMessage[] messages)
    {
        // Defensive
        if (!messages.Any()) return this;

        // Compress
        if(_config.CompressionType != null)
            foreach (var message in messages)
                message.Compress(_config.CompressionType);

        // Get topic
        var sw = Stopwatch.StartNew();
        var activity = TraceConstants.ActivitySource.StartActivity();
        activity?.SetTag(TraceConstants.Tags.Count, messages.Length);
        try
        {
            await _producer.SendAsync(messages);
            _logger.LogInformation("Publisher - Sent {Count} {Type}(s) in {Duration} {Topic} ",
                messages.Length, _typeName, sw.ElapsedMilliseconds, _config.Topic);
            activity?.SetStatus(ActivityStatusCode.Ok);

            /*
            var tcs = new TaskCompletionSource<bool?>();
            messages.ToObservable()
                .Buffer(_config.BatchSize)
                .Subscribe(async x =>
                {
                    try
                    {
                        await _producer.SendAsync(x.ToArray());
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                },
                () =>
                {
                    tcs.SetResult(true);
                    _logger.LogInformation("Publisher - Sent {Type}(s) {Count} {Topic} in {Duration}",
                        _typeName, messages.Length, _config.Topic, sw.ElapsedMilliseconds);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    activity?.Dispose();
                });

            await tcs.Task;
            */
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Publisher - Failed to Send {Count} {Type} {Error}",
                messages.Length, _typeName, ex.Message);
            throw;
        }

        return this;
    }

    public async ValueTask DisposeAsync()
    {
        await _producer.DisposeAsync();
    }
}
