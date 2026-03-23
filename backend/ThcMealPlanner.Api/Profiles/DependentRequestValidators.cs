using FluentValidation;

namespace ThcMealPlanner.Api.Profiles;

public sealed class CreateDependentRequestValidator : AbstractValidator<CreateDependentRequest>
{
    public CreateDependentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.AgeGroup)
            .MaximumLength(50)
            .When(x => x.AgeGroup is not null);

        RuleFor(x => x.EatingStyle)
            .MaximumLength(200)
            .When(x => x.EatingStyle is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes is not null);

        RuleForEach(x => x.DietaryPrefs)
            .NotEmpty()
            .MaximumLength(50);

        RuleForEach(x => x.PreferredFoods)
            .NotEmpty()
            .MaximumLength(100);

        RuleForEach(x => x.AvoidedFoods)
            .NotEmpty()
            .MaximumLength(100);

        RuleForEach(x => x.Allergies)
            .SetValidator(new AllergyValidator());

        RuleFor(x => x.MacroTargets)
            .SetValidator(new MacroTargetsValidator())
            .When(x => x.MacroTargets is not null);
    }
}

public sealed class UpdateDependentRequestValidator : AbstractValidator<UpdateDependentRequest>
{
    public UpdateDependentRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100)
            .When(x => x.Name is not null);

        RuleFor(x => x.AgeGroup)
            .MaximumLength(50)
            .When(x => x.AgeGroup is not null);

        RuleFor(x => x.EatingStyle)
            .MaximumLength(200)
            .When(x => x.EatingStyle is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes is not null);

        RuleForEach(x => x.DietaryPrefs)
            .NotEmpty()
            .MaximumLength(50);

        RuleForEach(x => x.PreferredFoods)
            .NotEmpty()
            .MaximumLength(100);

        RuleForEach(x => x.AvoidedFoods)
            .NotEmpty()
            .MaximumLength(100);

        RuleForEach(x => x.Allergies)
            .SetValidator(new AllergyValidator());

        RuleFor(x => x.MacroTargets)
            .SetValidator(new MacroTargetsValidator())
            .When(x => x.MacroTargets is not null);
    }
}

internal sealed class AllergyValidator : AbstractValidator<AllergyModel>
{
    public AllergyValidator()
    {
        RuleFor(x => x.Allergen).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Severity).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Reaction).MaximumLength(200).When(x => x.Reaction is not null);
    }
}

internal sealed class MacroTargetsValidator : AbstractValidator<MacroTargetsModel>
{
    public MacroTargetsValidator()
    {
        RuleFor(x => x.Calories).GreaterThan(0).When(x => x.Calories.HasValue);
        RuleFor(x => x.Protein).GreaterThanOrEqualTo(0).When(x => x.Protein.HasValue);
        RuleFor(x => x.Carbohydrates).GreaterThanOrEqualTo(0).When(x => x.Carbohydrates.HasValue);
        RuleFor(x => x.Fat).GreaterThanOrEqualTo(0).When(x => x.Fat.HasValue);
        RuleFor(x => x.Fiber).GreaterThanOrEqualTo(0).When(x => x.Fiber.HasValue);
        RuleFor(x => x.Sodium).GreaterThanOrEqualTo(0).When(x => x.Sodium.HasValue);
    }
}