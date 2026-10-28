using System;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventSourcing.Interfaces;

namespace EventHorizon.EventSourcing.Aggregates;

public class AggregateConfig<T> where T : class, IState
{
    public bool IsValidationEnabled { get; set; }
    public bool IsRebuildEnabled { get; set; }
    public int? BatchSize { get; set; }
    public IAggregateMiddleware<T> Middleware { get; set; }

    /// <summary>
    /// Attempts for messages whose documents the store rejected individually before they are nacked.
    /// Whole-store failures are retried until they succeed or the consumer stops.
    /// </summary>
    public int MaxDocumentAttempts { get; set; } = 5;

    internal TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    internal TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}
