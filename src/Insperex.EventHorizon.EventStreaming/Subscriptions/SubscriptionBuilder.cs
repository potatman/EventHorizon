using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionBuilder<T> where T : class, ITopicMessage, new()
{
    private readonly IStreamFactory _factory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITopicResolver _topicResolver;
    private readonly List<string> _topics;
    private int? _batchSize = 1000;
    private bool? _isBeginning = true;
    private TimeSpan _noBatchDelay = TimeSpan.FromMilliseconds(10);
    private DateTime? _startDateTime;
    private string _subscriptionName = AssemblyUtil.AssemblyName;
    private bool _redeliverFailedMessages = true;
    private IBackoffStrategy _backoffStrategy = new ConstantBackoffStrategy {Delay = TimeSpan.FromMilliseconds(10)};
    private Func<SubscriptionContext<T>, Task> _onBatch;
    private SubscriptionType _subscriptionType = Abstractions.Models.SubscriptionType.KeyShared;
    private bool _isPreload;

    public SubscriptionBuilder(IStreamFactory factory, ILoggerFactory loggerFactory)
    {
        _factory = factory;
        _loggerFactory = loggerFactory;
        _topics = new List<string>();
        _topicResolver = _factory.GetTopicResolver();
    }

    public SubscriptionBuilder<T> AddStream<TS>(string topic = null)
    {
        // Add Main Topic
        _topics.AddRange(_topicResolver.GetTopics<T>(typeof(TS), topic));

        // Add Sub Topics (for IState only)
        var topics = AssemblyUtil.SubStateDict.GetValueOrDefault(typeof(TS).Name)?
            .SelectMany(x => _topicResolver.GetTopics<T>(x, topic))
            .ToArray();

        if(topics != null)
            _topics.AddRange(topics);

        return this;
    }

    public SubscriptionBuilder<T> SubscriptionName(string name)
    {
        _subscriptionName = $"{AssemblyUtil.AssemblyName}-{name}";
        return this;
    }

    public SubscriptionBuilder<T> SubscriptionType(SubscriptionType subscriptionType)
    {
        _subscriptionType = subscriptionType;
        return this;
    }

    public SubscriptionBuilder<T> NoBatchDelay(TimeSpan delay)
    {
        _noBatchDelay = delay;
        return this;
    }

    public SubscriptionBuilder<T> BatchSize(int size)
    {
        _batchSize = size;
        return this;
    }

    public SubscriptionBuilder<T> StartDateTime(DateTime startDateTime)
    {
        _startDateTime = startDateTime;
        return this;
    }

    public SubscriptionBuilder<T> IsBeginning(bool isBeginning)
    {
        _isBeginning = isBeginning;
        return this;
    }

    public SubscriptionBuilder<T> IsPreLoad(bool isPreload)
    {
        _isPreload = isPreload;
        return this;
    }

    public SubscriptionBuilder<T> RedeliverFailedMessages(bool redeliver)
    {
        _redeliverFailedMessages = redeliver;
        return this;
    }

    public SubscriptionBuilder<T> BackoffStrategy(IBackoffStrategy backoffStrategy)
    {
        _backoffStrategy = backoffStrategy;
        return this;
    }

    public SubscriptionBuilder<T> OnBatch(Func<SubscriptionContext<T>, Task> onBatch)
    {
        _onBatch = onBatch;
        return this;
    }

    public Subscription<T> Build()
    {
        EnsureValid();

        var config = new SubscriptionConfig<T>
        {
            Topics = _topics.Distinct().ToArray(),
            SubscriptionName = _subscriptionName,
            SubscriptionType = _subscriptionType,
            NoBatchDelay = _noBatchDelay,
            BatchSize = _batchSize,
            StartDateTime = _startDateTime,
            IsBeginning = _isBeginning,
            IsPreload = _isPreload,
            RedeliverFailedMessages = _redeliverFailedMessages,
            BackoffStrategy = _backoffStrategy,
            OnBatch = _onBatch
        };
        var logger = _loggerFactory.CreateLogger<Subscription<T>>();

        // Return
        return new Subscription<T>(_factory, config, logger);
    }

    private void EnsureValid()
    {
        var anyFailureHandling = _backoffStrategy != null;
        if (!_redeliverFailedMessages && anyFailureHandling)
        {
            throw new InvalidOperationException(
                "If any failure handling options are set, expect RedeliverFailedMessages to be true.");
        }
    }
}
