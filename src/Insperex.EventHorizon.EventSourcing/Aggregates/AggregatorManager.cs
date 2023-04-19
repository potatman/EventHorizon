using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregatorManager<TParent, T>
    where TParent : class, IStateParent<T>, new()
    where T : class, IState
{
    private readonly AttributeUtil _attributeUtil;
    private readonly Aggregator<TParent, T> _aggregator;
    private readonly AggregateConfig<T> _config;
    private readonly ILogger<AggregatorManager<TParent, T>> _logger;

    public AggregatorManager(
        AttributeUtil attributeUtil,
        Aggregator<TParent, T> aggregator,
        ILogger<AggregatorManager<TParent, T>> logger)
    {
        _attributeUtil = attributeUtil;
        _aggregator = aggregator;
        _config = aggregator.GetConfig();
        _logger = logger;
    }

    public async Task Handle<TM>(TM[] messages, int retryCount, CancellationToken ct) where TM : ITopicMessage
    {
        Dictionary<string, Aggregate<T>> aggregateDict;
        do
        {
            _logger.LogInformation("{State} Handling {Count} {Type} (Retry {RetryCount})",
                typeof(T).Name, messages.Length, typeof(TM).Name, retryCount);

            // Load Aggregate
            var streamIds = messages.Select(x => x.StreamId).Distinct().ToArray();
            aggregateDict = await _aggregator.GetAggregatesFromSnapshotsAsync(streamIds, ct);

            // Map/Apply Changes
            Handle(messages, aggregateDict);
            
            // Save Successful Aggregates
            await SaveAllAsync(aggregateDict);
            
            // Publish Successful Responses
            await _aggregator.PublishResponseAsync(aggregateDict, false);
            
            // Setup for Next Iteration, If Any Failures
            messages = messages
                .Where(x => aggregateDict[x.StreamId].Status != AggregateStatus.Ok)
                .ToArray();
            retryCount = ++retryCount;

            _aggregator.ResetAll(aggregateDict);

        } while (_config.RetryLimit != retryCount && messages.Any());
        
        // Publish Failed Responses - After all retry's
        await _aggregator.PublishResponseAsync(aggregateDict, true);
    }


    private void Handle<TM>(TM[] messages, Dictionary<string, Aggregate<T>> aggregateDict) where TM : ITopicMessage
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

    private async Task SaveAllAsync(Dictionary<string, Aggregate<T>> aggregateDict)
    {
        // Save Snapshots, Events, and Publish Responses for Successful Saves
        await _aggregator.SaveSnapshotsAsync(aggregateDict);
        await _aggregator.PublishEventsAsync(aggregateDict);

        // Log Groups of failed snapshots
        var aggStatusLookup = aggregateDict.Values.ToLookup(x => x.Status);
        foreach (var group in aggStatusLookup)
        {
            if (group.Key == AggregateStatus.Ok)
            {
                // _logger.LogInformation("{State} Handled {Count} {Type} (Retry {RetryCount})",
                //     typeof(T).Name, group.Count(), typeof(TM).Name, retryCount);
                continue;
            }
            var first = group.First();
            _logger.LogError("{State} {Count} had {Status} => {Error}", 
                typeof(T).Name, group.Count(), first.Status, first.Error);
        }
    }
}