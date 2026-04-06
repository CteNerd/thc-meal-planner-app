using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Recipes;

namespace ThcMealPlanner.Tests;

public sealed class RecipeImportServiceTests
{
    [Fact]
    public void ParseImportedRecipeDraft_WhenJsonLdRecipePresent_UsesStructuredFields()
    {
        var sourceUrl = new Uri("https://example.com/structured-recipe");
        const string html = """
            <html>
              <head><title>Ignore title</title></head>
              <body>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "Recipe",
                  "name": "Sheet Pan Lemon Chicken",
                  "description": "A quick weeknight dinner.",
                  "recipeCategory": "Dinner",
                  "recipeCuisine": "Mediterranean",
                  "recipeYield": "4 servings",
                  "prepTime": "PT15M",
                  "cookTime": "PT30M",
                  "keywords": "quick, family-friendly",
                  "recipeIngredient": ["1 lb chicken", "2 lemons"],
                  "recipeInstructions": [
                    { "@type": "HowToStep", "text": "Season the chicken." },
                    { "@type": "HowToStep", "text": "Bake until cooked through." }
                  ]
                }
                </script>
              </body>
            </html>
            """;

        var draft = RecipeImportService.ParseImportedRecipeDraft(sourceUrl, html);

        draft.Name.Should().Be("Sheet Pan Lemon Chicken");
        draft.Description.Should().Be("A quick weeknight dinner.");
        draft.Category.Should().Be("dinner");
        draft.Cuisine.Should().Be("Mediterranean");
        draft.Servings.Should().Be(4);
        draft.PrepTimeMinutes.Should().Be(15);
        draft.CookTimeMinutes.Should().Be(30);
        draft.Tags.Should().Contain(["quick", "family-friendly"]);
        draft.Ingredients.Should().HaveCount(2);
        draft.Instructions.Should().ContainInOrder("Season the chicken.", "Bake until cooked through.");
        draft.SourceUrl.Should().Be(sourceUrl.ToString());
        draft.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ParseImportedRecipeDraft_WhenNoJsonLd_FallsBackToTextExtraction()
    {
        var sourceUrl = new Uri("https://example.com/fallback");
        const string html = """
            <html>
              <head>
                <title>Tomato Soup</title>
                <meta name="description" content="Simple tomato soup." />
              </head>
              <body>
                <h2>Ingredients:</h2>
                <ul>
                  <li>- 2 cups tomatoes</li>
                  <li>* 1 cup broth</li>
                </ul>
                <h2>Instructions:</h2>
                <ol>
                  <li>1. Blend ingredients.</li>
                  <li>2. Simmer for 15 minutes.</li>
                </ol>
              </body>
            </html>
            """;

        var draft = RecipeImportService.ParseImportedRecipeDraft(sourceUrl, html);

        draft.Name.Should().Be("Tomato Soup");
        draft.Description.Should().Be("Simple tomato soup.");
        draft.Ingredients.Should().Contain(ingredient => ingredient.Name == "2 cups tomatoes");
        draft.Ingredients.Should().Contain(ingredient => ingredient.Name == "1 cup broth");
        draft.Instructions.Should().ContainInOrder("Blend ingredients.", "Simmer for 15 minutes.");
        draft.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportFromUrlAsync_WhenUrlIsLocalhost_RejectsRequest()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler())
        {
            BaseAddress = new Uri("https://example.com")
        };
        var service = new RecipeImportService(
          httpClient,
          null,
          null,
          null,
          NullLogger<RecipeImportService>.Instance);

        var act = async () => await service.ImportFromUrlAsync("http://localhost/recipe", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Localhost URLs are not allowed.*");
    }

    [Fact]
    public void ParseImportedRecipeDraft_WhenJsonLdUsesObjectVariants_ParsesWithoutArrayConversionErrors()
    {
        var sourceUrl = new Uri("https://example.com/object-variants");
        const string html = """
            <html>
              <body>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "Recipe",
                  "name": "One Pot Pasta",
                  "recipeCategory": ["Dinner", "Pasta"],
                  "recipeCuisine": { "name": "Italian" },
                  "recipeYield": ["4 servings"],
                  "keywords": ["quick", "one-pot"],
                  "recipeIngredient": {
                    "itemListElement": [
                      { "@type": "HowToStep", "text": "12 oz pasta" },
                      { "@type": "HowToStep", "text": "2 cups broth" }
                    ]
                  },
                  "recipeInstructions": {
                    "@type": "HowToSection",
                    "itemListElement": [
                      { "@type": "HowToStep", "text": "Add pasta and broth." },
                      { "@type": "HowToStep", "text": "Simmer until tender." }
                    ]
                  }
                }
                </script>
              </body>
            </html>
            """;

        var draft = RecipeImportService.ParseImportedRecipeDraft(sourceUrl, html);

        draft.Name.Should().Be("One Pot Pasta");
        draft.Category.Should().Be("dinner");
        draft.Cuisine.Should().Be("Italian");
        draft.Servings.Should().Be(4);
        draft.Tags.Should().Contain(["quick", "one-pot"]);
        draft.Ingredients.Select(i => i.Name).Should().Contain(["12 oz pasta", "2 cups broth"]);
        draft.Instructions.Should().ContainInOrder("Add pasta and broth.", "Simmer until tender.");
        draft.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ParseImportedRecipeDraft_WhenJsonLdContainsArrayNodes_DoesNotThrowAndParsesRecipe()
    {
        var sourceUrl = new Uri("https://example.com/jsonld-array-graph");
        const string html = """
            <html>
              <body>
                <script type="application/ld+json">
                [
                  {
                    "@context": "https://schema.org",
                    "@type": "WebPage",
                    "name": "Buffalo Wings"
                  },
                  {
                    "@context": "https://schema.org",
                    "@type": "Recipe",
                    "name": "Best Buffalo Wings",
                    "description": "Oven baked wings with buffalo sauce.",
                    "recipeCategory": "Dinner",
                    "recipeIngredient": ["2 lb wings", "1/2 cup hot sauce"],
                    "recipeInstructions": [
                      { "@type": "HowToStep", "text": "Bake wings until crisp." },
                      { "@type": "HowToStep", "text": "Toss with sauce." }
                    ]
                  }
                ]
                </script>
              </body>
            </html>
            """;

        var draft = RecipeImportService.ParseImportedRecipeDraft(sourceUrl, html);

        draft.Name.Should().Be("Best Buffalo Wings");
        draft.Category.Should().Be("dinner");
        draft.Ingredients.Select(i => i.Name).Should().Contain(["2 lb wings", "1/2 cup hot sauce"]);
        draft.Instructions.Should().ContainInOrder("Bake wings until crisp.", "Toss with sauce.");
    }

    [Fact]
    public async Task ParseImportedRecipeDraftAsync_WhenJsonLdMissing_UsesAiFallbackWhenConfigured()
    {
        const string openAiResponse = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"name\":\"Crispy Buffalo Wings\",\"description\":\"Spicy baked wings.\",\"category\":\"dinner\",\"cuisine\":\"American\",\"servings\":4,\"prepTimeMinutes\":15,\"cookTimeMinutes\":40,\"tags\":[\"spicy\",\"game day\"],\"ingredients\":[\"2 lb chicken wings\",\"1/2 cup buffalo sauce\"],\"instructions\":[\"Pat wings dry.\",\"Bake until crisp.\",\"Toss in sauce.\"]}"
                  }
                }
              ]
            }
            """;

        using var httpClient = new HttpClient(new OpenAiOnlyHttpMessageHandler(openAiResponse));
        var service = new RecipeImportService(
            httpClient,
          null,
            new StubApiKeyProvider("sk-test"),
            Options.Create(new OpenAiOptions()),
            NullLogger<RecipeImportService>.Instance);

        var draft = await service.ParseImportedRecipeDraftAsync(
            new Uri("https://example.com/ai-fallback"),
            "<html><head><title>Wings</title></head><body><h1>Buffalo Wings</h1><p>Bake and toss in sauce.</p></body></html>",
            CancellationToken.None);

        draft.Name.Should().Be("Crispy Buffalo Wings");
        draft.Description.Should().Be("Spicy baked wings.");
        draft.Category.Should().Be("dinner");
        draft.Cuisine.Should().Be("American");
        draft.Servings.Should().Be(4);
        draft.PrepTimeMinutes.Should().Be(15);
        draft.CookTimeMinutes.Should().Be(40);
        draft.Tags.Should().Contain(["spicy", "game day"]);
        draft.Ingredients.Select(i => i.Name).Should().Contain(["2 lb chicken wings", "1/2 cup buffalo sauce"]);
        draft.Instructions.Should().ContainInOrder("Pat wings dry.", "Bake until crisp.", "Toss in sauce.");
        draft.Warnings.Should().Contain("AI-assisted extraction was used. Review before saving.");
    }

    [Fact]
    public async Task ImportFromImageAsync_WhenAiReturnsRecipe_ParsesImageDraft()
    {
        const string openAiResponse = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"name\":\"Cacciucco\",\"description\":\"Tuscan seafood stew\",\"category\":\"dinner\",\"cuisine\":\"Italian\",\"servings\":6,\"prepTimeMinutes\":25,\"cookTimeMinutes\":60,\"tags\":[\"seafood\",\"stew\"],\"ingredients\":[\"1 kg mixed seafood\",\"400 ml passata\"],\"instructions\":[\"Saute aromatics.\",\"Add seafood and simmer.\"]}"
                  }
                }
              ]
            }
            """;

        using var httpClient = new HttpClient(new OpenAiOnlyHttpMessageHandler(openAiResponse));
        var service = new RecipeImportService(
            httpClient,
          null,
            new StubApiKeyProvider("sk-test"),
            Options.Create(new OpenAiOptions()),
            NullLogger<RecipeImportService>.Instance);

        var draft = await service.ImportFromImageAsync("https://example.com/recipe-image.jpg", cancellationToken: CancellationToken.None);

        draft.Name.Should().Be("Cacciucco");
        draft.Description.Should().Be("Tuscan seafood stew");
        draft.Category.Should().Be("dinner");
        draft.Cuisine.Should().Be("Italian");
        draft.Servings.Should().Be(6);
        draft.PrepTimeMinutes.Should().Be(25);
        draft.CookTimeMinutes.Should().Be(60);
        draft.Tags.Should().Contain(["seafood", "stew"]);
        draft.Ingredients.Select(i => i.Name).Should().Contain(["1 kg mixed seafood", "400 ml passata"]);
        draft.Instructions.Should().ContainInOrder("Saute aromatics.", "Add seafood and simmer.");
        draft.SourceType.Should().Be("image_upload");
        draft.Warnings.Should().Contain("AI vision extraction was used. Verify ingredients and steps before saving.");
    }

    [Fact]
    public async Task ImportFromImageAsync_WhenAiReturns429_ThrowsHttpRequestExceptionWith429Status()
    {
        using var httpClient = new HttpClient(new TooManyRequestsHttpMessageHandler());
        var service = new RecipeImportService(
            httpClient,
            null,
            new StubApiKeyProvider("sk-test"),
            Options.Create(new OpenAiOptions()),
            NullLogger<RecipeImportService>.Instance);

        var act = async () => await service.ImportFromImageAsync("https://example.com/recipe-image.jpg", cancellationToken: CancellationToken.None);

        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.TooManyRequests);
        exception.Which.Message.Should().Contain("rate limited");
    }

    [Fact]
    public async Task ImportFromImageAsync_WhenPreferOcr_SkipsVisionAndUsesTextractPath()
    {
        // With null Textract and valid AI key, preferOcr=true should fail on OCR (not Vision)
        using var httpClient = new HttpClient(new OpenAiOnlyHttpMessageHandler("{}"));
        var service = new RecipeImportService(
            httpClient,
            null, // no Textract client
            new StubApiKeyProvider("sk-test"),
            Options.Create(new OpenAiOptions()),
            NullLogger<RecipeImportService>.Instance);

        var act = async () => await service.ImportFromImageAsync("https://example.com/recipe-image.jpg", preferOcr: true, CancellationToken.None);

        // Without Textract configured, OCR path throws immediately (no HTTP call to Vision made)
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*OCR extraction did not produce usable results*");
    }

      [Fact]
      public async Task ImportFromUrlAsync_WhenUrlHasEmbeddedCredentials_RejectsRequest()
      {
        using var httpClient = new HttpClient(new StubHttpMessageHandler())
        {
          BaseAddress = new Uri("https://example.com")
        };
        var service = new RecipeImportService(
          httpClient,
          null,
          null,
          null,
          NullLogger<RecipeImportService>.Instance);

        var act = async () => await service.ImportFromUrlAsync("https://user:pass@example.com/recipe", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
          .WithMessage("*embedded credentials*");
      }

      [Fact]
      public async Task ImportFromUrlAsync_WhenResponseIsTruncated_AddsTruncationWarning()
      {
        var largeHtml = "<html><head><title>Big Soup</title></head><body><h2>Ingredients</h2><ul><li>1 onion</li></ul><h2>Instructions</h2><ol><li>Cook slowly.</li></ol>" + new string('a', 1_100_000) + "</body></html>";
        using var httpClient = new HttpClient(new LargeHtmlHttpMessageHandler(largeHtml));
        var service = new RecipeImportService(
          httpClient,
          null,
          null,
          null,
          NullLogger<RecipeImportService>.Instance);

        var draft = await service.ImportFromUrlAsync("https://example.com/large-recipe", CancellationToken.None);

        draft.Name.Should().Be("Big Soup");
        draft.Warnings.Should().Contain(w => w.Contains("truncated snapshot", StringComparison.OrdinalIgnoreCase));
      }

      [Fact]
      public async Task ImportFromImageAsync_WhenApiKeyIsBlank_ThrowsInvalidOperationException()
      {
        using var httpClient = new HttpClient(new OpenAiOnlyHttpMessageHandler("{}"));
        var service = new RecipeImportService(
          httpClient,
          null,
          new StubApiKeyProvider("   "),
          Options.Create(new OpenAiOptions()),
          NullLogger<RecipeImportService>.Instance);

        var act = async () => await service.ImportFromImageAsync("https://example.com/recipe-image.jpg", cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
          .WithMessage("*API key is unavailable*");
      }

      [Fact]
      public async Task ImportFromImageAsync_WhenAiReturnsInvalidRecipe_ThrowsInvalidOperationException()
      {
        const string invalidOpenAiResponse = """
          {
            "choices": [
            {
              "message": {
              "content": "{\"description\":\"missing name\"}"
              }
            }
            ]
          }
          """;

        using var httpClient = new HttpClient(new OpenAiOnlyHttpMessageHandler(invalidOpenAiResponse));
        var service = new RecipeImportService(
          httpClient,
          null,
          new StubApiKeyProvider("sk-test"),
          Options.Create(new OpenAiOptions()),
          NullLogger<RecipeImportService>.Instance);

        var act = async () => await service.ImportFromImageAsync("https://example.com/recipe-image.jpg", cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
          .WithMessage("*did not return a usable recipe*");
      }

    private sealed class TooManyRequestsHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests));
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("<html><title>stub</title></html>")
            });
        }
    }

    private sealed class OpenAiOnlyHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public OpenAiOnlyHttpMessageHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri.Should().NotBeNull();
            request.RequestUri!.ToString().Should().Contain("/chat/completions");
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody)
            });
        }
    }

        private sealed class LargeHtmlHttpMessageHandler : HttpMessageHandler
        {
          private readonly string _html;

          public LargeHtmlHttpMessageHandler(string html)
          {
            _html = html;
          }

          protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
          {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
              Content = new StringContent(_html, Encoding.UTF8, mediaType: "text/html")
            });
          }
        }

    private sealed class StubApiKeyProvider : IOpenAiApiKeyProvider
    {
        private readonly string? _apiKey;

        public StubApiKeyProvider(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apiKey);
        }
    }
}
