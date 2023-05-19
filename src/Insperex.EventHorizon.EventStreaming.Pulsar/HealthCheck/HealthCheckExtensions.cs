using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.HealthCheck
{
    public static class HealthCheckExtensions
    {
        private const string Name = "pulsar";

        public static IHealthChecksBuilder AddPulsarHealthCheck(this IHealthChecksBuilder builder, string url)
        {
            return builder.Add(
                new HealthCheckRegistration(
                    Name, x => new PulsarHealthCheck(url),
                    HealthStatus.Degraded,
                    Array.Empty<string>()
                )
            );
        }
    }
}
