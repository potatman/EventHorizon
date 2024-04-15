using System;

namespace EventHorizon.Abstractions.Attributes
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
