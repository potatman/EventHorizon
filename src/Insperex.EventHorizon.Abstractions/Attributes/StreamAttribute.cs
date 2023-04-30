using System;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class StreamAttribute<T> : BaseStreamAttribute where T : ITopicMessage
    {

        public StreamAttribute(string topic) : base(topic) { }

        public StreamAttribute(string tenant, string @namespace, string topic) : base(tenant, @namespace, topic) { }

        public StreamAttribute(Type subType)
        {
            var attr = subType.GetCustomAttribute<StreamAttribute<T>>();
            if (attr == null) throw new Exception($"{subType.Name} is missing StreamAttribute<{typeof(T).Name}>");
            SubType = subType;
            Tenant = attr.Tenant;
            Namespace = attr.Namespace;
            Topic = attr.Topic;
        }
    }
}
