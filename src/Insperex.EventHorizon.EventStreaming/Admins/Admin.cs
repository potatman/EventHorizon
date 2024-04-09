using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

namespace Insperex.EventHorizon.EventStreaming.Admins
{
    public class Admin<TMessage>
        where TMessage : ITopicMessage
    {
        private readonly Formatter _formatter;
        private readonly ITopicAdmin<TMessage> _topicAdmin;

        public Admin(Formatter formatter, ITopicAdmin<TMessage> topicAdmin)
        {
            _formatter = formatter;
            _topicAdmin = topicAdmin;
        }

        public Task RequireTopicAsync(Type type, CancellationToken ct = default)
        {
            return _topicAdmin.RequireTopicAsync(_formatter.GetTopic<TMessage>(type), ct);
        }

        public Task DeleteTopicAsync(Type type, CancellationToken ct = default)
        {
            var topic = _formatter.GetTopic<TMessage>(type);
            return topic == null ? Task.CompletedTask : _topicAdmin.DeleteTopicAsync(topic, ct);
        }
    }
}
