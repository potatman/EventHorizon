using System;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Microsoft.Extensions.Options;
using Pulsar.Client.Api;
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarClientResolver : IDisposable
{
    private IOptions<PulsarConfig> _options;
    private PulsarAdminRESTAPIClient _admin;
    private PulsarClient _client;
    private Uri _credentials;
    private bool disposed;

    public PulsarClientResolver(IOptions<PulsarConfig> options)
    {
        _options = options;
        _credentials = _options.Value.OAuth2.ToBase64EncodedUri();
    }

    public async Task<PulsarClient> GetPulsarClientAsync()
    {
        if (_client != null)
            return _client;

        var builder = new PulsarClientBuilder()
            .ServiceUrl(_options.Value.ServiceUrl)
            .EnableTransaction(true);

        if (_options.Value.OAuth2 != null)
        {
            var audience = _options.Value.OAuth2.Audience;
            builder = builder.Authentication(AuthenticationFactoryOAuth2.ClientCredentials(new Uri(_options.Value.OAuth2.IssuerUrl), audience, _credentials));
        }

        return await builder.BuildAsync();
    }

    public async Task<IPulsarAdminRESTAPIClient> GetAdminClientAsync()
    {
        if (_admin != null) return _admin;

        var client = new HttpClient
        {
            BaseAddress = new Uri($"{_options.Value.AdminUrl}/admin/v2/")
        };

        if (_options.Value.OAuth2 != null)
        {
            var token = await GetTokenAsync(_options.Value.OAuth2);
            client.SetBearerToken(token);
        }

        return _admin = new PulsarAdminRESTAPIClient(client);
    }

    public HttpClient GetAdminHttpClient()
    {
        return new HttpClient { BaseAddress = new Uri($"{_options.Value.AdminUrl}/admin/v2/") };
    }

    private static async Task<string> GetTokenAsync(PulsarOAuth2Config config)
    {
        var request = new TokenRequest
        {
            Address = config.TokenAddress,
            GrantType = config.GrantType,
            ClientId = config.ClientId,
            ClientSecret = config.ClientSecret,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
        };
        request.Parameters.Add("audience", config.Audience);
        request.Parameters.Add("type", config.Type);
        request.Parameters.Add("client_email", config.ClientEmail);
        request.Parameters.Add("issuer_url", config.IssuerUrl);

        // Get Token
        var client = new HttpClient();
        var response = await client.RequestTokenAsync(request);
        return response.AccessToken;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                _client.CloseAsync();
            }

            // Large fields to null;
            _options = null;
            _admin = null;
            _credentials = null;

            disposed = true;
        }
    }
}
