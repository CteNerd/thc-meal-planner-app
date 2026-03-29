using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Recipes;

namespace ThcMealPlanner.Tests;

public sealed class MealPlanAiServiceTests
{
    [Fact]
    public async Task GenerateRecipeIdsAsync_WhenSlotsEmpty_ReturnsEmpty()
    {
        var service = CreateService(
            apiKey: "test-key",
            responder: _ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await service.GenerateRecipeIdsAsync("2026-04-06", [], [BuildRecipe("rec_1")]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RankSwapCandidatesAsync_WhenCandidatesEmpty_ReturnsEmpty()
    {
        var service = CreateService(
            apiKey: "test-key",
            responder: _ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await service.RankSwapCandidatesAsync("Monday", "dinner", null, []);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateRecipeIdsAsync_WhenApiKeyMissing_ReturnsEmptyWithoutHttpCall()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(apiKey: null, handler: handler);

        var result = await service.GenerateRecipeIdsAsync(
            "2026-04-06",
            [("Monday", "dinner")],
            [BuildRecipe("rec_1")]);

        result.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateRecipeIdsAsync_WhenOpenAiReturnsNonSuccess_FallsBackToEmpty()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var service = CreateService(apiKey: "test-key", handler: handler);

        var result = await service.GenerateRecipeIdsAsync(
            "2026-04-06",
            [("Monday", "dinner")],
            [BuildRecipe("rec_1")]);

        result.Should().BeEmpty();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GenerateRecipeIdsAsync_WhenResponseIsValid_ReturnsDistinctRecipeIds()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"recipeIds\": [\"rec_2\", \"rec_1\", \"rec_2\"]}"
                      }
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        });

        var service = CreateService(apiKey: "test-key", handler: handler);

        var result = await service.GenerateRecipeIdsAsync(
            "2026-04-06",
            [("Monday", "dinner"), ("Tuesday", "dinner")],
            [BuildRecipe("rec_1"), BuildRecipe("rec_2")]);

        result.Should().Equal("rec_2", "rec_1");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().EndWith("/chat/completions");
        handler.Requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.Should().Be("test-key");
    }

    [Fact]
    public async Task RankSwapCandidatesAsync_WhenResponseContainsRanking_ReturnsOrderedIds()
    {
        var service = CreateService(
            apiKey: "test-key",
            responder: _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "{\"rankedRecipeIds\": [\"rec_3\", \"rec_2\", \"rec_1\"]}"
                          }
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var result = await service.RankSwapCandidatesAsync(
            "Monday",
            "dinner",
            "rec_0",
            [BuildRecipe("rec_1"), BuildRecipe("rec_2"), BuildRecipe("rec_3")]);

        result.Should().Equal("rec_3", "rec_2", "rec_1");
    }

    [Fact]
    public async Task GenerateRecipeIdsAsync_WhenResponseMalformed_ReturnsEmpty()
    {
        var service = CreateService(
            apiKey: "test-key",
            responder: _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[]}", Encoding.UTF8, "application/json")
            });

        var result = await service.GenerateRecipeIdsAsync(
            "2026-04-06",
            [("Monday", "dinner")],
            [BuildRecipe("rec_1")]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateRecipeIdsAsync_WhenHttpThrows_ReturnsEmpty()
    {
        var handler = new RecordingHttpMessageHandler(_ => throw new HttpRequestException("network"));
        var service = CreateService(apiKey: "test-key", handler: handler);

        var result = await service.GenerateRecipeIdsAsync(
            "2026-04-06",
            [("Monday", "dinner")],
            [BuildRecipe("rec_1")]);

        result.Should().BeEmpty();
    }

    private static MealPlanAiService CreateService(
        string? apiKey,
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null,
        RecordingHttpMessageHandler? handler = null)
    {
        handler ??= new RecordingHttpMessageHandler(responder ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new HttpClient(handler);
        var options = Options.Create(new OpenAiOptions
        {
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-4o-mini",
            Temperature = 0.2
        });

        return new MealPlanAiService(
            client,
            new StubApiKeyProvider(apiKey),
            options,
            NullLogger<MealPlanAiService>.Instance);
    }

    private static RecipeDocument BuildRecipe(string recipeId)
    {
        return new RecipeDocument
        {
            RecipeId = recipeId,
            FamilyId = "FAM#test-family",
            Name = $"Recipe {recipeId}",
            Category = "dinner",
            Ingredients = [new RecipeIngredientModel { Name = "Ingredient" }],
            Instructions = ["Cook"],
            Tags = [],
            CreatedByUserId = "test-user-123",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class StubApiKeyProvider(string? apiKey) : IOpenAiApiKeyProvider
    {
        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(apiKey);
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }
}
