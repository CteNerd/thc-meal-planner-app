using FluentValidation;

namespace ThcMealPlanner.Api.MealPlans;

internal static class MealPlanConstants
{
    public static readonly HashSet<string> ValidDays = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

    public static readonly HashSet<string> ValidMealTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "breakfast", "lunch", "dinner", "snack"
    };

    public static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active", "archived"
    };
}

public sealed class CreateMealPlanRequestValidator : AbstractValidator<CreateMealPlanRequest>
{
    public CreateMealPlanRequestValidator()
    {
        RuleFor(x => x.WeekStartDate)
            .NotEmpty()
            .Must(BeAMonday)
            .WithMessage("weekStartDate must be a valid ISO date (yyyy-MM-dd) that falls on a Monday.");

        RuleForEach(x => x.Meals).SetValidator(new MealSlotRequestValidator());
    }

    private static bool BeAMonday(string date)
    {
        return DateOnly.TryParse(date, out var d) && d.DayOfWeek == DayOfWeek.Monday;
    }
}

public sealed class UpdateMealPlanRequestValidator : AbstractValidator<UpdateMealPlanRequest>
{
    public UpdateMealPlanRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is null || MealPlanConstants.ValidStatuses.Contains(s))
            .WithMessage($"status must be one of: {string.Join(", ", MealPlanConstants.ValidStatuses)}.");

        RuleForEach(x => x.Meals)
            .SetValidator(new MealSlotRequestValidator())
            .When(x => x.Meals is not null);
    }
}

public sealed class GenerateMealPlanRequestValidator : AbstractValidator<GenerateMealPlanRequest>
{
    public GenerateMealPlanRequestValidator()
    {
        RuleFor(x => x.WeekStartDate)
            .NotEmpty()
            .Must(BeAMonday)
            .WithMessage("weekStartDate must be a valid ISO date (yyyy-MM-dd) that falls on a Monday.");

        RuleFor(x => x.Prompt)
            .MaximumLength(500)
            .When(x => x.Prompt is not null);
    }

    private static bool BeAMonday(string date)
    {
        return DateOnly.TryParse(date, out var d) && d.DayOfWeek == DayOfWeek.Monday;
    }
}

internal sealed class MealSlotRequestValidator : AbstractValidator<CreateMealSlotRequest>
{
    public MealSlotRequestValidator()
    {
        RuleFor(x => x.Day)
            .NotEmpty()
            .Must(d => MealPlanConstants.ValidDays.Contains(d))
            .WithMessage($"day must be one of: {string.Join(", ", MealPlanConstants.ValidDays)}.");

        RuleFor(x => x.MealType)
            .NotEmpty()
            .Must(t => MealPlanConstants.ValidMealTypes.Contains(t))
            .WithMessage($"mealType must be one of: {string.Join(", ", MealPlanConstants.ValidMealTypes)}.");

        RuleFor(x => x.RecipeId)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Servings)
            .GreaterThan(0)
            .When(x => x.Servings.HasValue);
    }
}
