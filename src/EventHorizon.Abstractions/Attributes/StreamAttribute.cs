using System;
using System.Reflection;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class StreamAttribute : Attribute
    {
        public string Topic { get; set; }
        public Type SubType { get; set; }

        public StreamAttribute(string topic)
        {
            Topic = topic;
        }

        public StreamAttribute(Type subType)
        {
            var attr = subType.GetCustomAttribute<StreamAttribute>();
            if (attr == null) throw new Exception($"{subType.Name} is missing StreamAttribute");
            SubType = subType;
            Topic = attr.Topic;
        }
    }
}
