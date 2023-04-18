using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Readers;

public class Reader<T> : IDisposable where T : ITopicMessage
{
    private readonly ReaderConfig _config;
    private readonly ILogger<Reader<T>> _logger;
    private readonly ITopicReader<T> _reader;

    public Reader(ITopicReader<T> reader, ReaderConfig config, ILogger<Reader<T>> logger)
    {
        _reader = reader;
        _config = config;
        _logger = logger;
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
            var events = await _reader.GetNextAsync(batchSize, timeout.Value);
            activity?.SetTag(TraceConstants.Tags.Count, events.Length);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return events;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}