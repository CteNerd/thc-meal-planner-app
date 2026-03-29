using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.MealPlans;

namespace ThcMealPlanner.Tests;

public sealed class OpenAiApiKeyProviderTests
{
    [Fact]
    public async Task GetApiKeyAsync_WhenSecretArnMissing_ReturnsNull()
    {
        var fakeSecrets = new FakeSecretsManagerClient();
        var provider = CreateProvider(fakeSecrets, new OpenAiOptions { SecretArn = null });

        var result = await provider.GetApiKeyAsync();

        result.Should().BeNull();
        fakeSecrets.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenRawSkValue_ReturnsKeyAndCaches()
    {
        var fakeSecrets = new FakeSecretsManagerClient
        {
            ResponseFactory = _ => new GetSecretValueResponse { SecretString = "sk-test-key" }
        };

        var provider = CreateProvider(fakeSecrets, new OpenAiOptions { SecretArn = "arn:aws:secretsmanager:us-east-1:123:secret:test" });

        var first = await provider.GetApiKeyAsync();
        var second = await provider.GetApiKeyAsync();

        first.Should().Be("sk-test-key");
        second.Should().Be("sk-test-key");
        fakeSecrets.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenJsonContainsApiKey_ReturnsParsedValue()
    {
        var fakeSecrets = new FakeSecretsManagerClient
        {
            ResponseFactory = _ => new GetSecretValueResponse { SecretString = "{\"apiKey\":\"sk-json-key\"}" }
        };

        var provider = CreateProvider(fakeSecrets, new OpenAiOptions { SecretArn = "arn:aws:secretsmanager:us-east-1:123:secret:test" });

        var result = await provider.GetApiKeyAsync();

        result.Should().Be("sk-json-key");
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenSecretEmpty_ReturnsNull()
    {
        var fakeSecrets = new FakeSecretsManagerClient
        {
            ResponseFactory = _ => new GetSecretValueResponse { SecretString = " " }
        };

        var provider = CreateProvider(fakeSecrets, new OpenAiOptions { SecretArn = "arn:aws:secretsmanager:us-east-1:123:secret:test" });

        var result = await provider.GetApiKeyAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenPayloadHasNoApiKey_ReturnsNull()
    {
        var fakeSecrets = new FakeSecretsManagerClient
        {
            ResponseFactory = _ => new GetSecretValueResponse { SecretString = "{\"foo\":\"bar\"}" }
        };

        var provider = CreateProvider(fakeSecrets, new OpenAiOptions { SecretArn = "arn:aws:secretsmanager:us-east-1:123:secret:test" });

        var result = await provider.GetApiKeyAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenSecretsManagerThrows_ReturnsNull()
    {
        var fakeSecrets = new FakeSecretsManagerClient
        {
            ExceptionFactory = _ => new InvalidOperationException("boom")
        };

        var provider = CreateProvider(fakeSecrets, new OpenAiOptions { SecretArn = "arn:aws:secretsmanager:us-east-1:123:secret:test" });

        var result = await provider.GetApiKeyAsync();

        result.Should().BeNull();
        fakeSecrets.CallCount.Should().Be(1);
    }

    private static OpenAiApiKeyProvider CreateProvider(IAmazonSecretsManager secretsManager, OpenAiOptions options)
    {
        return new OpenAiApiKeyProvider(
            secretsManager,
            Options.Create(options),
            NullLogger<OpenAiApiKeyProvider>.Instance);
    }

    private sealed class FakeSecretsManagerClient : AmazonSecretsManagerClient
    {
        public int CallCount { get; private set; }

        public Func<GetSecretValueRequest, GetSecretValueResponse>? ResponseFactory { get; init; }

        public Func<GetSecretValueRequest, Exception>? ExceptionFactory { get; init; }

        public FakeSecretsManagerClient()
            : base(new BasicAWSCredentials("test", "test"), new AmazonSecretsManagerConfig { RegionEndpoint = RegionEndpoint.USEast1 })
        {
        }

        public override Task<GetSecretValueResponse> GetSecretValueAsync(GetSecretValueRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (ExceptionFactory is not null)
            {
                throw ExceptionFactory(request);
            }

            return Task.FromResult(ResponseFactory?.Invoke(request) ?? new GetSecretValueResponse());
        }
    }
}
