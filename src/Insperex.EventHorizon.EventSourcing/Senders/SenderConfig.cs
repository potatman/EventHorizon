using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class SenderConfig
{
    public TimeSpan Timeout { get; set; }
    public Func<AggregateStatus, string, IResponse> GetErrorResult { get; set; }
}