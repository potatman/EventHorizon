using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Serialization;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Pulsar.Client.Api;
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar
{
    public class PulsarClientResolver : IClientResolver<PulsarClient>, IDisposable
    {
        private readonly IOptions<PulsarConfig> _options;
        private PulsarAdminRESTAPIClient _admin;
        private readonly Uri _fileUri;
        private readonly string _fileName;

        public PulsarClientResolver(IOptions<PulsarConfig> options)
        {
            _options = options;

            // Create File for pulsar client
            _fileName = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}oauth2.txt";
            var json = SerializationConstants.Serializer.Serialize(_options.Value.OAuth2);

            if(!File.Exists(_fileName))
                File.WriteAllText(_fileName, json);

            _fileUri = new Uri(_fileName);
        }

        public PulsarClient GetClient()
        {
            var builder = new PulsarClientBuilder()
                .ServiceUrl(_options.Value.ServiceUrl)
                .EnableTransaction(true);

            if (_options.Value.OAuth2 != null)
            {
                var audience = _options.Value.OAuth2.Audience;
                builder = builder.Authentication(AuthenticationFactoryOAuth2.ClientCredentials(new Uri(_options.Value.OAuth2.IssuerUrl), audience, _fileUri));
            }

            return builder.BuildAsync().GetAwaiter().GetResult();
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
                var oauth2 = _options.Value.OAuth2;
                var token = await GetTokenAsync(oauth2.TokenAddress, oauth2.GrantType, oauth2.Audience, _fileUri);
                client.SetBearerToken(token);
            }

            return _admin = new PulsarAdminRESTAPIClient(client);
        }

        public HttpClient GetAdminHttpClient()
        {
            return new HttpClient {BaseAddress = new Uri($"{_options.Value.AdminUrl}/admin/v2/")};
        }

        private static async Task<PulsarOAuthData> ReadOAuth2File(Uri fileUri)
        {
            // Load Json
            var webRequest = WebRequest.Create(fileUri);
            webRequest.Credentials = CredentialCache.DefaultCredentials;
            webRequest.Method ="GET";
            var webResponse = await webRequest.GetResponseAsync();
            var contents = await new StreamReader(webResponse.GetResponseStream()).ReadToEndAsync();
            return SerializationConstants.Serializer.Deserialize<PulsarOAuthData>(contents);
        }

        private static async Task<string> GetTokenAsync(string tokenAddress, string grantType, string audience, Uri fileUri)
        {
            var json = await ReadOAuth2File(fileUri);
            var request = new TokenRequest
            {
                Address = tokenAddress,
                GrantType = grantType,
                ClientId = json.ClientId,
                ClientSecret = json.ClientSecret,
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
            };
            request.Parameters.Add("audience", audience);
            request.Parameters.Add("type", json.Type);
            request.Parameters.Add("client_email", json.ClientEmail);
            request.Parameters.Add("issuer_url", json.IssuerUrl);

            // Get Token
            var client = new HttpClient();
            var response = await client.RequestTokenAsync(request);
            return response.AccessToken;
        }

        public void Dispose()
        {
            File.Delete(_fileName);
        }
    }
}
