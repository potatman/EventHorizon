using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using Microsoft.Extensions.Hosting;

namespace EventHorizon.EventSourcing.AggregateWorkflows
{
    public class WorkflowHostedService : IHostedService
    {
        private readonly IEnumerable<IWorkflow> _workflows;

        public WorkflowHostedService(IEnumerable<IWorkflow> workflows)
        {
            _workflows = workflows;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var workflow in _workflows)
                await workflow.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var workflow in _workflows)
                await workflow.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
