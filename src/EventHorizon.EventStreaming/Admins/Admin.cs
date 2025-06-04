using System;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Interfaces.Streaming;

namespace EventHorizon.EventStreaming.Admins
{
    public class Admin<TM>
        where TM : ITopicMessage
    {
        private readonly ITopicAdmin<TM> _topicAdmin;
        private readonly ITopicResolver _topicResolver;

        public Admin(ITopicAdmin<TM> topicAdmin, ITopicResolver topicResolver)
        {
            _topicAdmin = topicAdmin;
            _topicResolver = topicResolver;
        }

        public async Task RequireTopicAsync(Type type, string name = default, CancellationToken ct = default)
        {
            var topics = _topicResolver.GetTopics<TM>(type, name);
            foreach (var topic in topics)
                await _topicAdmin.RequireTopicAsync(topic, ct);
        }

        public async Task DeleteTopicAsync(Type type, string name = default, CancellationToken ct = default)
        {
            var topics = _topicResolver.GetTopics<TM>(type, name);
            foreach (var topic in topics)
                await _topicAdmin.DeleteTopicAsync(topic, ct);
        }
    }
}
