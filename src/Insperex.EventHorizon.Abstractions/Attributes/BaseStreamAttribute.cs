using System;

namespace Insperex.EventHorizon.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public abstract class BaseStreamAttribute : Attribute
    {
        public string Tenant { get; set; }
        public string Namespace { get; set; }
        public string Topic { get; set; }
        public Type SubType { get; set; }

        protected BaseStreamAttribute() { }

        protected BaseStreamAttribute(string topic)
        {
            Topic = topic;
        }

        protected BaseStreamAttribute(string tenant, string @namespace, string topic)
        {
            Tenant = tenant;
            Namespace = @namespace;
            Topic = topic;
        }
    }
}
