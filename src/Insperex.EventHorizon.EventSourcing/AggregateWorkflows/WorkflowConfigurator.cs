using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows
{
    public class WorkflowConfigurator<TState>
        where TState : IState
    {
        private readonly IServiceProvider _provider;
        internal int? BatchSize { get; set; }
        internal IWorkflowMiddleware<TState> WorkflowMiddleware { get; set; }

        public WorkflowConfigurator(IServiceProvider provider)
        {
            _provider = provider;
        }

        public WorkflowConfigurator<TState> WithBatchSize(int batchSize)
        {
            BatchSize = batchSize;
            return this;
        }

        public WorkflowConfigurator<TState> WithMiddleware<TMiddleware>() where TMiddleware : IWorkflowMiddleware<TState>
        {
            WorkflowMiddleware = _provider.GetRequiredService<TMiddleware>();
            return this;
        }
    }
}
