using FluentAssertions;
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
        var service = new RecipeImportService(httpClient);

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
}
