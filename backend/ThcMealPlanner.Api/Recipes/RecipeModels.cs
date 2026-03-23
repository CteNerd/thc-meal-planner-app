namespace ThcMealPlanner.Api.Recipes;

public sealed class RecipeDocument
{
    public string RecipeId { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Category { get; init; } = string.Empty;

    public string? Cuisine { get; init; }

    public int? Servings { get; init; }

    public int? PrepTimeMinutes { get; init; }

    public int? CookTimeMinutes { get; init; }

    public string? ProteinSource { get; init; }

    public string? CookingMethod { get; init; }

    public string? Difficulty { get; init; }

    public List<string> Tags { get; init; } = [];

    public List<RecipeIngredientModel> Ingredients { get; init; } = [];

    public List<string> Instructions { get; init; } = [];

    public RecipeNutritionModel? Nutrition { get; init; }

    public string? ImageKey { get; init; }

    public string? Variations { get; init; }

    public string? StorageInfo { get; init; }

    public string CreatedByUserId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class RecipeIngredientModel
{
    public string Name { get; init; } = string.Empty;

    public string? Quantity { get; init; }

    public string? Unit { get; init; }

    public string? Section { get; init; }

    public string? Notes { get; init; }
}

public sealed class RecipeNutritionModel
{
    public int? Calories { get; init; }

    public int? Protein { get; init; }

    public int? Carbohydrates { get; init; }

    public int? Fat { get; init; }

    public int? Fiber { get; init; }

    public int? Sodium { get; init; }

    public int? Sugar { get; init; }
}

public sealed class CreateRecipeRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Category { get; init; } = string.Empty;

    public string? Cuisine { get; init; }

    public int? Servings { get; init; }

    public int? PrepTimeMinutes { get; init; }

    public int? CookTimeMinutes { get; init; }

    public string? ProteinSource { get; init; }

    public string? CookingMethod { get; init; }

    public string? Difficulty { get; init; }

    public List<string>? Tags { get; init; }

    public List<RecipeIngredientModel>? Ingredients { get; init; }

    public List<string>? Instructions { get; init; }

    public RecipeNutritionModel? Nutrition { get; init; }

    public string? ImageKey { get; init; }

    public string? Variations { get; init; }

    public string? StorageInfo { get; init; }
}

public sealed class UpdateRecipeRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? Category { get; init; }

    public string? Cuisine { get; init; }

    public int? Servings { get; init; }

    public int? PrepTimeMinutes { get; init; }

    public int? CookTimeMinutes { get; init; }

    public string? ProteinSource { get; init; }

    public string? CookingMethod { get; init; }

    public string? Difficulty { get; init; }

    public List<string>? Tags { get; init; }

    public List<RecipeIngredientModel>? Ingredients { get; init; }

    public List<string>? Instructions { get; init; }

    public RecipeNutritionModel? Nutrition { get; init; }

    public string? ImageKey { get; init; }

    public string? Variations { get; init; }

    public string? StorageInfo { get; init; }
}

public sealed class FavoriteRecipeDocument
{
    public string UserId { get; init; } = string.Empty;

    public string RecipeId { get; init; } = string.Empty;

    public string RecipeName { get; init; } = string.Empty;

    public string RecipeCategory { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public int? PortionOverride { get; init; }

    public DateTimeOffset AddedAt { get; init; }
}

public sealed class FavoriteRecipeRequest
{
    public string? Notes { get; init; }

    public int? PortionOverride { get; init; }
}
