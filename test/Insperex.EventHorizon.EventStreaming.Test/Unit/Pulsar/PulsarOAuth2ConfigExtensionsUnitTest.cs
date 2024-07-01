using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Xunit;

namespace Insperex.EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public class PulsarOAuth2ConfigExtensionsUnitTest
{
    [Fact]
    public void ToBase64EncodedUri()
    {
        // Arrange
        var config = new PulsarOAuth2Config
        {
            Type = "type",
            ClientId = "client_id",
            ClientSecret = "client_secret",
            ClientEmail = "client_email",
            IssuerUrl = "issuer_url",
            TokenAddress = "token_address",
            GrantType = "grant_type",
            Audience = "audience"
        };

        // Act
        var result = config.ToBase64EncodedUri();

        // Assert
        Assert.StartsWith("data:application/json;base64,", result.ToString());
        Assert.Equal("data", result.Scheme);
        Assert.NotNull(result);
    }

    [Fact]
    public void FromBase64EncodedUri()
    {
        // Arrange
        var config = new PulsarOAuth2Config
        {
            Type = "type",
            ClientId = "client_id",
            ClientSecret = "client_secret",
            ClientEmail = "client_email",
            IssuerUrl = "issuer_url",
            TokenAddress = "token_address",
            GrantType = "grant_type",
            Audience = "audience"
        };
        var uri = config.ToBase64EncodedUri();

        // Act
        var result = PulsarOAuth2ConfigExtensions.FromBase64EncodedUri(uri);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(config.Type, result.Type);
        Assert.Equal(config.ClientId, result.ClientId);
        Assert.Equal(config.ClientSecret, result.ClientSecret);
        Assert.Equal(config.ClientEmail, result.ClientEmail);
        Assert.Equal(config.IssuerUrl, result.IssuerUrl);
        Assert.Equal(config.TokenAddress, result.TokenAddress);
        Assert.Equal(config.GrantType, result.GrantType);
        Assert.Equal(config.Audience, result.Audience);
    }
}
