using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStreaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class Aggregator<TParent, T>
    where TParent : class, IStateParent<T>, new()
    where T : class, IState
{
    private readonly AggregateConfig<T> _config;
    private readonly ICrudStore<TParent> _crudStore;
    private readonly ILogger<Aggregator<TParent, T>> _logger;
    private readonly StreamingClient _streamingClient;

    public Aggregator(
        ICrudStore<TParent> crudStore,
        StreamingClient streamingClient,
        AggregateConfig<T> config,
        ILogger<Aggregator<TParent, T>> logger)
    {
        _crudStore = crudStore;
        _streamingClient = streamingClient;
        _config = config;
        _logger = logger;
    }

    internal AggregateConfig<T> GetConfig()
    {
        return _config;
    }

    public Task SetupAsync()
    {
        return _crudStore.Setup(CancellationToken.None);
    }

    public async Task RebuildAllAsync(CancellationToken ct)
    {
        var minDateTime = await _crudStore.GetLastUpdatedDateAsync(ct);

        // NOTE: return with one ms forward because mongodb rounds to one ms
        minDateTime = minDateTime.AddMilliseconds(1);

        var reader = _streamingClient.CreateReader<Event>().AddStream<T>().StartDateTime(minDateTime).Build();

        while (!ct.IsCancellationRequested)
        {
            var events = await reader.GetNextAsync(1000);
            if (!events.Any()) break;

            var lookup = events.ToLookup(x => x.Data.StreamId);
            var streamIds = lookup.Select(x => x.Key).ToArray();
            var models = await _crudStore.GetAllAsync(streamIds, ct);
            var modelsDict = models.ToDictionary(x => x.Id);
            var dict = new Dictionary<string, Aggregate<T>>();
            foreach (var streamId in streamIds)
            {
                var agg = modelsDict.ContainsKey(streamId)
                    ? new Aggregate<T>(modelsDict[streamId])
                    : new Aggregate<T>(streamId);

                foreach (var message in lookup[streamId])
                    agg.Apply(message.Data);

                dict[agg.Id] = agg;
            }

            if(! dict.Any()) return;
            await SaveSnapshotsAsync(dict);
            await PublishEventsAsync(dict);
            ResetAll(dict);
        }
    }

    public async Task<Response[]> HandleAsync<TM>(TM[] messages, CancellationToken ct) where TM : ITopicMessage
    {
        var responses = new List<Response>();
        Dictionary<string, Aggregate<T>> aggregateDict;
        var retryCount = 0;
        do
        {
            _logger.LogInformation("{State} Handling {Count} {Type} (Retry {RetryCount})",
                typeof(T).Name, messages.Length, typeof(TM).Name, retryCount);

            // Load Aggregate
            var streamIds = messages.Select(x => x.StreamId).Distinct().ToArray();
            aggregateDict = await GetAggregatesFromSnapshotsAsync(streamIds, ct);

            // Map/Apply Changes
            TriggerHandle(messages, aggregateDict);

            // Save Successful Aggregates
            await SaveAllAsync(aggregateDict);

            // Add Successful Responses
            responses.AddRange(aggregateDict.Values
                .Where(x => x.Status == AggregateStatus.Ok)
                .SelectMany(x => x.Responses).ToArray());

            // Setup for Next Iteration, If Any Failures
            messages = messages
                .Where(x => aggregateDict[x.StreamId].Status != AggregateStatus.Ok)
                .ToArray();

            retryCount = ++retryCount;
        } while (_config.RetryLimit != retryCount && messages.Any());

        responses.AddRange(aggregateDict.Values
            .Where(x => x.Status == AggregateStatus.Ok)
            .SelectMany(x => x.Responses).ToArray());

        return responses.Distinct().ToArray();
    }

    private void TriggerHandle<TM>(TM[] messages, Dictionary<string, Aggregate<T>> aggregateDict) where TM : ITopicMessage
    {
        foreach (var message in messages)
        {
            var agg = aggregateDict.GetValueOrDefault(message.StreamId);
            try
            {
                switch (message)
                {
                    case Command command: agg.Handle(command); break;
                    case Request request: agg.Handle(request); break;
                    case Event @event: agg.Apply(@event, false); break;
                }
            }
            catch (Exception e)
            {
                agg.SetStatus(AggregateStatus.HandlerFailed, e.Message);
            }
        }

        // OnCompleted Hook
        try
        {
            _config.BeforeSave?.Invoke(aggregateDict.Values.ToArray());
        }
        catch (Exception e)
        {
            foreach (var agg in aggregateDict.Values)
                agg.SetStatus(AggregateStatus.BeforeSaveFailed, e.Message);
        }
    }

    #region Save

    private async Task SaveAllAsync(Dictionary<string, Aggregate<T>> aggregateDict)
    {
        // Save Snapshots, Events, and Publish Responses for Successful Saves
        await SaveSnapshotsAsync(aggregateDict);
        await PublishEventsAsync(aggregateDict);

        // Log Groups of failed snapshots
        var aggStatusLookup = aggregateDict.Values.ToLookup(x => x.Status);
        foreach (var group in aggStatusLookup)
        {
            if (group.Key == AggregateStatus.Ok) continue;
            var first = group.First();
            _logger.LogError("{State} {Count} had {Status} => {Error}",
                typeof(T).Name, group.Count(), first.Status, first.Error);
        }
    }

    private async Task SaveSnapshotsAsync(Dictionary<string, Aggregate<T>> aggregateDict)
    {
        try
        {
            // Save Snapshots and then track failures
            var parents = aggregateDict.Values
                .Where(x => x.Status == AggregateStatus.Ok)
                .Where(x => x.IsDirty)
                .Select(x => new TParent
                {
                    Id = x.Id,
                    SequenceId = x.SequenceId,
                    State = x.State,
                    CreatedDate = x.CreatedDate,
                    UpdatedDate = x.UpdatedDate
                })
                .ToArray();

            if (parents.Any() != true)
                return;

            var results = await _crudStore.UpsertAsync(parents, CancellationToken.None);
            foreach (var id in results.FailedIds)
                aggregateDict[id].SetStatus(AggregateStatus.SaveSnapshotFailed);
            foreach (var id in results.PassedIds)
                aggregateDict[id].SequenceId++;
        }
        catch (Exception ex)
        {
            foreach (var aggregate in aggregateDict.Values)
                aggregate.SetStatus(AggregateStatus.SaveSnapshotFailed, ex.Message);
        }
    }

    private async Task PublishEventsAsync(Dictionary<string, Aggregate<T>> aggregateDict)
    {
        var events = aggregateDict.Values
            .Where(x => x.Status == AggregateStatus.Ok)
            .SelectMany(x => x.Events)
            .ToArray();

        if (events.Any() != true)
            return;

        try
        {
            var publisher = _streamingClient.CreatePublisher<Event>().AddStream<T>().Build();
            await publisher.PublishAsync(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish events");
            var streamIds = events.Select(x => x.StreamId).Distinct().ToArray();
            foreach (var streamId in streamIds)
                aggregateDict[streamId].SetStatus(AggregateStatus.SaveEventsFailed);
        }
    }

    internal async Task PublishResponseAsync(Response[] responses)
    {
        try
        {
            var responsesLookup = responses.ToLookup(x => x.SenderId);
            foreach (var group in responsesLookup)
            {
                var publisher = _streamingClient.CreatePublisher<Response>().AddStream<T>(group.Key).Build();
                await publisher.PublishAsync(group.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish results");
        }
    }

    private static void ResetAll(Dictionary<string, Aggregate<T>> aggregateDict)
    {
        foreach (var aggregate in aggregateDict.Values)
        {
            aggregate.Events.Clear();
            aggregate.Responses.Clear();
            aggregate.SetStatus(AggregateStatus.Ok);
        }
    }

    #endregion

    #region load

    public async Task<Aggregate<T>> GetAggregateFromSnapshotAsync(string streamId, CancellationToken ct)
    {
        var result = await GetAggregatesFromSnapshotsAsync(new[] { streamId }, ct);
        return result.Values.FirstOrDefault();
    }

    public async Task<Dictionary<string, Aggregate<T>>> GetAggregatesFromSnapshotsAsync(string[] streamIds, CancellationToken ct)
    {
        try
        {
            // Load Snapshots
            streamIds = streamIds.Distinct().ToArray();
            var snapshots = await _crudStore.GetAllAsync(streamIds, ct);
            var parentDict = snapshots.ToDictionary(x => x.Id);

            // Build Aggregate Dict
            var aggregateDict = streamIds
                .Select(x => parentDict.TryGetValue(x, out var value)? new Aggregate<T>(value) : new Aggregate<T>(x))
                .ToDictionary(x => x.Id);

            return aggregateDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load snapshots");
            return streamIds
                .Select(x =>
                {
                    var agg = new Aggregate<T>(x);
                    agg.SetStatus(AggregateStatus.LoadSnapshotFailed, ex.Message);
                    return agg;
                })
                .ToDictionary(x => x.Id);
        }
    }

    public async Task<Aggregate<T>> GetAggregateFromEventsAsync(string streamId, DateTime? endDateTime = null)
    {
        var results = await GetAggregatesFromEventsAsync(new[] { streamId }, endDateTime);
        return results[streamId];
    }

    public async Task<Dictionary<string, Aggregate<T>>> GetAggregatesFromEventsAsync(string[] streamIds,
        DateTime? endDateTime = null)
    {
        var events = await GetEventsAsync(streamIds, endDateTime);
        var eventLookup = events.ToLookup(x => x.Data.StreamId);
        return streamIds
            .Select(x =>
            {
                var e = eventLookup[x].ToArray();
                return e.Any() ? new Aggregate<T>(e.ToArray()) : new Aggregate<T>(x);
            })
            .ToDictionary(x => x.Id);
    }

    public Task<MessageContext<Event>[]> GetEventsAsync(string[] streamIds, DateTime? endDateTime = null)
    {
        var reader = _streamingClient.CreateReader<Event>().AddStream<T>().Keys(streamIds).EndDateTime(endDateTime).Build();
        return reader.GetNextAsync(10000);
    }

    #endregion
}
