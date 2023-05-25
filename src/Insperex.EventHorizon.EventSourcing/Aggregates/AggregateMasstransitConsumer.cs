
using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStore.Interfaces;
using MassTransit;

namespace Insperex.EventHorizon.EventSourcing.Aggregates
{
    public interface IMasstransitRequest : IRequest
    {
        public string StreamId { get; set; }
    }

    public class AggregateMasstransitConsumer<TParent, T, TReq> : IConsumer<Batch<TReq>>
        where TReq : class, IMasstransitRequest
        where TParent : class, IStateParent<T>, new()
        where T : class, IState
    {
        private readonly Aggregator<TParent, T> _aggregator;

        public AggregateMasstransitConsumer(Aggregator<TParent, T> aggregator)
        {
            _aggregator = aggregator;
        }

        public async Task Consume(ConsumeContext<Batch<TReq>> context)
        {
            var reqs = context.Message
                .Select(x => new Request(x.Message.StreamId, x.Message) { Id = x.RequestId.ToString(), })
                .ToArray();
            var reps = await _aggregator.HandleAsync(reqs, context.CancellationToken);
            var dict = reps.ToDictionary(x => x.RequestId);

            foreach (var context2 in context.Message)
            {
                var response = dict[context2.RequestId.ToString()];
                await context2.RespondAsync(response);
            }
        }
    }
}
