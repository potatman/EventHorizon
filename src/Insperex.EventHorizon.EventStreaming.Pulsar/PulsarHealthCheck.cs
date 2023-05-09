using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Insperex.EventHorizon.EventStreaming.Pulsar
{
    public class PulsarHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;

        public PulsarHealthCheck(PulsarClientResolver clientResolver)
        {
            _httpClient = clientResolver.GetHttpClient();
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
        {
            var response = await _httpClient.GetAsync("/brokers/health", cancellationToken);
            return new HealthCheckResult(response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy);
        }
    }
}
