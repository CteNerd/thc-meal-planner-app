using FluentValidation;

namespace ThcMealPlanner.Api.GroceryLists;

public sealed class GenerateGroceryListRequestValidator : AbstractValidator<GenerateGroceryListRequest>
{
    public GenerateGroceryListRequestValidator()
    {
        RuleFor(x => x.WeekStartDate)
            .Must(BeAValidIsoDate)
            .When(x => !string.IsNullOrWhiteSpace(x.WeekStartDate))
            .WithMessage("weekStartDate must be a valid ISO date (yyyy-MM-dd).");
    }

    private static bool BeAValidIsoDate(string? weekStartDate)
    {
        return DateOnly.TryParse(weekStartDate, out _);
    }
}

public sealed class ToggleGroceryItemRequestValidator : AbstractValidator<ToggleGroceryItemRequest>
{
    public ToggleGroceryItemRequestValidator()
    {
        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(1);
    }
}

public sealed class AddGroceryItemRequestValidator : AbstractValidator<AddGroceryItemRequest>
{
    public AddGroceryItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Section)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .When(x => x.Quantity.HasValue);

        RuleFor(x => x.Unit)
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.Unit));

        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(1);
    }
}

public sealed class SetInStockRequestValidator : AbstractValidator<SetInStockRequest>
{
    public SetInStockRequestValidator()
    {
        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(1);
    }
}

public sealed class RemoveGroceryItemRequestValidator : AbstractValidator<RemoveGroceryItemRequest>
{
    public RemoveGroceryItemRequestValidator()
    {
        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(1);
    }
}

public sealed class ReplacePantryStaplesRequestValidator : AbstractValidator<ReplacePantryStaplesRequest>
{
    public ReplacePantryStaplesRequestValidator()
    {
        RuleForEach(x => x.Items).SetValidator(new PantryStapleItemValidator());

        RuleForEach(x => x.PreferredSectionOrder)
            .NotEmpty()
            .MaximumLength(50)
            .When(x => x.PreferredSectionOrder is not null);
    }
}

public sealed class AddPantryStapleItemRequestValidator : AbstractValidator<AddPantryStapleItemRequest>
{
    public AddPantryStapleItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Section)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.Section));
    }
}

internal sealed class PantryStapleItemValidator : AbstractValidator<PantryStapleItemDocument>
{
    public PantryStapleItemValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Section)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.Section));
    }
}
