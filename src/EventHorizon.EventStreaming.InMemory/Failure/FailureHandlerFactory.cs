﻿using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.InMemory.Databases;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming.InMemory.Failure;

public class FailureHandlerFactory
{
    private readonly MessageDatabase _messageDatabase;
    private readonly ILoggerFactory _loggerFactory;

    public FailureHandlerFactory(MessageDatabase messageDatabase, ILoggerFactory loggerFactory)
    {
        _messageDatabase = messageDatabase;
        _loggerFactory = loggerFactory;
    }

    public IFailureHandler<TMessage> Create<TMessage>(SubscriptionConfig<TMessage> config)
        where TMessage : ITopicMessage
    {
        if (!config.RedeliverFailedMessages)
            return new OptOutFailureHandler<TMessage>();

        if (config.IsMessageOrderGuaranteedOnFailure)
            return new OrderGuaranteedFailureHandler<TMessage>(config, _messageDatabase,
                _loggerFactory.CreateLogger<OrderGuaranteedFailureHandler<TMessage>>());

        return new BasicFailureHandler<TMessage>(config);
    }
}