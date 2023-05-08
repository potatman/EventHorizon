using System;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class PulsarNamespaceAttribute : Attribute
    {
        public string Tenant { get; set; }
        public string Namespace { get; set; }
        public string CommandNamespace { get; set; }
        public string RequestNamespace { get; set; }
        public int RetentionTimeInMinutes { get; set; }
        public int RetentionSizeInMb { get; set; }

        public PulsarNamespaceAttribute(string tenant, string @namespace)
        {
            Tenant = tenant;
            Namespace = @namespace;

            // Note: pulsar will delete data with no subscriptions, if not set to -1
            RetentionTimeInMinutes = -1;
            RetentionSizeInMb = -1;
        }
    }
}
