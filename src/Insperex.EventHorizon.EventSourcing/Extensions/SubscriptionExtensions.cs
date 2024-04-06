using System.Collections.Generic;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventSourcing.Extensions
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
