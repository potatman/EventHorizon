using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Util;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Range = Pulsar.Client.Api.Range;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicReader<T> : ITopicReader<T> where T : ITopicMessage, new()
{
    private readonly PulsarClientResolver _clientResolver;
    private readonly ReaderConfig _config;
    private readonly ITopicAdmin<T> _admin;
    private IReader<T> _reader;

    public PulsarTopicReader(
        PulsarClientResolver clientResolver,
        ReaderConfig config,
        ITopicAdmin<T> admin)
    {
        _clientResolver = clientResolver;
        _config = config;
        _admin = admin;
    }

    public async Task<MessageContext<T>[]> GetNextAsync(int batchSize, TimeSpan timeout)
    {
        var list = new List<MessageContext<T>>();
        var reader = await GetReaderAsync();

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

    private async Task<IReader<T>> GetReaderAsync()
    {
        if (_reader != null)
            return _reader;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _admin.RequireTopicAsync(_config.Topic, cts.Token);

        var client = await _clientResolver.GetPulsarClientAsync();
        var builder = client.NewReader(Schema.JSON<T>())
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
                .Select(x => MurmurHash3.Hash(x) % PulsarTopicConstants.HashKey)
                .Select(x => new Range(x, x))
                .ToArray());

        _reader = await builder.CreateAsync();

        // Move After StartDateTime
        if (_config.StartDateTime != null)
            await _reader.SeekAsync(_config.StartDateTime.Value.Ticks);

        return _reader;
    }

    public async ValueTask DisposeAsync()
    {
        if(_reader != null) await _reader.DisposeAsync();
        _reader = null;
    }
}
