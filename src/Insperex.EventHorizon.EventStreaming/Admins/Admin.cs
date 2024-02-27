using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

namespace Insperex.EventHorizon.EventStreaming.Admins
{
    public class Admin<TMessage>
        where TMessage : ITopicMessage
    {
        private readonly ITopicAdmin<TMessage> _topicAdmin;

        public Admin(ITopicAdmin<TMessage> topicAdmin)
        {
            _topicAdmin = topicAdmin;
        }

        public async Task RequireTopicAsync(Type type, string senderId = default, CancellationToken ct = default)
        {
            await _topicAdmin.RequireTopicAsync(_topicAdmin.GetTopic(type, senderId), ct);
        }

        public async Task DeleteTopicAsync(Type type, string senderId = default, CancellationToken ct = default)
        {
            var topic = _topicAdmin.GetTopic(type, senderId);
            if(topic == null) return;

            await _topicAdmin.DeleteTopicAsync(topic, ct);
        }
    }
}
