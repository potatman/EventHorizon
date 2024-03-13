using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Destructurama.Attributed;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionConfig<TMessage> where TMessage : ITopicMessage
{
    public string[] Topics { get; set; }
    public string SubscriptionName { get; set; }
    public SubscriptionType SubscriptionType { get; set; }
    public int? BatchSize { get; set; }
    [NotLogged] public Dictionary<string, Type> TypeDict { get; set; }

    [NotLogged] public bool? IsBeginning { get; set; }

    [NotLogged] public bool? IsPreload { get; set; }
    [NotLogged] public bool StopAtEnd { get; set; }

    [NotLogged] public DateTime? StartDateTime { get; set; }

    [NotLogged] public TimeSpan NoBatchDelay { get; set; }

    [NotLogged] public bool RedeliverFailedMessages { get; set; }

    [NotLogged] public bool IsMessageOrderGuaranteedOnFailure { get; set; }

    [NotLogged] public IBackoffStrategy BackoffStrategy { get; set; }

    [NotLogged] public Func<SubscriptionContext<TMessage>, Task> OnBatch { get; set; }
}
