using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.Recipes;

public interface IRecipeService
{
    Task<IReadOnlyList<RecipeDocument>> ListByFamilyAsync(string familyId, CancellationToken cancellationToken = default);

    Task<RecipeDocument?> GetByIdAsync(string familyId, string recipeId, CancellationToken cancellationToken = default);

    Task<RecipeDocument> CreateAsync(string familyId, string userId, CreateRecipeRequest request, CancellationToken cancellationToken = default);

    Task<RecipeDocument?> UpdateAsync(string familyId, string recipeId, UpdateRecipeRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string familyId, string recipeId, CancellationToken cancellationToken = default);

    Task<FavoriteRecipeDocument?> AddFavoriteAsync(
        string familyId,
        string userId,
        string recipeId,
        FavoriteRecipeRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveFavoriteAsync(string userId, string recipeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteRecipeDocument>> ListFavoritesAsync(
        string userId,
        string? category,
        CancellationToken cancellationToken = default);
}

public sealed class RecipeService : IRecipeService
{
    private readonly IDynamoDbRepository<RecipeDocument> _recipeRepository;
    private readonly IDynamoDbRepository<FavoriteRecipeDocument> _favoriteRepository;

    public RecipeService(
        IDynamoDbRepository<RecipeDocument> recipeRepository,
        IDynamoDbRepository<FavoriteRecipeDocument> favoriteRepository)
    {
        _recipeRepository = recipeRepository;
        _favoriteRepository = favoriteRepository;
    }

    public async Task<IReadOnlyList<RecipeDocument>> ListByFamilyAsync(string familyId, CancellationToken cancellationToken = default)
    {
        var recipes = await _recipeRepository.QueryByIndexPartitionKeyAsync(
            indexName: "FamilyIndex",
            partitionKeyName: "familyId",
            partitionKeyValue: familyId,
            cancellationToken: cancellationToken);

        return recipes
            .OrderBy(recipe => recipe.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<RecipeDocument?> GetByIdAsync(string familyId, string recipeId, CancellationToken cancellationToken = default)
    {
        var recipe = await _recipeRepository.GetAsync(ToRecipeKey(familyId, recipeId), cancellationToken);
        if (recipe is null)
        {
            return null;
        }

        return string.Equals(recipe.FamilyId, familyId, StringComparison.Ordinal) ? recipe : null;
    }

    public async Task<RecipeDocument> CreateAsync(
        string familyId,
        string userId,
        CreateRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var recipeId = $"rec_{Guid.NewGuid().ToString("N")[..10]}";

        var recipe = new RecipeDocument
        {
            RecipeId = recipeId,
            FamilyId = familyId,
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            Cuisine = request.Cuisine,
            Servings = request.Servings,
            PrepTimeMinutes = request.PrepTimeMinutes,
            CookTimeMinutes = request.CookTimeMinutes,
            ProteinSource = request.ProteinSource,
            CookingMethod = request.CookingMethod,
            Difficulty = request.Difficulty,
            Tags = request.Tags ?? [],
            Ingredients = request.Ingredients ?? [],
            Instructions = request.Instructions ?? [],
            Nutrition = request.Nutrition,
            ImageKey = request.ImageKey,
            Variations = request.Variations,
            StorageInfo = request.StorageInfo,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _recipeRepository.PutAsync(ToRecipeKey(familyId, recipeId), recipe, cancellationToken);

        return recipe;
    }

    public async Task<RecipeDocument?> UpdateAsync(
        string familyId,
        string recipeId,
        UpdateRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(familyId, recipeId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var updated = new RecipeDocument
        {
            RecipeId = existing.RecipeId,
            FamilyId = existing.FamilyId,
            Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            Category = request.Category ?? existing.Category,
            Cuisine = request.Cuisine ?? existing.Cuisine,
            Servings = request.Servings ?? existing.Servings,
            PrepTimeMinutes = request.PrepTimeMinutes ?? existing.PrepTimeMinutes,
            CookTimeMinutes = request.CookTimeMinutes ?? existing.CookTimeMinutes,
            ProteinSource = request.ProteinSource ?? existing.ProteinSource,
            CookingMethod = request.CookingMethod ?? existing.CookingMethod,
            Difficulty = request.Difficulty ?? existing.Difficulty,
            Tags = request.Tags ?? existing.Tags,
            Ingredients = request.Ingredients ?? existing.Ingredients,
            Instructions = request.Instructions ?? existing.Instructions,
            Nutrition = request.Nutrition ?? existing.Nutrition,
            ImageKey = request.ImageKey ?? existing.ImageKey,
            Variations = request.Variations ?? existing.Variations,
            StorageInfo = request.StorageInfo ?? existing.StorageInfo,
            CreatedByUserId = existing.CreatedByUserId,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _recipeRepository.PutAsync(ToRecipeKey(familyId, recipeId), updated, cancellationToken);

        return updated;
    }

    public async Task<bool> DeleteAsync(string familyId, string recipeId, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(familyId, recipeId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        await _recipeRepository.DeleteAsync(ToRecipeKey(familyId, recipeId), cancellationToken);

        return true;
    }

    public async Task<FavoriteRecipeDocument?> AddFavoriteAsync(
        string familyId,
        string userId,
        string recipeId,
        FavoriteRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        var recipe = await GetByIdAsync(familyId, recipeId, cancellationToken);
        if (recipe is null)
        {
            return null;
        }

        var favorite = new FavoriteRecipeDocument
        {
            UserId = userId,
            RecipeId = recipe.RecipeId,
            RecipeName = recipe.Name,
            RecipeCategory = recipe.Category,
            Notes = request.Notes,
            PortionOverride = request.PortionOverride,
            AddedAt = DateTimeOffset.UtcNow
        };

        await _favoriteRepository.PutAsync(ToFavoriteKey(userId, recipeId), favorite, cancellationToken);

        return favorite;
    }

    public Task RemoveFavoriteAsync(string userId, string recipeId, CancellationToken cancellationToken = default)
    {
        return _favoriteRepository.DeleteAsync(ToFavoriteKey(userId, recipeId), cancellationToken);
    }

    public async Task<IReadOnlyList<FavoriteRecipeDocument>> ListFavoritesAsync(
        string userId,
        string? category,
        CancellationToken cancellationToken = default)
    {
        var favorites = await _favoriteRepository.QueryByPartitionKeyAsync($"USER#{userId}", cancellationToken: cancellationToken);

        return favorites
            .Where(favorite => category is null || string.Equals(favorite.RecipeCategory, category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(favorite => favorite.RecipeName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DynamoDbKey ToRecipeKey(string familyId, string recipeId)
    {
        return new DynamoDbKey($"FAMILY#{familyId}", $"RECIPE#{recipeId}");
    }

    private static DynamoDbKey ToFavoriteKey(string userId, string recipeId)
    {
        return new DynamoDbKey($"USER#{userId}", $"FAV#{recipeId}");
    }
}
