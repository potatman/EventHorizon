using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.InMemory.Databases;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.InMemory.Failure;

public class FailureHandlerFactory
{
    private readonly MessageDatabase _messageDatabase;
    private readonly ILoggerFactory _loggerFactory;

    public FailureHandlerFactory(MessageDatabase messageDatabase, ILoggerFactory loggerFactory)
    {
        _messageDatabase = messageDatabase;
        _loggerFactory = loggerFactory;
    }

    public IFailureHandler<T> Create<T>(SubscriptionConfig<T> config) where T: class, ITopicMessage, new()
    {
        if (!config.RedeliverFailedMessages)
            return new OptOutFailureHandler<T>();

        if (config.IsMessageOrderGuaranteedOnFailure)
            return new OrderGuaranteedFailureHandler<T>(config, _messageDatabase,
                _loggerFactory.CreateLogger<OrderGuaranteedFailureHandler<T>>());

        return new BasicFailureHandler<T>(config);
    }
}
