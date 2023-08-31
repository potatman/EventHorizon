using System;
using System.Threading.Tasks;
using Destructurama.Attributed;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionConfig<T> where T : ITopicMessage
{
    public string[] Topics { get; set; }
    public string SubscriptionName { get; set; }
    public SubscriptionType SubscriptionType { get; set; }
    public int? BatchSize { get; set; }

    [NotLogged] public bool? IsBeginning { get; set; }

    [NotLogged] public DateTime? StartDateTime { get; set; }

    [NotLogged] public TimeSpan NoBatchDelay { get; set; }

    [NotLogged] public bool RedeliverFailedMessages { get; set; }

    [NotLogged] public IBackoffStrategy BackoffStrategy { get; set; }

    [NotLogged] public Func<SubscriptionContext<T>, Task> OnBatch { get; set; }
    [NotLogged] public bool IsPreload { get; set; }
}
