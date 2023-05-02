using System;
using System.Net.Http;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Microsoft.Extensions.Options;
using Pulsar.Client.Api;
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar
{
    public class PulsarClientResolver
    {
        private readonly IOptions<PulsarConfig> _options;
        private PulsarAdminRESTAPIClient _admin;
        private PulsarClient _client;

        public PulsarClientResolver(IOptions<PulsarConfig> options)
        {
            _options = options;
        }

        public async Task<PulsarClient> GetPulsarClientAsync()
        {
            return _client ??= await new PulsarClientBuilder()
                .ServiceUrl(_options.Value.ServiceUrl)
                .EnableTransaction(true)
                .BuildAsync();
        }

        public IPulsarAdminRESTAPIClient GetAdminClient()
        {
            return _admin ??= new PulsarAdminRESTAPIClient(new HttpClient
            {
                BaseAddress = new Uri($"{_options.Value.AdminUrl}/admin/v2/")
            });
        }

    }
}
