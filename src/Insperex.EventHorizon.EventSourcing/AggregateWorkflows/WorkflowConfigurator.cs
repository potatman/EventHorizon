using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows
{
    public class WorkflowConfigurator<TParent, TState>
        where TParent : class, IStateParent<TState>, new()
        where TState : class, IState
    {
        private readonly IServiceProvider _provider;
        internal int? BatchSize { get; set; }
        internal IWorkflowMiddleware<TState> WorkflowMiddleware { get; set; }

        internal Action<AggregatorBuilder<TParent, TState>> AggregateConfiguration { get; set; }

        public WorkflowConfigurator(IServiceProvider provider)
        {
            _provider = provider;
        }

        public WorkflowConfigurator<TParent, TState> WithBatchSize(int batchSize)
        {
            BatchSize = batchSize;
            return this;
        }

        public WorkflowConfigurator<TParent, TState> WithMiddleware<TMiddleware>() where TMiddleware : IWorkflowMiddleware<TState>
        {
            WorkflowMiddleware = _provider.GetRequiredService<TMiddleware>();
            return this;
        }

        public WorkflowConfigurator<TParent, TState> WithAggregate(Action<AggregatorBuilder<TParent, TState>> onConfig)
        {
            AggregateConfiguration = onConfig;
            return this;
        }
    }
}
