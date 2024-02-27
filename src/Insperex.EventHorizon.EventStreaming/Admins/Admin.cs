using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

namespace Insperex.EventHorizon.EventStreaming.Admins
{
    public class Admin<TM>
        where TM : ITopicMessage
    {
        private readonly ITopicAdmin<TM> _topicAdmin;

        public Admin(ITopicAdmin<TM> topicAdmin)
        {
            _topicAdmin = topicAdmin;
        }

        public async Task RequireTopicAsync(Type type, string name = default, CancellationToken ct = default)
        {
            await _topicAdmin.RequireTopicAsync(_topicAdmin.GetTopic(type, name), ct);
        }

        public async Task DeleteTopicAsync(Type type, string name = default, CancellationToken ct = default)
        {
            var topic = _topicAdmin.GetTopic(type, name);
            if(topic == null) return;

            await _topicAdmin.DeleteTopicAsync(topic, ct);
        }
    }
}
