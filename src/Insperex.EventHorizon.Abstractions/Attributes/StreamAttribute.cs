using System;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class StreamAttribute : Attribute
    {
        public string TopicFormat { get; set; }

        public StreamAttribute(string topicFormat)
        {
            TopicFormat = topicFormat;
        }
    }
}
