using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Workflows;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows
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
                await workflow.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var workflow in _workflows)
                await workflow.StopAsync(cancellationToken);
        }
    }
}
