using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.Recipes;

namespace ThcMealPlanner.Api.MealPlans;

public sealed class ConstraintConfig
{
    public const string SectionName = "ConstraintEngine";

    public List<string> NoCookDays { get; init; } = ["Wednesday"];

    public int MaxWeekdayPrepMinutes { get; init; } = 45;

    public int MaxWeekendPrepMinutes { get; init; } = 180;

    public List<string> MealTypes { get; init; } = ["breakfast", "lunch", "dinner"];
}

public sealed class ConstraintViolation
{
    public string Rule { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}

public sealed class ConstraintValidationResult
{
    public bool IsValid => Violations.Count == 0;

    public List<ConstraintViolation> Violations { get; init; } = [];
}

public interface IConstraintEngine
{
    ConstraintValidationResult ValidateMealSlot(string day, string mealType, RecipeDocument recipe);

    QualityScoreDocument ScorePlan(IReadOnlyList<MealSlotDocument> meals, int constraintViolationCount);
}

public sealed class ConstraintEngine : IConstraintEngine
{
    private static readonly HashSet<string> ActiveCookingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stovetop", "Grilled", "Baked", "Stir-fried", "Pan-fried", "Steamed", "Deep-fried"
    };

    private static readonly string[] WeekendDays = ["Saturday", "Sunday"];

    private readonly ConstraintConfig _config;

    public ConstraintEngine(IOptions<ConstraintConfig> options)
    {
        _config = options.Value;
    }

    public ConstraintValidationResult ValidateMealSlot(string day, string mealType, RecipeDocument recipe)
    {
        var violations = new List<ConstraintViolation>();

        var isNoCookDay = _config.NoCookDays.Any(d => string.Equals(d, day, StringComparison.OrdinalIgnoreCase));

        if (isNoCookDay)
        {
            var cookingMethods = recipe.CookingMethod ?? [];
            var hasActiveCooking = cookingMethods.Any(m => ActiveCookingMethods.Contains(m));

            if (hasActiveCooking)
            {
                violations.Add(new ConstraintViolation
                {
                    Rule = "NoCookDay",
                    Detail = $"{day} is a no-cook day but recipe '{recipe.Name}' requires active cooking ({string.Join(", ", cookingMethods)})."
                });
            }
        }

        var isWeekend = WeekendDays.Any(d => string.Equals(d, day, StringComparison.OrdinalIgnoreCase));
        var maxPrepMinutes = isWeekend ? _config.MaxWeekendPrepMinutes : _config.MaxWeekdayPrepMinutes;
        var totalPrepMinutes = (recipe.PrepTimeMinutes ?? 0) + (recipe.CookTimeMinutes ?? 0);

        if (totalPrepMinutes > maxPrepMinutes && totalPrepMinutes > 0)
        {
            violations.Add(new ConstraintViolation
            {
                Rule = "PrepTime",
                Detail = $"Recipe '{recipe.Name}' requires {totalPrepMinutes} minutes but the {(isWeekend ? "weekend" : "weekday")} limit is {maxPrepMinutes} minutes."
            });
        }

        return new ConstraintValidationResult { Violations = violations };
    }

    public QualityScoreDocument ScorePlan(IReadOnlyList<MealSlotDocument> meals, int constraintViolationCount)
    {
        if (meals.Count == 0)
        {
            return new QualityScoreDocument
            {
                Overall = 0,
                VarietyScore = 0,
                DiversityScore = 0,
                ConstraintViolations = constraintViolationCount,
                Grade = "F"
            };
        }

        // Variety: unique recipes used relative to total slots (0–40 pts)
        var uniqueRecipes = meals.Select(m => m.RecipeId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var varietyScore = (int)Math.Round((double)uniqueRecipes / meals.Count * 40);

        // Diversity: unique days + meal types covered (0–30 pts)
        var uniqueDays = meals.Select(m => m.Day).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var uniqueMealTypes = meals.Select(m => m.MealType).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var diversityScore = (int)Math.Round((double)uniqueDays / 7.0 * 20 + (double)uniqueMealTypes / 4.0 * 10);

        // Base completeness bonus (0–30 pts)
        var completenessScore = Math.Min(30, meals.Count * 2);

        // Penalty: constraint violations
        var penalty = Math.Min(50, constraintViolationCount * 10);

        var overall = Math.Clamp(varietyScore + diversityScore + completenessScore - penalty, 0, 100);
        var grade = ComputeGrade(overall);

        return new QualityScoreDocument
        {
            Overall = overall,
            VarietyScore = varietyScore,
            DiversityScore = diversityScore,
            ConstraintViolations = constraintViolationCount,
            Grade = grade
        };
    }

    private static string ComputeGrade(int overall) => overall switch
    {
        >= 90 => "A",
        >= 80 => "B+",
        >= 70 => "B",
        >= 60 => "C+",
        >= 50 => "C",
        >= 40 => "D",
        _ => "F"
    };
}
