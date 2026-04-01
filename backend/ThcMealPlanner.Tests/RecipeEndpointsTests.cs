using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Net.Http.Json;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class RecipeEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RecipeEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRecipes_ReturnsFamilyScopedRecipes()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();

        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_1"),
            new RecipeDocument
            {
                RecipeId = "rec_1",
                FamilyId = "FAM#test-family",
                Name = "Test Family Recipe",
                Category = "dinner",
                ImageKey = "recipes/rec_1/test.jpg",
                Ingredients = [new RecipeIngredientModel { Name = "Rice" }],
                Instructions = ["Cook"],
                CreatedByUserId = "test-user-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#other", "RECIPE#rec_2"),
            new RecipeDocument
            {
                RecipeId = "rec_2",
                FamilyId = "FAM#other",
                Name = "Other Family Recipe",
                Category = "dinner",
                Ingredients = [new RecipeIngredientModel { Name = "Pasta" }],
                Instructions = ["Cook"],
                CreatedByUserId = "other-user",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var response = await client.GetAsync("/api/recipes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeResponse>>();
        recipes.Should().NotBeNull();
        recipes!.Should().HaveCount(1);
        recipes[0].RecipeId.Should().Be("rec_1");
        recipes[0].ImageUrl.Should().Be("https://example.com/recipes/rec_1/test.jpg");
    }

    [Fact]
    public async Task PostRecipe_WithValidPayload_CreatesRecipe()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();
        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var response = await client.PostAsJsonAsync(
            "/api/recipes",
            new CreateRecipeRequest
            {
                Name = "Weeknight Stir Fry",
                Category = "dinner",
                Ingredients = [new RecipeIngredientModel { Name = "Broccoli" }],
                Instructions = ["Stir fry everything."]
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<RecipeResponse>();
        created.Should().NotBeNull();
        created!.RecipeId.Should().StartWith("rec_");
        created.FamilyId.Should().Be("FAM#test-family");
    }

    [Fact]
    public async Task GetRecipe_WhenRecipeHasImageKey_ReturnsSignedImageUrl()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();

        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_detail"),
            new RecipeDocument
            {
                RecipeId = "rec_detail",
                FamilyId = "FAM#test-family",
                Name = "Detail Recipe",
                Category = "dinner",
                ImageKey = "recipes/rec_detail/main.jpg",
                Ingredients = [new RecipeIngredientModel { Name = "Rice" }],
                Instructions = ["Cook"],
                CreatedByUserId = "test-user-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var response = await client.GetAsync("/api/recipes/rec_detail");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeResponse>();
        recipe.Should().NotBeNull();
        recipe!.ImageUrl.Should().Be("https://example.com/recipes/rec_detail/main.jpg");
    }

    [Fact]
    public async Task PostRecipe_WithInvalidPayload_ReturnsValidationProblem()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();
        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var response = await client.PostAsJsonAsync(
            "/api/recipes",
            new CreateRecipeRequest
            {
                Name = string.Empty,
                Category = "invalid",
                Ingredients = [],
                Instructions = []
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Should().ContainKey("Name");
        problem.Errors.Should().ContainKey("Category");
        problem.Errors.Should().ContainKey("Ingredients");
        problem.Errors.Should().ContainKey("Instructions");
    }

    [Fact]
    public async Task PutRecipe_OutsideFamily_ReturnsNotFoundProblemDetails()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();

        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#other", "RECIPE#rec_other"),
            new RecipeDocument
            {
                RecipeId = "rec_other",
                FamilyId = "FAM#other",
                Name = "Other Family Recipe",
                Category = "dinner",
                Ingredients = [new RecipeIngredientModel { Name = "Pasta" }],
                Instructions = ["Cook"],
                CreatedByUserId = "other-user",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var response = await client.PutAsJsonAsync(
            "/api/recipes/rec_other",
            new UpdateRecipeRequest
            {
                Name = "Updated Name"
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        problem.Title.Should().Be("Recipe not found");
    }

    [Fact]
    public async Task FavoriteEndpoints_AddListAndRemoveFavorites()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();

        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_fav"),
            new RecipeDocument
            {
                RecipeId = "rec_fav",
                FamilyId = "FAM#test-family",
                Name = "Favorite Recipe",
                Category = "lunch",
                Ingredients = [new RecipeIngredientModel { Name = "Rice" }],
                Instructions = ["Cook"],
                CreatedByUserId = "test-user-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var addResponse = await client.PostAsJsonAsync(
            "/api/recipes/rec_fav/favorite",
            new FavoriteRecipeRequest
            {
                Notes = "Double the sauce",
                PortionOverride = 6
            });

        var listResponse = await client.GetAsync("/api/recipes/favorites");
        var removeResponse = await client.DeleteAsync("/api/recipes/rec_fav/favorite");
        var listAfterRemoveResponse = await client.GetAsync("/api/recipes/favorites");

        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var addedFavorite = await addResponse.Content.ReadFromJsonAsync<FavoriteRecipeDocument>();
        addedFavorite.Should().NotBeNull();
        addedFavorite!.RecipeId.Should().Be("rec_fav");
        addedFavorite.PortionOverride.Should().Be(6);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var favorites = await listResponse.Content.ReadFromJsonAsync<List<FavoriteRecipeDocument>>();
        favorites.Should().NotBeNull();
        favorites!.Should().ContainSingle();

        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        listAfterRemoveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var favoritesAfterRemove = await listAfterRemoveResponse.Content.ReadFromJsonAsync<List<FavoriteRecipeDocument>>();
        favoritesAfterRemove.Should().NotBeNull();
        favoritesAfterRemove!.Should().BeEmpty();
    }

    [Fact]
    public async Task PostImportFromUrl_ReturnsDraftRecipe()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();
        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var response = await client.PostAsJsonAsync(
            "/api/recipes/import-from-url",
            new ImportRecipeFromUrlRequest
            {
                Url = "https://example.com/recipe"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = await response.Content.ReadFromJsonAsync<ImportedRecipeDraft>();
        draft.Should().NotBeNull();
        draft!.Name.Should().Be("Imported Draft");
        draft.SourceType.Should().Be("url");
    }

    [Fact]
    public async Task PostUploadUrl_WhenRecipeExists_ReturnsPresignedUrlPayload()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();
        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_upload"),
            new RecipeDocument
            {
                RecipeId = "rec_upload",
                FamilyId = "FAM#test-family",
                Name = "Upload Recipe",
                Category = "dinner",
                Ingredients = [new RecipeIngredientModel { Name = "Rice" }],
                Instructions = ["Cook"],
                CreatedByUserId = "test-user-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var response = await client.PostAsJsonAsync(
            "/api/recipes/rec_upload/upload-url",
            new CreateRecipeUploadUrlRequest
            {
                FileName = "dish.jpg",
                ContentType = "image/jpeg"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<RecipeUploadUrlResponse>();
        payload.Should().NotBeNull();
        payload!.ImageKey.Should().Be("recipes/rec_upload/test.jpg");
        payload.ImageUrl.Should().Be("https://example.com/recipes/rec_upload/test.jpg");
    }

    [Fact]
    public async Task PostImportFromImage_WhenRecipeHasImageKey_ReturnsDraftRecipe()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();
        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_img"),
            new RecipeDocument
            {
                RecipeId = "rec_img",
                FamilyId = "FAM#test-family",
                Name = "Image Recipe",
                Category = "dinner",
                Cuisine = "unspecified",
                ImageKey = "recipes/rec_img/test.jpg",
                Ingredients = [new RecipeIngredientModel { Name = "Rice" }],
                Instructions = ["Cook"],
                CreatedByUserId = "test-user-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(recipeRepository, favoriteRepository);

        var response = await client.PostAsJsonAsync(
            "/api/recipes/rec_img/import-from-image",
            new ImportRecipeFromImageRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = await response.Content.ReadFromJsonAsync<ImportedRecipeDraft>();
        draft.Should().NotBeNull();
        draft!.Name.Should().Be("Image Imported Draft");
        draft.SourceType.Should().Be("image_upload");
    }

    [Fact]
    public async Task GetRecipes_WhenMissingRequiredClaims_ReturnsUnauthorizedProblemDetails()
    {
        var recipeRepository = new InMemoryRecipeRepository();
        var favoriteRepository = new InMemoryFavoriteRepository();
        var client = CreateMissingClaimsClient(recipeRepository, favoriteRepository);

        var response = await client.GetAsync("/api/recipes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Missing required user claims.");
    }

    private HttpClient CreateAuthenticatedClient(
        InMemoryRecipeRepository recipeRepository,
        InMemoryFavoriteRepository favoriteRepository)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.AddScoped<IRecipeService>(_ => new RecipeService(recipeRepository, favoriteRepository));
                services.AddSingleton<IDynamoDbRepository<RecipeDocument>>(recipeRepository);
                services.AddSingleton<IDynamoDbRepository<FavoriteRecipeDocument>>(favoriteRepository);
                services.AddScoped<IRecipeImportService, FakeRecipeImportService>();
                services.AddScoped<IRecipeImageUploadService, FakeRecipeImageUploadService>();
                services.AddScoped<IValidator<CreateRecipeRequest>, CreateRecipeRequestValidator>();
                services.AddScoped<IValidator<UpdateRecipeRequest>, UpdateRecipeRequestValidator>();
                services.AddScoped<IValidator<FavoriteRecipeRequest>, FavoriteRecipeRequestValidator>();
                services.AddScoped<IValidator<ImportRecipeFromUrlRequest>, ImportRecipeFromUrlRequestValidator>();
                services.AddScoped<IValidator<ImportRecipeFromImageRequest>, ImportRecipeFromImageRequestValidator>();
                services.AddScoped<IValidator<CreateRecipeUploadUrlRequest>, CreateRecipeUploadUrlRequestValidator>();
            });
        }).CreateClient();
    }

    private HttpClient CreateMissingClaimsClient(
        InMemoryRecipeRepository recipeRepository,
        InMemoryFavoriteRepository favoriteRepository)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(MissingClaimsAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, MissingClaimsAuthHandler>(
                        MissingClaimsAuthHandler.SchemeName,
                        _ => { });

                services.AddScoped<IRecipeService>(_ => new RecipeService(recipeRepository, favoriteRepository));
                services.AddSingleton<IDynamoDbRepository<RecipeDocument>>(recipeRepository);
                services.AddSingleton<IDynamoDbRepository<FavoriteRecipeDocument>>(favoriteRepository);
                services.AddScoped<IRecipeImportService, FakeRecipeImportService>();
                services.AddScoped<IRecipeImageUploadService, FakeRecipeImageUploadService>();
                services.AddScoped<IValidator<CreateRecipeRequest>, CreateRecipeRequestValidator>();
                services.AddScoped<IValidator<UpdateRecipeRequest>, UpdateRecipeRequestValidator>();
                services.AddScoped<IValidator<FavoriteRecipeRequest>, FavoriteRecipeRequestValidator>();
                services.AddScoped<IValidator<ImportRecipeFromUrlRequest>, ImportRecipeFromUrlRequestValidator>();
                services.AddScoped<IValidator<ImportRecipeFromImageRequest>, ImportRecipeFromImageRequestValidator>();
                services.AddScoped<IValidator<CreateRecipeUploadUrlRequest>, CreateRecipeUploadUrlRequestValidator>();
            });
        }).CreateClient();
    }

    private sealed class FakeRecipeImportService : IRecipeImportService
    {
        public Task<ImportedRecipeDraft> ImportFromUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ImportedRecipeDraft
            {
                Name = "Imported Draft",
                Category = "dinner",
                Ingredients = [new RecipeIngredientModel { Name = "Imported ingredient" }],
                Instructions = ["Imported step"],
                SourceType = "url",
                SourceUrl = url
            });
        }

        public Task<ImportedRecipeDraft> ImportFromImageAsync(string imageUrl, bool preferOcr = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ImportedRecipeDraft
            {
                Name = "Image Imported Draft",
                Category = "dinner",
                Ingredients = [new RecipeIngredientModel { Name = "Image ingredient" }],
                Instructions = ["Image step"],
                SourceType = "image_upload",
                SourceUrl = imageUrl
            });
        }
    }

    private sealed class FakeRecipeImageUploadService : IRecipeImageUploadService
    {
        public Task<RecipeUploadUrlResponse> CreateUploadUrlAsync(
            string recipeId,
            CreateRecipeUploadUrlRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RecipeUploadUrlResponse
            {
                UploadUrl = "https://example.com/upload",
                ImageKey = $"recipes/{recipeId}/test.jpg",
                ImageUrl = $"https://example.com/recipes/{recipeId}/test.jpg"
            });
        }

        public string CreateReadUrl(string imageKey, TimeSpan? expiresIn = null)
        {
            return $"https://example.com/{imageKey}";
        }
    }

    private sealed class InMemoryRecipeRepository : IDynamoDbRepository<RecipeDocument>
    {
        private readonly Dictionary<string, RecipeDocument> _store = new(StringComparer.Ordinal);

        public Task<RecipeDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToMapKey(key), out var document);
            return Task.FromResult(document);
        }

        public Task PutAsync(DynamoDbKey key, RecipeDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToMapKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToMapKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RecipeDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store
                .Where(entry => entry.Key.StartsWith(partitionKey + "|", StringComparison.Ordinal))
                .Select(entry => entry.Value)
                .ToList();

            if (limit.HasValue)
            {
                items = items.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<RecipeDocument>>(items);
        }

        public Task<IReadOnlyList<RecipeDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store.Values
                .Where(item => string.Equals(item.FamilyId, partitionKeyValue, StringComparison.Ordinal))
                .ToList();

            if (limit.HasValue)
            {
                items = items.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<RecipeDocument>>(items);
        }

        private static string ToMapKey(DynamoDbKey key)
        {
            return $"{key.PartitionKey}|{key.SortKey}";
        }
    }

    private sealed class InMemoryFavoriteRepository : IDynamoDbRepository<FavoriteRecipeDocument>
    {
        private readonly Dictionary<string, FavoriteRecipeDocument> _store = new(StringComparer.Ordinal);

        public Task<FavoriteRecipeDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToMapKey(key), out var document);
            return Task.FromResult(document);
        }

        public Task PutAsync(DynamoDbKey key, FavoriteRecipeDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToMapKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToMapKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FavoriteRecipeDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store
                .Where(entry => entry.Key.StartsWith(partitionKey + "|", StringComparison.Ordinal))
                .Select(entry => entry.Value)
                .ToList();

            if (limit.HasValue)
            {
                items = items.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<FavoriteRecipeDocument>>(items);
        }

        public Task<IReadOnlyList<FavoriteRecipeDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store.Values.ToList();

            if (limit.HasValue)
            {
                items = items.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<FavoriteRecipeDocument>>(items);
        }

        private static string ToMapKey(DynamoDbKey key)
        {
            return $"{key.PartitionKey}|{key.SortKey}";
        }
    }
}
