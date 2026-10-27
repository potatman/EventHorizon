using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;
using EventHorizon.EventStreaming.Pulsar.Extensions;
using EventHorizon.EventStreaming.Pulsar.Models;
using Microsoft.Extensions.Options;
using Pulsar.Client.Api;
using SharpPulsar.Admin.v2;

namespace EventHorizon.EventStreaming.Pulsar;

public class PulsarClientResolver : IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _clientSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<(Type MessageType, string Topic, TimeSpan SendTimeout), Lazy<Task<object>>> _producers = new();
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

        await _clientSemaphore.WaitAsync();
        try
        {
            if (_client != null)
                return _client;

            var builder = new PulsarClientBuilder()
                .ServiceUrl(_options.Value.ServiceUrl);

            if (_options.Value.OAuth2 != null)
            {
                var audience = _options.Value.OAuth2.Audience;
                builder = builder.Authentication(AuthenticationFactoryOAuth2.ClientCredentials(new Uri(_options.Value.OAuth2.IssuerUrl), audience, _credentials));
            }

            return _client = await builder.BuildAsync();
        }
        finally
        {
            _clientSemaphore.Release();
        }
    }

    /// <summary>
    /// Returns the process-wide producer for a message type, topic and send timeout, creating it
    /// on first use. Publishers are built per aggregator and sender, which callers build per
    /// message batch or request, so a producer per publisher grows without bound, and every
    /// idle producer keeps its batching and send-timeout timers running. Shared producers close
    /// with the client. A failed creation is evicted so the next caller retries it.
    /// </summary>
    internal async Task<TProducer> GetOrAddProducerAsync<TProducer>(
        Type messageType, string topic, TimeSpan sendTimeout, Func<Task<TProducer>> create)
        where TProducer : class
    {
        var key = (messageType, topic, sendTimeout);
        var entry = _producers.GetOrAdd(key, _ => new Lazy<Task<object>>(async () => await create()));
        try
        {
            return (TProducer)await entry.Value;
        }
        catch
        {
            _producers.TryRemove(new KeyValuePair<(Type, string, TimeSpan), Lazy<Task<object>>>(key, entry));
            throw;
        }
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

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;

        var client = _client;
        _client = null;
        if (client != null)
        {
            try
            {
                await client.CloseAsync();
            }
            catch (Exception)
            {
                // Best effort close; broker may already be unreachable.
            }
        }

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            var client = _client;
            _client = null;
            if (disposing && client != null)
            {
                try
                {
                    client.CloseAsync().GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                    // Best effort close; broker may already be unreachable.
                }
            }

            // Closing the client closed the shared producers.
            _producers.Clear();

            // Large fields to null;
            _options = null;
            _admin = null;
            _credentials = null;

            disposed = true;
        }
    }
}
