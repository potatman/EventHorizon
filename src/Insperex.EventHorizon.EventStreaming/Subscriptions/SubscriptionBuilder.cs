using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionBuilder<TMessage>
    where TMessage : ITopicMessage
{
    private readonly Formatter _formatter;
    private readonly IStreamFactory _factory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _topics;
    private readonly Dictionary<string, Type> _typeDict = new();
    private int? _batchSize = 1000;
    private bool? _isBeginning = true;
    private bool _isPreload;
    private bool _stopAtEnd;
    private TimeSpan _noBatchDelay = TimeSpan.FromMilliseconds(10);
    private DateTime? _startDateTime;
    private string _subscriptionName = AssemblyUtil.AssemblyName;
    private bool _redeliverFailedMessages = true;
    private bool _guaranteeMessageOrderOnFailure;
    private IBackoffStrategy _backoffStrategy = new ConstantBackoffStrategy {Delay = TimeSpan.FromMilliseconds(10)};
    private Func<SubscriptionContext<TMessage>, Task> _onBatch;
    private SubscriptionType _subscriptionType = Abstractions.Models.SubscriptionType.KeyShared;
    private readonly Type _messageType;

    public SubscriptionBuilder(Formatter formatter, IStreamFactory factory, ILoggerFactory loggerFactory)
    {
        _formatter = formatter;
        _factory = factory;
        _loggerFactory = loggerFactory;
        _topics = new List<string>();
        _messageType = typeof(TMessage);
    }

    public SubscriptionBuilder<TMessage> AddStateStream<TState>(Assembly assembly = null, string nodeId = null) where TState : IState
    {
        var rootStateType = typeof(TState);

        // Loop States Inside State
        foreach (var stateType in ReflectionFactory.GetStateDetail(rootStateType).AllStateTypes)
        {
            var subStateDetails = ReflectionFactory.GetStateDetail(stateType);
            var subStateTypes = subStateDetails.MessageTypeDict[_messageType];
            if (subStateTypes.Count != 0)
            {
                // Add Types and Topics
                _typeDict.AddRange(subStateTypes);
                var topic = _formatter.GetTopic<TMessage>(assembly, stateType, nodeId);
                if(!_topics.Contains(topic))
                    _topics.Add(topic);
            }
            else
            {
                // Loop Sates from Events
                // Note: needed for subscribers that don't own the actions (like views)
                var actionStates = ReflectionFactory.GetStateDetail(stateType).MessageStateDict[_messageType];
                foreach (var state in actionStates)
                {
                    var stateDetails = ReflectionFactory.GetStateDetail(state);
                    var types = stateDetails.MessageTypeDict[_messageType];
                    if(types == null) continue;

                    // Add Types and Topics
                    _typeDict.AddRange(types);
                    var topic = _formatter.GetTopic<TMessage>(assembly, state, nodeId);
                    if(!_topics.Contains(topic))
                        _topics.Add(topic);
                }
            }

        }

        return this;
    }

    public SubscriptionBuilder<TMessage> AddStream<TAction>() where TAction : IAction
    {
        var actionType = typeof(TAction);

        // Add Types and Topics
        _typeDict.AddRange(ReflectionFactory.GetTypeDetail(actionType).GetTypes<TAction>());
        _topics.Add(_formatter.GetTopic<TMessage>(actionType.Assembly, actionType));

        return this;
    }

    public SubscriptionBuilder<TMessage> SubscriptionName(string name)
    {
        _subscriptionName = $"{AssemblyUtil.AssemblyName}-{name}";
        return this;
    }

    public SubscriptionBuilder<TMessage> SubscriptionType(SubscriptionType subscriptionType)
    {
        _subscriptionType = subscriptionType;
        return this;
    }

    public SubscriptionBuilder<TMessage> NoBatchDelay(TimeSpan delay)
    {
        _noBatchDelay = delay;
        return this;
    }

    public SubscriptionBuilder<TMessage> BatchSize(int size)
    {
        _batchSize = size;
        return this;
    }

    public SubscriptionBuilder<TMessage> StartDateTime(DateTime startDateTime)
    {
        _startDateTime = startDateTime;
        return this;
    }

    public SubscriptionBuilder<TMessage> IsBeginning(bool isBeginning)
    {
        _isBeginning = isBeginning;
        return this;
    }

    public SubscriptionBuilder<TMessage> IsPreLoad(bool isPreload)
    {
        _isPreload = isPreload;
        return this;
    }

    public SubscriptionBuilder<TMessage> StopAtEnd(bool stopAtEnd)
    {
        _stopAtEnd = stopAtEnd;
        return this;
    }

    public SubscriptionBuilder<TMessage> RedeliverFailedMessages(bool redeliver)
    {
        _redeliverFailedMessages = redeliver;
        return this;
    }

    public SubscriptionBuilder<TMessage> GuaranteeMessageOrderOnFailure(bool guarantee)
    {
        _guaranteeMessageOrderOnFailure = guarantee;
        return this;
    }

    public SubscriptionBuilder<TMessage> BackoffStrategy(IBackoffStrategy backoffStrategy)
    {
        _backoffStrategy = backoffStrategy;
        return this;
    }

    public SubscriptionBuilder<TMessage> OnBatch(Func<SubscriptionContext<TMessage>, Task> onBatch)
    {
        _onBatch = onBatch;
        return this;
    }

    public Subscription<TMessage> Build()
    {
        EnsureValid();

        var config = new SubscriptionConfig<TMessage>
        {
            Topics = _topics.Distinct().ToArray(),
            TypeDict = _typeDict,
            SubscriptionName = _subscriptionName,
            SubscriptionType = _subscriptionType,
            NoBatchDelay = _noBatchDelay,
            BatchSize = _batchSize,
            StartDateTime = _startDateTime,
            IsBeginning = _isBeginning,
            IsPreload = _isPreload,
            StopAtEnd = _stopAtEnd,
            RedeliverFailedMessages = _redeliverFailedMessages,
            IsMessageOrderGuaranteedOnFailure = _guaranteeMessageOrderOnFailure,
            BackoffStrategy = _backoffStrategy,
            OnBatch = _onBatch,
        };
        var logger = _loggerFactory.CreateLogger<Subscription<TMessage>>();

        // Return
        return new Subscription<TMessage>(_factory, config, logger);
    }

    private void EnsureValid()
    {
        var anyFailureHandling = _backoffStrategy != null || _guaranteeMessageOrderOnFailure;
        if (!_redeliverFailedMessages && anyFailureHandling)
        {
            throw new InvalidOperationException(
                "If any failure handling options are set, expect RedeliverFailedMessages to be true.");
        }
    }
}
