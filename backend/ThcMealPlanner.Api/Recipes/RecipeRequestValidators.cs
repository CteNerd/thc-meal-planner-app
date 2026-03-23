using FluentValidation;

namespace ThcMealPlanner.Api.Recipes;

public sealed class CreateRecipeRequestValidator : AbstractValidator<CreateRecipeRequest>
{
    public CreateRecipeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(IsKnownCategory)
            .WithMessage("Category must be one of: breakfast, lunch, dinner, snack.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description is not null);

        RuleFor(x => x.Cuisine)
            .MaximumLength(100)
            .When(x => x.Cuisine is not null);

        RuleFor(x => x.ProteinSource)
            .MaximumLength(100)
            .When(x => x.ProteinSource is not null);

        RuleFor(x => x.CookingMethod)
            .MaximumLength(100)
            .When(x => x.CookingMethod is not null);

        RuleFor(x => x.Difficulty)
            .MaximumLength(50)
            .When(x => x.Difficulty is not null);

        RuleFor(x => x.ImageKey)
            .MaximumLength(300)
            .When(x => x.ImageKey is not null);

        RuleFor(x => x.Variations)
            .MaximumLength(2000)
            .When(x => x.Variations is not null);

        RuleFor(x => x.StorageInfo)
            .MaximumLength(1000)
            .When(x => x.StorageInfo is not null);

        RuleFor(x => x.Servings)
            .GreaterThan(0)
            .When(x => x.Servings.HasValue);

        RuleFor(x => x.PrepTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PrepTimeMinutes.HasValue);

        RuleFor(x => x.CookTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CookTimeMinutes.HasValue);

        RuleFor(x => x.Ingredients)
            .NotNull()
            .Must(ingredients => ingredients is { Count: > 0 })
            .WithMessage("At least one ingredient is required.");

        RuleForEach(x => x.Ingredients!)
            .SetValidator(new RecipeIngredientModelValidator());

        RuleFor(x => x.Instructions)
            .NotNull()
            .Must(instructions => instructions is { Count: > 0 })
            .WithMessage("At least one instruction is required.");

        RuleForEach(x => x.Instructions!)
            .NotEmpty()
            .MaximumLength(500);

        RuleForEach(x => x.Tags)
            .NotEmpty()
            .MaximumLength(40);

        RuleFor(x => x.Nutrition)
            .SetValidator(new RecipeNutritionModelValidator())
            .When(x => x.Nutrition is not null);
    }

    private static bool IsKnownCategory(string category)
    {
        return RecipeCategories.All.Contains(category);
    }
}

public sealed class UpdateRecipeRequestValidator : AbstractValidator<UpdateRecipeRequest>
{
    public UpdateRecipeRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(150)
            .When(x => x.Name is not null);

        RuleFor(x => x.Category)
            .Must(IsKnownCategory)
            .WithMessage("Category must be one of: breakfast, lunch, dinner, snack.")
            .When(x => x.Category is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description is not null);

        RuleFor(x => x.Cuisine)
            .MaximumLength(100)
            .When(x => x.Cuisine is not null);

        RuleFor(x => x.ProteinSource)
            .MaximumLength(100)
            .When(x => x.ProteinSource is not null);

        RuleFor(x => x.CookingMethod)
            .MaximumLength(100)
            .When(x => x.CookingMethod is not null);

        RuleFor(x => x.Difficulty)
            .MaximumLength(50)
            .When(x => x.Difficulty is not null);

        RuleFor(x => x.ImageKey)
            .MaximumLength(300)
            .When(x => x.ImageKey is not null);

        RuleFor(x => x.Variations)
            .MaximumLength(2000)
            .When(x => x.Variations is not null);

        RuleFor(x => x.StorageInfo)
            .MaximumLength(1000)
            .When(x => x.StorageInfo is not null);

        RuleFor(x => x.Servings)
            .GreaterThan(0)
            .When(x => x.Servings.HasValue);

        RuleFor(x => x.PrepTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PrepTimeMinutes.HasValue);

        RuleFor(x => x.CookTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CookTimeMinutes.HasValue);

        RuleFor(x => x.Ingredients)
            .Must(ingredients => ingredients is null || ingredients.Count > 0)
            .WithMessage("Ingredients cannot be empty when provided.");

        RuleForEach(x => x.Ingredients)
            .SetValidator(new RecipeIngredientModelValidator());

        RuleFor(x => x.Instructions)
            .Must(instructions => instructions is null || instructions.Count > 0)
            .WithMessage("Instructions cannot be empty when provided.");

        RuleForEach(x => x.Instructions)
            .NotEmpty()
            .MaximumLength(500);

        RuleForEach(x => x.Tags)
            .NotEmpty()
            .MaximumLength(40);

        RuleFor(x => x.Nutrition)
            .SetValidator(new RecipeNutritionModelValidator())
            .When(x => x.Nutrition is not null);
    }

    private static bool IsKnownCategory(string category)
    {
        return RecipeCategories.All.Contains(category);
    }
}

public sealed class FavoriteRecipeRequestValidator : AbstractValidator<FavoriteRecipeRequest>
{
    public FavoriteRecipeRequestValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes is not null);

        RuleFor(x => x.PortionOverride)
            .GreaterThan(0)
            .When(x => x.PortionOverride.HasValue);
    }
}

internal sealed class RecipeIngredientModelValidator : AbstractValidator<RecipeIngredientModel>
{
    public RecipeIngredientModelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Quantity)
            .MaximumLength(30)
            .When(x => x.Quantity is not null);

        RuleFor(x => x.Unit)
            .MaximumLength(30)
            .When(x => x.Unit is not null);

        RuleFor(x => x.Section)
            .MaximumLength(50)
            .When(x => x.Section is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(200)
            .When(x => x.Notes is not null);
    }
}

internal sealed class RecipeNutritionModelValidator : AbstractValidator<RecipeNutritionModel>
{
    public RecipeNutritionModelValidator()
    {
        RuleFor(x => x.Calories).GreaterThan(0).When(x => x.Calories.HasValue);
        RuleFor(x => x.Protein).GreaterThanOrEqualTo(0).When(x => x.Protein.HasValue);
        RuleFor(x => x.Carbohydrates).GreaterThanOrEqualTo(0).When(x => x.Carbohydrates.HasValue);
        RuleFor(x => x.Fat).GreaterThanOrEqualTo(0).When(x => x.Fat.HasValue);
        RuleFor(x => x.Fiber).GreaterThanOrEqualTo(0).When(x => x.Fiber.HasValue);
        RuleFor(x => x.Sodium).GreaterThanOrEqualTo(0).When(x => x.Sodium.HasValue);
        RuleFor(x => x.Sugar).GreaterThanOrEqualTo(0).When(x => x.Sugar.HasValue);
    }
}

internal static class RecipeCategories
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "breakfast",
        "lunch",
        "dinner",
        "snack"
    };
}
