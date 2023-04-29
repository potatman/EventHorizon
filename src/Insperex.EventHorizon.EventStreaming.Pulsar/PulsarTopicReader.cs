using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Util;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Range = Pulsar.Client.Api.Range;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicReader<T> : ITopicReader<T> where T : ITopicMessage, new()
{
    private readonly PulsarClient _client;
    private readonly ReaderConfig _config;
    private readonly ITopicAdmin _admin;
    private IReader<T> _reader;

    public PulsarTopicReader(
        PulsarClient client,
        ReaderConfig config,
        ITopicAdmin admin)
    {
        _client = client;
        _config = config;
        _admin = admin;
    }

    public async Task<MessageContext<T>[]> GetNextAsync(int batchSize, TimeSpan timeout)
    {
        var list = new List<MessageContext<T>>();
        var reader = await GetReaderAsync();

        // Move After StartDateTime
        if (_config.StartDateTime != null)
        {
            await reader.SeekAsync(_config.StartDateTime.Value.Ticks);
        }

        Message<T> message;
        do
        {
            try
            {
                var cts = new CancellationTokenSource(timeout);
                message = await reader.ReadNextAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                // ignore
                message = null;
            }

            // Defensive
            if (message == null)
                continue;

            // Note: reader key hashing isn't perfect, need to check here.
            if (_config.Keys != null && !_config.Keys.Contains(message?.Key))
                continue;

            // Stop at EndDateTime
            if (_config.EndDateTime != null && new DateTime(message.PublishTime) > _config.EndDateTime)
                break;

            list.Add(new MessageContext<T>
            {
                Data = message.GetValue(),
                TopicData = PulsarMessageMapper.MapTopicData(list.Count.ToString(CultureInfo.InvariantCulture), message, _config.Topic)
            });
        } while (message != null && list.Count < batchSize && await reader.HasMessageAvailableAsync());

        return list.ToArray();
    }

    public void Dispose()
    {
        _reader.DisposeAsync().GetAwaiter().GetResult();
        _reader = null;
    }

    private async Task<IReader<T>> GetReaderAsync()
    {
        if (_reader != null)
            return _reader;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _admin.RequireTopicAsync(_config.Topic, cts.Token);

        var builder = _client.NewReader(Schema.JSON<T>())
            .Topic(_config.Topic)
            .ReaderName(NameUtil.AssemblyNameWithGuid)
            .ReceiverQueueSize(1000);

        if (_config.IsBeginning != null)
            builder = builder.StartMessageId(
                _config.IsBeginning == true
                    ? MessageId.Earliest
                    : MessageId.Latest);

        if (_config.Keys?.Any() == true)
            builder = builder.KeyHashRange(_config.Keys
                .Select(x => MurmurHash3.Hash(x) % 65536)
                .Select(x => new Range(x, x))
                .ToArray());

        _reader = await builder.CreateAsync();

        return _reader;
    }
}
