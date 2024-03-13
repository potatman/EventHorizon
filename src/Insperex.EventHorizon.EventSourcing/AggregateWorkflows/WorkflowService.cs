using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows
{
public class WorkflowService<TWrapper, TState, TMessage>
        where TWrapper : class, IStateParent<TState>, new()
        where TState : class, IState
        where TMessage : class, ITopicMessage, new()
    {
        private readonly IWorkflowMiddleware<TState> _middleware;
        private readonly ILogger<WorkflowService<TWrapper, TState, TMessage>> _logger;
        private readonly Aggregator<TWrapper, TState> _aggregator;

        public WorkflowService(IServiceProvider serviceProvider, IWorkflowMiddleware<TState> middleware)
        {
            _middleware = middleware;
            _logger = serviceProvider.GetRequiredService<ILogger<WorkflowService<TWrapper, TState, TMessage>>>();

            // Aggregator
            var aggregatorBuilder = serviceProvider.GetRequiredService<AggregatorBuilder<TWrapper, TState>>();
            _aggregator = aggregatorBuilder.Build();
        }

        public async Task<Dictionary<string, Aggregate<TState>>> LoadAsync(TMessage[] messages, CancellationToken ct)
        {
            // Load Aggregate
            var streamIds = messages.Select(x => x.StreamId).Distinct().ToArray();
            var aggregateDict = await _aggregator.GetAggregatesFromStateAsync(streamIds, ct);

            // OnLoad Hook
            SafeHook(() => _middleware?.OnLoad(aggregateDict), aggregateDict);

            return aggregateDict;
        }

        public void TriggerHandle(TMessage[] messages, Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            var sw = Stopwatch.StartNew();
            foreach (var message in messages)
            {
                var agg = aggregateDict.GetValueOrDefault(message.StreamId);
                if (agg.Error != null)
                    continue;
                try
                {
                    switch (message)
                    {
                        case Command command: agg.Handle(command); break;
                        case Request request: agg.Handle(request); break;
                        case Event @event: agg.Handle(@event); break;
                    }
                }
                catch (Exception e)
                {
                    agg.SetStatus(HttpStatusCode.InternalServerError, e.Message);
                }
            }

            _logger.LogInformation("TriggerHandled {Count} {Type} Aggregate(s) in {Duration}",
                aggregateDict.Count, typeof(TState).Name, sw.ElapsedMilliseconds);
        }

        public void TriggerApplyEvents(TMessage[] messages, Dictionary<string, Aggregate<TState>> aggregateDict, bool isFirstTime)
        {
            var sw = Stopwatch.StartNew();
            foreach (var message in messages)
            {
                var agg = aggregateDict.GetValueOrDefault(message.StreamId);
                if (agg.Error != null)
                    continue;
                try
                {
                    switch (message)
                    {
                        case Event @event: agg.Apply(@event, isFirstTime); break;
                    }
                }
                catch (Exception e)
                {
                    agg.SetStatus(HttpStatusCode.InternalServerError, e.Message);
                }
            }

            _logger.LogInformation("TriggerHandled {Count} {Type} Aggregate(s) in {Duration}",
                aggregateDict.Count, typeof(TState).Name, sw.ElapsedMilliseconds);
        }

        public async Task SaveAsync(Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            // AfterSave Hook
            SafeHook(() => _middleware?.BeforeSave(aggregateDict), aggregateDict);

            // Save Successful Aggregates and Events
            await _aggregator.SaveAllAsync(aggregateDict);

            // AfterSave Hook
            SafeHook(() => _middleware?.AfterSave(aggregateDict), aggregateDict);
        }

        public async Task TryAndPublishResponses(Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            var responses = aggregateDict.Values.SelectMany(x => x.Responses).ToArray();
            if(responses.Any())
                await _aggregator.PublishResponseAsync(responses);
        }

        private static void SafeHook(Action action, Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                foreach (var agg in aggregateDict.Values)
                    agg.SetStatus(HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }
}
