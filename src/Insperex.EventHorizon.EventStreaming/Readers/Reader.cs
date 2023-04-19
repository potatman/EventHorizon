using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Microsoft.Extensions.Logging;
using Insperex.EventHorizon.EventStreaming.Extensions;

namespace Insperex.EventHorizon.EventStreaming.Readers;

public class Reader<T> : IDisposable where T : class, ITopicMessage
{
    private readonly ITopicReader<T> _reader;

    public Reader(ITopicReader<T> reader)
    {
        _reader = reader;
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }

    public async Task<MessageContext<T>[]> GetNextAsync(int batchSize, TimeSpan? timeout = default)
    {
        using var activity = TraceConstants.ActivitySource.StartActivity();
        try
        {
            timeout ??= TimeSpan.FromSeconds(10);
            var items = await _reader.GetNextAsync(batchSize, timeout.Value);
            activity?.SetTag(TraceConstants.Tags.Count, items.Length);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            // Upgrade Actions
            foreach (var item in items)
                item.Data = item.Data.Upgrade();
            
            return items;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}