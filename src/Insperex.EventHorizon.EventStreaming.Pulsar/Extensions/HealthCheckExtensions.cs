using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Extensions
{
    public static class HealthCheckExtensions
    {
        public static IHealthChecksBuilder AddPulsarHealthCheck(this IHealthChecksBuilder builder)
        {
            return builder.AddCheck<PulsarHealthCheck>(nameof(PulsarHealthCheck));
        }
    }
}
