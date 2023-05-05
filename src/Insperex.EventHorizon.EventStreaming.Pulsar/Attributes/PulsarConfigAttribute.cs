using System;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class PulsarConfigAttribute : Attribute
    {
        public string Tenant { get; set; }
        public string EventNamespace { get; set; }
        public string CommandNamespace { get; set; }
        public string RequestNamespace { get; set; }
        public int RetentionTimeInMinutes { get; set; }
        public int RetentionSizeInMb { get; set; }

        public PulsarConfigAttribute(string tenant)
        {
            Tenant = tenant;

            // Note: pulsar will delete data with no subscriptions, if not set to -1
            RetentionTimeInMinutes = -1;
            RetentionSizeInMb = -1;
        }
    }
}
