using System;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionConfig<T> where T : ITopicMessage
{
    public string[] Topics { get; set; }
    public string SubscriptionName { get; set; }
    public SubscriptionType SubscriptionType { get; set; }
    public int? BatchSize { get; set; }
    public DateTime? StartDateTime { get; set; }
    public TimeSpan NoBatchDelay { get; set; }
    public bool? IsBeginning { get; set; }
    public bool IsMessageOrderGuaranteedOnFailure { get; set; }
    public BackoffPolicy RetryBackoffPolicy { get; set; }
    public Func<SubscriptionContext<T>, Task> OnBatch { get; set; }
}
