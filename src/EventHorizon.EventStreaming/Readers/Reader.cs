using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.Abstractions.Serialization.Compression.Extensions;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Tracing;

namespace EventHorizon.EventStreaming.Readers;

public class Reader<TMessage> : IAsyncDisposable
    where TMessage : ITopicMessage
{
    private readonly ITopicReader<TMessage> _reader;

    public Reader(ITopicReader<TMessage> reader)
    {
        _reader = reader;
    }

    public async Task<MessageContext<TMessage>[]> GetNextAsync(int batchSize, TimeSpan? timeout = default)
    {
        using var activity = TraceConstants.ActivitySource.StartActivity();
        try
        {
            timeout ??= TimeSpan.FromSeconds(10);
            var items = await _reader.GetNextAsync(batchSize, timeout.Value).ConfigureAwait(false);
            activity?.SetTag(TraceConstants.Tags.Count, items.Length);
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Decompress
            foreach (var item in items)
                item.Data.Decompress();

            // Upgrade Actions
            foreach (var item in items)
                item.Data = item.Upgrade();

            return items;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync().ConfigureAwait(false);
    }
}
