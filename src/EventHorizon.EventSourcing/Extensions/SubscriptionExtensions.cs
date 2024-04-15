using System.Collections.Generic;
using System.Linq;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventStreaming.Subscriptions;

namespace EventHorizon.EventSourcing.Extensions
{
    public static class SubscriptionExtensions
    {
        public static void NackFailedMessagesOnAggregates<TMessage, TState>(this SubscriptionContext<TMessage> batch, Dictionary<string, Aggregate<TState>> aggregateDict)
            where TMessage : ITopicMessage
            where TState : class, IState
        {
            // Get Failed StreamIds
            var failedIds = aggregateDict.Values
                .Where(x => x.Error != null)
                .Select(x => x.Id)
                .ToArray();

            // Get Failed Messages
            var failedMessages = batch.Messages
                .Where(x => failedIds.Contains(x.Data.StreamId))
                .ToArray();

            // Nack
            batch.Nack(failedMessages);
        }

    }
}
